// ============================================================================
// ToastNotifier.cs
// Fires a rich Windows 10/11 Toast Notification displaying:
//   • Privacy Score (%)
//   • Key Risks (bullet points)
//   • Status badge (Green / Yellow / Red)
//
// Uses Microsoft.Toolkit.Uwp.Notifications (now Community Toolkit) for
// the builder API. Add via:
//   dotnet add package Microsoft.Toolkit.Uwp.Notifications
//
// IMPORTANT: The host process must call ToastNotificationManagerCompat
// .Uninstall() on exit if it is not a packaged MSIX app, to clean up
// COM activator registration.
// ============================================================================

using AegisSentinel.NativeHost.Messaging;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Extensions.Logging;
using Windows.UI.Notifications;

namespace AegisSentinel.NativeHost.Notifications;

public sealed class ToastNotifier
{
    private readonly ILogger<ToastNotifier> _log;
    private const string AppId = "com.aegissentinel.host"; // must match AUMID in registry

    public ToastNotifier(ILogger<ToastNotifier> log) => _log = log;

    /// <summary>
    /// Shows a Windows Toast notification with the privacy analysis result.
    /// Fires and forgets — does not block the message loop.
    /// </summary>
    public Task ShowPrivacyResultAsync(string url, PrivacyScore score)
    {
        try
        {
            Show(url, score);
        }
        catch (Exception ex)
        {
            // Toasts can fail in some server/session-0 contexts — not fatal
            _log.LogWarning(ex, "Toast notification failed for {Url}", url);
        }
        return Task.CompletedTask;
    }

    private void Show(string url, PrivacyScore score)
    {
        var domain   = ExtractDomain(url);
        var headline = $"{score.StatusEmoji}  {domain}  —  {score.SafetyPercent}% Safe";
        var subtext  = score.DataSellingDetected
            ? "⚠️  Data selling detected"
            : "✅  No data selling detected";

        // ── Build the adaptive toast ──────────────────────────────────────────
        var builder = new ToastContentBuilder()
            .SetToastScenario(ToastScenario.Default)
            .AddAppLogoOverride(GetIconUri(), ToastGenericAppLogoCrop.Circle)
            .AddHeader(
                id:    Guid.NewGuid().ToString(),
                title: "AegisSentinel Privacy Report",
                arguments: $"action=openDashboard&url={Uri.EscapeDataString(url)}")

            // ── Title row ─────────────────────────────────────────────────────
            .AddText(headline,
                hintMaxLines: 1,
                hintStyle: AdaptiveTextStyle.Base)

            // ── Score + retention ─────────────────────────────────────────────
            .AddText(
                $"{score.StatusBadge}   Retention: {score.RetentionPeriod}",
                hintStyle: AdaptiveTextStyle.Caption)

            // ── Data selling flag ─────────────────────────────────────────────
            .AddText(subtext, hintStyle: AdaptiveTextStyle.Caption);

        // ── Key risks (up to 3 shown in notification; full list in popup) ─────
        var topRisks = score.KeyRisks.Take(3).ToList();
        if (topRisks.Count > 0)
        {
            builder.AddText("Key concerns:", hintStyle: AdaptiveTextStyle.CaptionSubtle);
            foreach (var risk in topRisks)
                builder.AddText($"  • {risk}", hintStyle: AdaptiveTextStyle.Caption);
        }

        // ── Verdict ───────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(score.Verdict))
            builder.AddText(TruncateVerdict(score.Verdict), hintStyle: AdaptiveTextStyle.CaptionSubtle);

        // ── Action buttons ────────────────────────────────────────────────────
        builder
            .AddButton(new ToastButton()
                .SetContent("View Full Report")
                .AddArgument("action", "viewReport")
                .AddArgument("url",    url))
            .AddButton(new ToastButton()
                .SetContent("Dismiss")
                .AddArgument("action", "dismiss")
                .SetDismissActivation());

        // ── Toast attribution tag (deduplication) ─────────────────────────────
        var tag   = $"aegis-{ComputeShortHash(url)}";
        var group = "privacy-reports";

        // ── Show (using Windows App SDK / WinRT interop) ──────────────────────
        builder.Show(toast =>
        {
            toast.Tag    = tag[..Math.Min(tag.Length, 64)];
            toast.Group  = group;
            toast.ExpiresOnReboot = false;
        });

        _log.LogInformation(
            "Toast shown for {Domain}: {Safety}% safe, risk={Risk}",
            domain, score.SafetyPercent, score.RiskLevel);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string ExtractDomain(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url.Length > 50 ? url[..50] + "…" : url; }
    }

    private static string TruncateVerdict(string verdict, int max = 120)
        => verdict.Length > max ? verdict[..max].TrimEnd() + "…" : verdict;

    private static string ComputeShortHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private static Uri GetIconUri()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "shield.png");
        return File.Exists(iconPath)
            ? new Uri(iconPath)
            : new Uri("https://raw.githubusercontent.com/aegissentinel/assets/main/shield.png");
    }
}
