// ============================================================================
// HtmlCleaner.cs
// Strips scripts, styles, nav, footer, and cookie banners from raw HTML,
// leaving only semantic text nodes suitable for LLM analysis.
// Uses HtmlAgilityPack (MIT) — add via:  dotnet add package HtmlAgilityPack
// ============================================================================

using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace AegisSentinel.NativeHost.Analysis;

public sealed class HtmlCleaner
{
    private readonly ILogger<HtmlCleaner> _log;

    // Tags whose entire subtree we discard
    private static readonly HashSet<string> _stripTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "svg", "math",
        "header", "nav", "footer", "aside", "iframe",
        "button", "form", "input", "select", "textarea",
        "img", "picture", "video", "audio", "canvas",
        "meta", "link", "base"
    };

    // Aria roles whose entire subtree we discard
    private static readonly HashSet<string> _stripRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "navigation", "banner", "complementary", "contentinfo"
    };

    public HtmlCleaner(ILogger<HtmlCleaner> log) => _log = log;

    /// <summary>
    /// Converts raw HTML into clean prose text, truncated to
    /// <paramref name="maxChars"/> characters to fit LLM context windows.
    /// </summary>
    public string Clean(string rawHtml, int maxChars = 100_000)
    {
        if (string.IsNullOrWhiteSpace(rawHtml))
            return string.Empty;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(rawHtml);

            // Remove unwanted elements in one pass
            RemoveNodes(doc.DocumentNode);

            // Extract text
            var text = ExtractText(doc.DocumentNode);

            // Collapse whitespace
            text = NormaliseWhitespace(text);

            if (text.Length > maxChars)
            {
                _log.LogDebug("Truncating cleaned text from {Full} to {Max} chars", text.Length, maxChars);
                text = text[..maxChars] + "\n[... CONTENT TRUNCATED ...]";
            }

            _log.LogInformation("HTML cleaned: {OrigLen} → {CleanLen} chars", rawHtml.Length, text.Length);
            return text;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "HTML cleaning failed — falling back to regex strip");
            return FallbackStrip(rawHtml, maxChars);
        }
    }

    // ── Node removal ──────────────────────────────────────────────────────────
    private static void RemoveNodes(HtmlNode root)
    {
        var toRemove = new List<HtmlNode>();

        foreach (var node in root.DescendantsAndSelf())
        {
            if (node.NodeType != HtmlNodeType.Element) continue;

            var tag  = node.Name.ToLowerInvariant();
            var role = node.GetAttributeValue("role", string.Empty);

            if (_stripTags.Contains(tag) || _stripRoles.Contains(role))
                toRemove.Add(node);
        }

        foreach (var node in toRemove)
            node.Remove();
    }

    // ── Text extraction ───────────────────────────────────────────────────────
    private static string ExtractText(HtmlNode root)
    {
        var sb = new StringBuilder(4096);

        foreach (var node in root.DescendantsAndSelf())
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = HtmlEntity.DeEntitize(node.InnerText);
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text.Trim());
            }
        }

        return sb.ToString();
    }

    // ── Whitespace normalisation ──────────────────────────────────────────────
    private static string NormaliseWhitespace(string text)
    {
        // Collapse 3+ blank lines to 2
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        // Collapse horizontal whitespace (tabs, multiple spaces)
        text = Regex.Replace(text, @"[^\S\n]+", " ");
        return text.Trim();
    }

    // ── Fallback: regex-only strip ────────────────────────────────────────────
    private static string FallbackStrip(string html, int maxChars)
    {
        // Remove tags
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = HtmlEntity.DeEntitize(text);
        text = NormaliseWhitespace(text);
        return text.Length > maxChars ? text[..maxChars] : text;
    }
}
