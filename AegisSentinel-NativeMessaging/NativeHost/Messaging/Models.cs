// ============================================================================
// Models.cs — Privacy scoring domain models
// Shared between Analysis, Notification, and Chrome response serialisation.
// ============================================================================

using System.Text.Json.Serialization;

namespace AegisSentinel.NativeHost.Messaging;

/// <summary>
/// The structured JSON object returned by the LLM and surfaced in:
///   1. The Chrome extension popup
///   2. The Windows Toast Notification
/// Fields match the strict prompt schema so no mapping layer is needed.
/// </summary>
public sealed class PrivacyScore
{
    [JsonPropertyName("safety_percent")]
    public int SafetyPercent { get; init; }           // 0–100 (higher = safer)

    [JsonPropertyName("risk_level")]
    public string RiskLevel { get; init; } = "Unknown"; // "Safe" | "Caution" | "Warning" | "Danger"

    [JsonPropertyName("data_selling_detected")]
    public bool DataSellingDetected { get; init; }

    [JsonPropertyName("retention_period")]
    public string RetentionPeriod { get; init; } = "Unknown";

    [JsonPropertyName("key_risks")]
    public List<string> KeyRisks { get; init; } = new();

    [JsonPropertyName("verdict")]
    public string Verdict { get; init; } = string.Empty;

    // ── Derived helpers (not serialised) ─────────────────────────────────
    [JsonIgnore]
    public string StatusBadge => RiskLevel switch
    {
        "Safe"    => "🟢 SAFE",
        "Caution" => "🟡 CAUTION",
        "Warning" => "🟠 WARNING",
        "Danger"  => "🔴 DANGER",
        _         => "⚪ UNKNOWN"
    };

    [JsonIgnore]
    public string StatusEmoji => RiskLevel switch
    {
        "Safe"    => "✅",
        "Caution" => "⚠️",
        "Warning" => "🚨",
        _         => "🛑"
    };

    /// <summary>Fallback object used when LLM parsing fails.</summary>
    public static PrivacyScore ParseFailed(string url) => new()
    {
        SafetyPercent       = 50,
        RiskLevel           = "Caution",
        DataSellingDetected = false,
        RetentionPeriod     = "Unknown",
        Verdict             = "Analysis could not be completed. Review policy manually.",
        KeyRisks            = new List<string> { "Automated analysis failed — manual review required" }
    };
}
