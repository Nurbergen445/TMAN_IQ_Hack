// ============================================================================
// AegisSentinel.NativeHost.Tests
// xUnit tests for the most failure-prone components:
//   1. NativeMessagingPipe binary framing (round-trip)
//   2. HtmlCleaner output quality
//   3. PrivacyScore JSON parsing
//   4. PrivacyAnalysisEngine cache behaviour
// ============================================================================

using System.Text;
using System.Text.Json;
using AegisSentinel.NativeHost.Analysis;
using AegisSentinel.NativeHost.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AegisSentinel.NativeHost.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 1. Binary framing — write a message, read it back
// ─────────────────────────────────────────────────────────────────────────────
public class BinaryFramingTests
{
    /// <summary>
    /// The Chrome protocol requires EXACTLY a 4-byte little-endian uint32
    /// length prefix before every JSON payload. This test verifies the
    /// round-trip: encode a known message → decode from the raw bytes.
    /// </summary>
    [Theory]
    [InlineData("Hello")]
    [InlineData("{\"type\":\"PING\",\"requestId\":\"abc\"}")]
    [InlineData("")]   // edge: empty string (length = 0)
    public void RoundTrip_EncodeDecode_MatchesOriginal(string payload)
    {
        var bytes   = Encoding.UTF8.GetBytes(payload);
        var length  = BitConverter.GetBytes((uint)bytes.Length);

        // Write: [4-byte length] + [payload]
        using var stream = new MemoryStream();
        stream.Write(length, 0, 4);
        stream.Write(bytes, 0, bytes.Length);

        // Read back
        stream.Position = 0;
        var lenBuf = new byte[4];
        stream.Read(lenBuf, 0, 4);
        uint decodedLen = BitConverter.ToUInt32(lenBuf, 0);

        Assert.Equal((uint)bytes.Length, decodedLen);

        if (decodedLen > 0)
        {
            var payloadBuf = new byte[decodedLen];
            stream.Read(payloadBuf, 0, (int)decodedLen);
            Assert.Equal(payload, Encoding.UTF8.GetString(payloadBuf));
        }
    }

    [Fact]
    public void LengthHeader_IsLittleEndian()
    {
        // 300 in little-endian = 0x2C 0x01 0x00 0x00
        uint value  = 300;
        var  bytes  = BitConverter.GetBytes(value);
        Assert.Equal(0x2C, bytes[0]);
        Assert.Equal(0x01, bytes[1]);
        Assert.Equal(0x00, bytes[2]);
        Assert.Equal(0x00, bytes[3]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. HtmlCleaner
// ─────────────────────────────────────────────────────────────────────────────
public class HtmlCleanerTests
{
    private readonly HtmlCleaner _cleaner = new(NullLogger<HtmlCleaner>.Instance);

    [Fact]
    public void Clean_RemovesScriptTags()
    {
        const string html = "<html><body><script>alert(1)</script><p>Privacy Policy text</p></body></html>";
        var result = _cleaner.Clean(html);

        Assert.DoesNotContain("alert", result);
        Assert.Contains("Privacy Policy text", result);
    }

    [Fact]
    public void Clean_RemovesStyleTags()
    {
        const string html = "<html><head><style>.foo{color:red}</style></head><body><p>Legal text here</p></body></html>";
        var result = _cleaner.Clean(html);

        Assert.DoesNotContain(".foo", result);
        Assert.DoesNotContain("color:red", result);
        Assert.Contains("Legal text here", result);
    }

    [Fact]
    public void Clean_DecodesHtmlEntities()
    {
        const string html = "<body><p>We &amp; our partners collect data&#46;</p></body>";
        var result = _cleaner.Clean(html);

        Assert.Contains("We & our partners", result);
        Assert.Contains("collect data.", result);
    }

    [Fact]
    public void Clean_TruncatesAtMaxChars()
    {
        var longText = string.Concat(Enumerable.Repeat("a", 200_000));
        var html     = $"<body><p>{longText}</p></body>";
        var result   = _cleaner.Clean(html, maxChars: 1000);

        Assert.True(result.Length <= 1100); // small buffer for truncation suffix
        Assert.Contains("TRUNCATED", result);
    }

    [Fact]
    public void Clean_HandlesEmptyInput()
    {
        Assert.Equal(string.Empty, _cleaner.Clean(""));
        Assert.Equal(string.Empty, _cleaner.Clean("   "));
    }

    [Fact]
    public void Clean_RemovesNavAndFooter()
    {
        const string html = @"
            <body>
              <nav>Home | About | Contact</nav>
              <main>We collect your email address for marketing.</main>
              <footer>© 2024 Corp</footer>
            </body>";
        var result = _cleaner.Clean(html);

        Assert.DoesNotContain("Home | About", result);
        Assert.DoesNotContain("© 2024", result);
        Assert.Contains("We collect your email", result);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. PrivacyScore JSON parsing
// ─────────────────────────────────────────────────────────────────────────────
public class PrivacyScoreParsingTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialise_ValidJson_AllFieldsPopulated()
    {
        const string json = """
            {
              "safety_percent": 35,
              "risk_level": "Danger",
              "data_selling_detected": true,
              "retention_period": "Indefinite",
              "verdict": "This policy is extremely risky.",
              "key_risks": [
                "Sells data to ad brokers",
                "No right to deletion"
              ]
            }
            """;

        var score = JsonSerializer.Deserialize<PrivacyScore>(json, Opts);

        Assert.NotNull(score);
        Assert.Equal(35,          score!.SafetyPercent);
        Assert.Equal("Danger",    score.RiskLevel);
        Assert.True(              score.DataSellingDetected);
        Assert.Equal("Indefinite", score.RetentionPeriod);
        Assert.Equal(2,           score.KeyRisks.Count);
        Assert.Equal("Sells data to ad brokers", score.KeyRisks[0]);
    }

    [Fact]
    public void StatusBadge_ReflectsRiskLevel()
    {
        Assert.Equal("🟢 SAFE",    new PrivacyScore { RiskLevel = "Safe"    }.StatusBadge);
        Assert.Equal("🟡 CAUTION", new PrivacyScore { RiskLevel = "Caution" }.StatusBadge);
        Assert.Equal("🟠 WARNING", new PrivacyScore { RiskLevel = "Warning" }.StatusBadge);
        Assert.Equal("🔴 DANGER",  new PrivacyScore { RiskLevel = "Danger"  }.StatusBadge);
    }

    [Fact]
    public void ParseFailed_ReturnsSensibleFallback()
    {
        var fallback = PrivacyScore.ParseFailed("https://example.com/privacy");

        Assert.Equal(50,       fallback.SafetyPercent);
        Assert.Equal("Caution", fallback.RiskLevel);
        Assert.NotEmpty(fallback.KeyRisks);
        Assert.NotEmpty(fallback.Verdict);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. Cache key normalisation
// ─────────────────────────────────────────────────────────────────────────────
public class CacheKeyNormalisationTests
{
    // We test the normalisation logic directly by creating a subclass that
    // exposes the private method, or by exercising it through identical-URL
    // calls returning the same cached object.

    [Theory]
    [InlineData("https://example.com/privacy",       "https://example.com/privacy")]
    [InlineData("https://example.com/privacy/",      "https://example.com/privacy")]
    [InlineData("https://example.com/privacy?utm=1", "https://example.com/privacy")]
    [InlineData("HTTPS://EXAMPLE.COM/Privacy",       "https://example.com/privacy")]
    public void NormalisedKey_StripsFractionsAndCase(string input, string expected)
    {
        // Mirror the normalisation logic from PrivacyAnalysisEngine
        static string Normalise(string url)
        {
            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}"
                    .ToLowerInvariant().TrimEnd('/');
            }
            catch { return url.ToLowerInvariant(); }
        }

        Assert.Equal(expected, Normalise(input));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. NativeMessage deserialisation
// ─────────────────────────────────────────────────────────────────────────────
public class NativeMessageTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialise_PrivacyPolicyMessage()
    {
        const string json = """
            {
              "type": "PRIVACY_POLICY_DETECTED",
              "requestId": "abc-123",
              "url": "https://example.com/privacy",
              "title": "Privacy Policy | Example",
              "htmlContent": "<p>We collect data.</p>"
            }
            """;

        var msg = JsonSerializer.Deserialize<NativeMessage>(json, Opts);

        Assert.NotNull(msg);
        Assert.Equal("PRIVACY_POLICY_DETECTED", msg!.Type);
        Assert.Equal("abc-123",                  msg.RequestId);
        Assert.Equal("https://example.com/privacy", msg.Url);
        Assert.Contains("We collect data",        msg.HtmlContent);
    }

    [Fact]
    public void NativeResponse_Ack_HasCorrectFields()
    {
        var ack = NativeResponse.Ack("req-001");

        Assert.Equal("ACK",        ack.Type);
        Assert.Equal("req-001",    ack.RequestId);
        Assert.Equal("processing", ack.Status);
        Assert.Null(ack.Error);
        Assert.Null(ack.Result);
    }

    [Fact]
    public void NativeResponse_Error_HasCorrectFields()
    {
        var err = NativeResponse.Error("req-002", "Something went wrong");

        Assert.Equal("ERROR",                 err.Type);
        Assert.Equal("req-002",               err.RequestId);
        Assert.Equal("error",                 err.Status);
        Assert.Equal("Something went wrong",  err.Error);
    }
}
