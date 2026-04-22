// popup.js — Renders the last PrivacyScore from storage
(async () => {
  const container = document.getElementById("content");

  const data = await chrome.storage.local.get(["lastResult"]);
  const r    = data.lastResult;

  if (!r) {
    container.innerHTML = `
      <div class="empty-state">
        <div class="empty-icon">🛡</div>
        <div class="empty-text">
          Navigate to a Privacy Policy page<br>to see an AI-powered analysis.
        </div>
      </div>
      <div class="footer">
        <span class="footer-link">AegisSentinel v1.0</span>
        <span class="footer-link" id="clear-btn">Clear</span>
      </div>`;
    return;
  }

  const colors = {
    Safe:    { c: "#10b981", bg: "#064e3b22" },
    Caution: { c: "#fbbf24", bg: "#451a0322" },
    Warning: { c: "#f97316", bg: "#43140722" },
    Danger:  { c: "#ef4444", bg: "#450a0a22" },
  };
  const { c: scoreColor, bg: badgeBg } = colors[r.risk_level] ?? { c: "#94a3b8", bg: "#1e293b" };

  const risksHtml = (r.key_risks ?? []).map(risk => `
    <div class="risk-item">
      <div class="risk-dot"></div>
      <span>${escHtml(risk)}</span>
    </div>`).join("");

  container.innerHTML = `
    <div class="score-section" style="--score-color:${scoreColor}; --badge-bg:${badgeBg}">
      <div class="score-circle">
        <span class="score-number">${r.safety_percent}</span>
      </div>
      <div class="score-pct">SAFETY SCORE</div>
      <div class="risk-badge">${escHtml(r.risk_level?.toUpperCase() ?? "UNKNOWN")}</div>
    </div>

    <div class="meta-row">
      <div class="meta-item">
        <div class="meta-label">Data Selling</div>
        <div class="meta-value ${r.data_selling_detected ? "selling-yes" : "selling-no"}">
          ${r.data_selling_detected ? "⚠ YES" : "✓ NO"}
        </div>
      </div>
      <div class="meta-item">
        <div class="meta-label">Retention</div>
        <div class="meta-value">${escHtml(r.retention_period ?? "Unknown")}</div>
      </div>
    </div>

    ${risksHtml ? `
    <div class="section-title">Key Risks</div>
    <div class="risk-list">${risksHtml}</div>` : ""}

    ${r.verdict ? `<div class="verdict">${escHtml(r.verdict)}</div>` : ""}

    <div class="footer">
      <span class="footer-link">AegisSentinel v1.0</span>
      <span class="footer-link" id="clear-btn">Clear</span>
    </div>`;

  document.getElementById("clear-btn")?.addEventListener("click", () => {
    chrome.storage.local.remove("lastResult");
    window.close();
  });

  function escHtml(str) {
    return String(str)
      .replace(/&/g,"&amp;").replace(/</g,"&lt;")
      .replace(/>/g,"&gt;").replace(/"/g,"&quot;");
  }
})();
