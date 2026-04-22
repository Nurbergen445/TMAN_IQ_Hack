// ============================================================================
// OpenAiClient.cs
// Sends cleaned privacy-policy text to OpenAI GPT-4o-mini and parses the
// strict JSON response into a PrivacyScore object.
//
// API key resolution order:
//   1. Environment variable: AEGIS_OPENAI_API_KEY
//   2. appsettings.json:     OpenAI:ApiKey
// ============================================================================

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AegisSentinel.NativeHost.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AegisSentinel.NativeHost.Analysis;

public sealed class OpenAiClient
{
    private readonly HttpClient             _http;
    private readonly ILogger<OpenAiClient>  _log;
    private readonly IConfiguration?        _config;

    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string Model  = "gpt-4o-mini";          // cheap, fast, structured-output capable

    // ── Strict analysis prompt ────────────────────────────────────────────────
    // The model is instructed to output ONLY a JSON object matching the schema.
    // Temperature 0 + response_format json_object enforces determinism.
    private const string SystemPrompt = """
        You are a privacy policy risk analyst. Analyse the provided privacy policy text and
        respond ONLY with a JSON object — no markdown fences, no explanation, pure JSON.

        Required JSON schema (all fields mandatory):
        {
          "safety_percent": <integer 0-100, where 100 = fully safe, 0 = extremely dangerous>,
          "risk_level": "<Safe|Caution|Warning|Danger>",
          "data_selling_detected": <true|false>,
          "retention_period": "<human-readable string, e.g. '10 years', 'Indefinite', 'Until account deletion'>",
          "verdict": "<one concise paragraph: the single most important thing users need to know>",
          "key_risks": [
            "<specific risk 1 in plain English, max 12 words>",
            "<specific risk 2>",
            "<specific risk 3>",
            "<add more as needed, up to 8 total>"
          ]
        }

        Risk level rules:
          - Safe (80-100%):    Standard terms, no data selling, reasonable retention
          - Caution (60-79%):  Some data sharing, vague retention, minor dark patterns
          - Warning (40-59%):  Data selling, long retention, confusing opt-outs
          - Danger (0-39%):    Sells to data brokers, indefinite retention, no user rights

        key_risks examples (use these styles):
          - "Sells data to advertising networks"
          - "Retains data for 10 years after account deletion"
          - "Shares location data with 3rd parties"
          - "No option to request data deletion"
          - "Auto-renews subscription without clear notice"
          - "Arbitration clause waives class-action rights"
        """;

    public OpenAiClient(IHttpClientFactory factory, ILogger<OpenAiClient> log,
        IConfiguration? config = null)
    {
        _http   = factory.CreateClient("openai");
        _log    = log;
        _config = config;
    }

    public async Task<PrivacyScore> AnalyseAsync(
        string cleanedText,
        CancellationToken ct = default)
    {
        var apiKey = ResolveApiKey();

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        // Truncate to ~90k chars to stay inside gpt-4o-mini's 128k context
        if (cleanedText.Length > 90_000)
            cleanedText = cleanedText[..90_000] + "\n[... TRUNCATED ...]";

        var requestBody = new
        {
            model    = Model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = $"Analyse this privacy policy:\n\n{cleanedText}" }
            },
            temperature     = 0,          // deterministic output
            max_tokens      = 1024,
            response_format = new { type = "json_object" }  // enforces pure JSON output
        };

        _log.LogInformation("Calling OpenAI {Model} with {Chars} chars of policy text",
            Model, cleanedText.Length);

        using var resp = await _http.PostAsJsonAsync(ApiUrl, requestBody, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadAsStringAsync(ct);
        _log.LogDebug("OpenAI raw response: {Raw}", raw.Length > 500 ? raw[..500] + "…" : raw);

        return ParseResponse(raw);
    }

    // ── Response parsing ──────────────────────────────────────────────────────
    private PrivacyScore ParseResponse(string rawJson)
    {
        try
        {
            using var doc     = JsonDocument.Parse(rawJson);
            var contentText   = doc
                .RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?? throw new InvalidOperationException("Empty content in OpenAI response");

            // contentText should already be JSON (response_format=json_object)
            var score = JsonSerializer.Deserialize<PrivacyScore>(contentText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return score ?? PrivacyScore.ParseFailed("unknown");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to parse OpenAI response");
            return PrivacyScore.ParseFailed("unknown");
        }
    }

    // ── API key resolution ────────────────────────────────────────────────────
    private string ResolveApiKey()
    {
        var envKey = Environment.GetEnvironmentVariable("AEGIS_OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey)) return envKey;

        var cfgKey = _config?["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(cfgKey)) return cfgKey;

        throw new InvalidOperationException(
            "OpenAI API key not configured. " +
            "Set the AEGIS_OPENAI_API_KEY environment variable or add OpenAI:ApiKey to appsettings.json.");
    }
}

// ── OpenAI response DTOs (private) ───────────────────────────────────────────
file sealed record OpenAiResponse(
    [property: JsonPropertyName("choices")] OpenAiChoice[] Choices);

file sealed record OpenAiChoice(
    [property: JsonPropertyName("message")] OpenAiMessage Message);

file sealed record OpenAiMessage(
    [property: JsonPropertyName("content")] string Content);
