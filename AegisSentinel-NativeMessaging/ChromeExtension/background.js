// ============================================================================
// background.js — Service Worker (Manifest V3)
// Responsibilities:
//   1. Maintain a single persistent Native Messaging port to the C# host.
//   2. Receive "PRIVACY_POLICY_DETECTED" messages from content.js.
//   3. Forward them to the C# host with the page HTML content.
//   4. Receive analysis results and store them in chrome.storage.local.
//   5. Update the extension badge with the safety score.
// ============================================================================

const NATIVE_HOST_ID = "com.aegissentinel.host";

// ── Native Messaging port (persistent) ───────────────────────────────────────
let nativePort = null;

/**
 * Returns the active native port, reconnecting if necessary.
 * The port is shared across all tab messages to avoid spawning
 * a new C# host process per-page.
 */
function getNativePort() {
  if (nativePort) return nativePort;

  console.log("[AegisSentinel] Connecting to native host:", NATIVE_HOST_ID);
  nativePort = chrome.runtime.connectNative(NATIVE_HOST_ID);

  nativePort.onMessage.addListener(handleNativeResponse);

  nativePort.onDisconnect.addListener(() => {
    const err = chrome.runtime.lastError;
    console.warn("[AegisSentinel] Native port disconnected:", err?.message);
    nativePort = null;
    // Will reconnect on next message
  });

  return nativePort;
}

// ── Pending request map: requestId → { tabId, resolve } ──────────────────────
const pendingRequests = new Map();

// ── Message from content.js ───────────────────────────────────────────────────
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === "PRIVACY_POLICY_DETECTED") {
    handlePrivacyPolicyDetected(message, sender.tab?.id)
      .then(result => sendResponse({ ok: true, result }))
      .catch(err  => sendResponse({ ok: false, error: err.message }));
    return true; // Keep sendResponse alive (async)
  }

  if (message.type === "GET_RESULT") {
    chrome.storage.local.get(["lastResult"], data => {
      sendResponse(data.lastResult ?? null);
    });
    return true;
  }
});

// ── Core handler ──────────────────────────────────────────────────────────────
async function handlePrivacyPolicyDetected(message, tabId) {
  const requestId = crypto.randomUUID();

  // Store tab association so we can update the right badge on result
  const request = { tabId, url: message.url };

  const response = await sendToNativeHost({
    type:        "PRIVACY_POLICY_DETECTED",
    requestId,
    url:         message.url,
    title:       message.title,
    htmlContent: message.htmlContent
  });

  return response;
}

// ── Send to C# host with promise wrapper ─────────────────────────────────────
function sendToNativeHost(payload) {
  return new Promise((resolve, reject) => {
    const { requestId } = payload;
    pendingRequests.set(requestId, { resolve, reject });

    // Timeout: 60 seconds (LLM calls can be slow on large policies)
    const timer = setTimeout(() => {
      if (pendingRequests.delete(requestId)) {
        reject(new Error(`Timeout waiting for analysis of requestId=${requestId}`));
      }
    }, 60_000);

    pendingRequests.get(requestId).timer = timer;

    try {
      getNativePort().postMessage(payload);
    } catch (err) {
      clearTimeout(timer);
      pendingRequests.delete(requestId);
      reject(err);
    }
  });
}

// ── Responses from C# host ────────────────────────────────────────────────────
function handleNativeResponse(response) {
  const { type, requestId, result, error } = response;
  console.log("[AegisSentinel] Native response:", type, requestId);

  if (type === "ACK") {
    // Intermediate acknowledgement — keep waiting
    return;
  }

  if (type === "ANALYSIS_RESULT" && requestId) {
    const pending = pendingRequests.get(requestId);
    if (pending) {
      clearTimeout(pending.timer);
      pendingRequests.delete(requestId);

      // Persist result for popup and content script
      chrome.storage.local.set({ lastResult: result });

      // Update badge
      updateBadge(result, pending.tabId);

      pending.resolve(result);
    }
  }

  if (type === "ERROR" && requestId) {
    const pending = pendingRequests.get(requestId);
    if (pending) {
      clearTimeout(pending.timer);
      pendingRequests.delete(requestId);
      pending.reject(new Error(error ?? "Unknown native host error"));
    }
  }
}

// ── Badge update ──────────────────────────────────────────────────────────────
function updateBadge(result, tabId) {
  if (!result) return;

  const { safety_percent: pct, risk_level: level } = result;

  const colors = {
    Safe:    [16,  185, 129, 255],   // green
    Caution: [251, 191,  36, 255],   // yellow
    Warning: [249, 115,  22, 255],   // orange
    Danger:  [239,  68,  68, 255],   // red
  };

  const badgeColor = colors[level] ?? [156, 163, 175, 255]; // grey fallback
  const badgeText  = level === "Safe" ? "✓" : `${pct}`;

  const tabOpts = tabId ? { tabId } : {};

  chrome.action.setBadgeText({ text: badgeText, ...tabOpts });
  chrome.action.setBadgeBackgroundColor({ color: badgeColor, ...tabOpts });
  chrome.action.setTitle({
    title: `AegisSentinel: ${pct}% safe — ${level}`,
    ...tabOpts
  });
}
