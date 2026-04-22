// ============================================================================
// content.js — Content Script
// Injected into every page at document_idle.
// Responsibilities:
//   1. Detect if the current page IS a privacy policy (heuristic scoring).
//   2. If detected: capture the full page HTML and send it to background.js.
//   3. Show a subtle status banner in the top-right corner of the page.
// ============================================================================

(function () {
  "use strict";

  // ── Deduplication: only fire once per page load ───────────────────────────
  if (window.__aegisInitialised) return;
  window.__aegisInitialised = true;

  const CONFIDENCE_THRESHOLD = 0.65; // 65% confidence to trigger analysis

  // ── Privacy policy detection ───────────────────────────────────────────────
  /**
   * Calculates a 0–1 confidence score that this page is a privacy policy.
   * Uses URL patterns, page title, heading text, and body text signals.
   */
  function computePrivacyPolicyConfidence() {
    let score = 0;
    const url   = window.location.href.toLowerCase();
    const title = document.title.toLowerCase();

    // URL signals (high weight)
    const urlPatterns = [
      /privacy[-_]?polic/,
      /privacy[-_]?statement/,
      /data[-_]?polic/,
      /datenschutz/,          // German
      /politique[-_]?confid/, // French
      /privacidad/,           // Spanish
    ];
    for (const p of urlPatterns) {
      if (p.test(url)) { score += 0.40; break; }
    }

    // Title signals
    const titleKeywords = ["privacy policy", "privacy statement", "data policy",
                           "privacy notice", "cookie policy"];
    for (const kw of titleKeywords) {
      if (title.includes(kw)) { score += 0.30; break; }
    }

    // Heading signals (h1, h2)
    const headings = [...document.querySelectorAll("h1, h2")]
      .map(h => h.textContent.toLowerCase().trim());

    const headingKeywords = ["privacy policy", "privacy statement", "privacy notice",
                             "your privacy", "data collection", "how we use your data"];
    for (const h of headings) {
      for (const kw of headingKeywords) {
        if (h.includes(kw)) { score += 0.25; break; }
      }
    }

    // Body text density signals (look for legal section headers)
    const bodyText = (document.body?.innerText ?? "").toLowerCase();
    const bodySignals = [
      "information we collect",
      "how we use your information",
      "data retention",
      "third-party",
      "cookies and tracking",
      "your rights",
      "gdpr",
      "ccpa",
      "personal data",
    ];
    let bodyHits = 0;
    for (const sig of bodySignals) {
      if (bodyText.includes(sig)) bodyHits++;
    }
    score += Math.min(bodyHits * 0.05, 0.25);

    return Math.min(score, 1.0);
  }

  // ── Capture clean HTML (not the whole DOM — just main content) ────────────
  function capturePageHtml() {
    // Prefer <main>, <article>, or the largest text container
    const candidates = [
      document.querySelector("main"),
      document.querySelector("article"),
      document.querySelector('[role="main"]'),
      document.querySelector(".privacy-policy, .privacy, #privacy, #content, .content, .page-content"),
      document.body
    ];

    const container = candidates.find(el => el !== null) ?? document.body;

    // Clone so we can strip elements without modifying the page
    const clone = container.cloneNode(true);

    // Strip scripts, styles, navs from clone
    ["script", "style", "nav", "header", "footer", "aside", "iframe", "noscript"]
      .forEach(tag => clone.querySelectorAll(tag).forEach(el => el.remove()));

    return clone.innerHTML;
  }

  // ── Status banner ─────────────────────────────────────────────────────────
  function showBanner(state, result) {
    const existing = document.getElementById("aegis-banner");
    if (existing) existing.remove();

    const colors = {
      scanning:  { bg: "#1e3a5f", text: "#93c5fd", emoji: "🔍" },
      safe:      { bg: "#064e3b", text: "#6ee7b7", emoji: "✅" },
      caution:   { bg: "#451a03", text: "#fbbf24", emoji: "⚠️" },
      warning:   { bg: "#431407", text: "#fb923c", emoji: "🚨" },
      danger:    { bg: "#450a0a", text: "#fca5a5", emoji: "🛑" },
      error:     { bg: "#374151", text: "#9ca3af", emoji: "❓" },
    };

    const { bg, text, emoji } = colors[state] ?? colors.error;

    const banner = document.createElement("div");
    banner.id = "aegis-banner";

    let bodyHtml = "";
    if (state === "scanning") {
      bodyHtml = `<span style="font-size:11px;opacity:0.85">Analysing privacy policy…</span>`;
    } else if (result) {
      bodyHtml = `
        <strong style="font-size:13px">${result.safety_percent}% Safe</strong>
        <span style="font-size:11px;margin-left:6px;opacity:0.85">${result.risk_level}</span>
        ${result.data_selling_detected
          ? '<span style="display:block;font-size:10px;margin-top:2px;color:#fca5a5">⚠ Sells data to 3rd parties</span>'
          : ''}
      `;
    }

    banner.innerHTML = `
      <div style="
        display:flex; align-items:center; gap:8px;
        padding: 8px 12px;
      ">
        <span style="font-size:16px">${emoji}</span>
        <div style="flex:1">
          <div style="font-size:10px;font-weight:700;letter-spacing:2px;opacity:0.7;margin-bottom:2px">AEGISSENTINEL</div>
          ${bodyHtml}
        </div>
        <button id="aegis-close-btn" style="
          background:none; border:none; cursor:pointer;
          color:${text}; font-size:14px; opacity:0.6; padding:0 4px;
        ">✕</button>
      </div>
    `;

    Object.assign(banner.style, {
      position:     "fixed",
      top:          "12px",
      right:        "12px",
      zIndex:       "2147483647",
      background:   bg,
      color:        text,
      borderRadius: "8px",
      fontFamily:   "system-ui, -apple-system, sans-serif",
      boxShadow:    "0 4px 20px rgba(0,0,0,0.5)",
      maxWidth:     "220px",
      border:       `1px solid ${text}33`,
      cursor:       "default",
      transition:   "opacity 0.3s ease",
    });

    document.body.appendChild(banner);

    // Auto-dismiss after 8 seconds (keeps 3 seconds if Safe)
    const ttl = state === "safe" ? 3000 : 8000;
    setTimeout(() => {
      banner.style.opacity = "0";
      setTimeout(() => banner.remove(), 400);
    }, ttl);

    document.getElementById("aegis-close-btn")
      ?.addEventListener("click", () => banner.remove());
  }

  // ── Main detection flow ────────────────────────────────────────────────────
  async function run() {
    const confidence = computePrivacyPolicyConfidence();
    console.log(`[AegisSentinel] Confidence: ${(confidence * 100).toFixed(0)}% (${window.location.href})`);

    if (confidence < CONFIDENCE_THRESHOLD) return;

    console.log("[AegisSentinel] Privacy policy detected — triggering analysis");
    showBanner("scanning");

    const html = capturePageHtml();

    try {
      const result = await chrome.runtime.sendMessage({
        type:        "PRIVACY_POLICY_DETECTED",
        url:         window.location.href,
        title:       document.title,
        htmlContent: html
      });

      if (result?.ok && result.result) {
        const r     = result.result;
        const level = (r.risk_level ?? "").toLowerCase();
        showBanner(level === "safe" ? "safe" : level, r);
      } else {
        showBanner("error");
      }
    } catch (err) {
      console.error("[AegisSentinel] Analysis error:", err);
      showBanner("error");
    }
  }

  // Run after a short delay to let SPAs finish rendering
  setTimeout(run, 1500);
})();
