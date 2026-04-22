// ============================================================================
// PrivacyAnalysisEngine.cs
// Orchestrates the full pipeline:
//   1. HTML cleaning (HtmlAgilityPack)
//   2. LLM analysis  (OpenAI GPT-4o-mini)
//   3. Returns structured PrivacyScore
// ============================================================================

using AegisSentinel.NativeHost.Messaging;
using Microsoft.Extensions.Logging;

namespace AegisSentinel.NativeHost.Analysis;

public sealed class PrivacyAnalysisEngine
{
    private readonly HtmlCleaner            _cleaner;
    private readonly OpenAiClient           _ai;
    private readonly ILogger<PrivacyAnalysisEngine> _log;

    // Simple in-process LRU cache: don't re-analyse the same URL twice
    private readonly Dictionary<string, (PrivacyScore Score, DateTime At)> _cache = new();
    private const int    CacheSize    = 50;
    private readonly TimeSpan CacheTtl = TimeSpan.FromHours(4);
    private readonly Lock _cacheLock   = new();

    public PrivacyAnalysisEngine(
        HtmlCleaner cleaner,
        OpenAiClient ai,
        ILogger<PrivacyAnalysisEngine> log)
    {
        _cleaner = cleaner;
        _ai      = ai;
        _log     = log;
    }

    public async Task<PrivacyScore> AnalyseAsync(
        string url,
        string rawHtml,
        CancellationToken ct = default)
    {
        // ── Cache check ───────────────────────────────────────────────────────
        var cacheKey = NormalisedCacheKey(url);
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached)
                && DateTime.UtcNow - cached.At < CacheTtl)
            {
                _log.LogInformation("Cache hit for {Url}", url);
                return cached.Score;
            }
        }

        // ── Stage 1: Clean HTML ───────────────────────────────────────────────
        _log.LogInformation("Cleaning HTML for {Url} ({Len} chars)", url, rawHtml.Length);
        var cleanText = _cleaner.Clean(rawHtml);

        if (cleanText.Length < 100)
        {
            _log.LogWarning("Cleaned text too short ({Len} chars) for {Url} — skipping LLM", cleanText.Length, url);
            return PrivacyScore.ParseFailed(url);
        }

        // ── Stage 2: LLM Analysis ─────────────────────────────────────────────
        _log.LogInformation("Sending {Chars} chars to OpenAI for {Url}", cleanText.Length, url);
        var score = await _ai.AnalyseAsync(cleanText, ct);

        _log.LogInformation(
            "Analysis complete for {Url}: Safety={Safety}% Risk={Risk} DataSelling={Selling}",
            url, score.SafetyPercent, score.RiskLevel, score.DataSellingDetected);

        // ── Cache result ──────────────────────────────────────────────────────
        lock (_cacheLock)
        {
            if (_cache.Count >= CacheSize)
            {
                // Evict oldest entry
                var oldest = _cache.MinBy(kvp => kvp.Value.At).Key;
                _cache.Remove(oldest);
            }
            _cache[cacheKey] = (score, DateTime.UtcNow);
        }

        return score;
    }

    private static string NormalisedCacheKey(string url)
    {
        try
        {
            var uri = new Uri(url);
            // Normalise to scheme + host + path (ignore query params like tracking IDs)
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}".ToLowerInvariant().TrimEnd('/');
        }
        catch
        {
            return url.ToLowerInvariant();
        }
    }
}
