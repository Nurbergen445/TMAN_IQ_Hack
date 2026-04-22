// ============================================================================
// NativeMessagingPipe.cs
// Implements the Chrome Native Messaging binary framing protocol:
//   READ:  4-byte LE uint32 length  →  UTF-8 JSON body
//   WRITE: 4-byte LE uint32 length  →  UTF-8 JSON body
//
// CRITICAL: Console stdin/stdout MUST be treated as raw binary streams.
// Do NOT use Console.ReadLine() — it strips the length header bytes.
// Do NOT set Console.OutputEncoding — it can corrupt binary output.
// ============================================================================

using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AegisSentinel.NativeHost.Messaging;

public sealed class NativeMessagingPipe
{
    private readonly Stream               _input;
    private readonly Stream               _output;
    private readonly SemaphoreSlim        _writeLock = new(1, 1);
    private readonly ILogger<NativeMessagingPipe> _log;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false
    };

    public NativeMessagingPipe(ILogger<NativeMessagingPipe> log)
    {
        _log = log;

        // Open stdin/stdout as raw binary streams — critical for correct framing
        _input  = Console.OpenStandardInput();
        _output = Console.OpenStandardOutput();
    }

    // ── Reading ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Yields deserialized <see cref="NativeMessage"/> objects from stdin
    /// until Chrome closes the pipe (EOF).
    /// </summary>
    public async IAsyncEnumerable<NativeMessage> ReadMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var lengthBuf = new byte[4];

        while (!ct.IsCancellationRequested)
        {
            // Read 4-byte length header
            int read = await ReadExactAsync(_input, lengthBuf, 0, 4, ct);
            if (read == 0) yield break; // EOF — Chrome closed the pipe

            uint length = BitConverter.ToUInt32(lengthBuf, 0); // little-endian on all platforms

            if (length == 0 || length > 10 * 1024 * 1024) // sanity: reject > 10 MB messages
            {
                _log.LogWarning("Invalid message length {Len} — skipping", length);
                continue;
            }

            // Read payload
            var payload = ArrayPool<byte>.Shared.Rent((int)length);
            try
            {
                await ReadExactAsync(_input, payload, 0, (int)length, ct);
                var json = Encoding.UTF8.GetString(payload, 0, (int)length);
                _log.LogDebug("← {Json}", json.Length > 200 ? json[..200] + "…" : json);

                var msg = JsonSerializer.Deserialize<NativeMessage>(json, _jsonOpts);
                if (msg is not null)
                    yield return msg;
                else
                    _log.LogWarning("Failed to deserialise message: {Json}", json[..Math.Min(json.Length, 500)]);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(payload);
            }
        }
    }

    // ── Writing ───────────────────────────────────────────────────────────────
    /// <summary>Thread-safe write with the 4-byte length prefix.</summary>
    public async Task WriteAsync<T>(T response, CancellationToken ct = default)
    {
        var json    = JsonSerializer.Serialize(response, _jsonOpts);
        var payload = Encoding.UTF8.GetBytes(json);
        var length  = BitConverter.GetBytes((uint)payload.Length); // 4-byte LE

        _log.LogDebug("→ {Json}", json.Length > 200 ? json[..200] + "…" : json);

        await _writeLock.WaitAsync(ct);
        try
        {
            await _output.WriteAsync(length, ct);
            await _output.WriteAsync(payload, ct);
            await _output.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static async Task<int> ReadExactAsync(
        Stream stream, byte[] buffer, int offset, int count,
        CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
            if (read == 0) return totalRead; // EOF
            totalRead += read;
        }
        return totalRead;
    }
}

// ── Message DTOs ──────────────────────────────────────────────────────────────
public sealed record NativeMessage
{
    [JsonPropertyName("type")]        public string? Type       { get; init; }
    [JsonPropertyName("requestId")]   public string? RequestId  { get; init; }
    [JsonPropertyName("url")]         public string? Url        { get; init; }
    [JsonPropertyName("htmlContent")] public string? HtmlContent { get; init; }
    [JsonPropertyName("title")]       public string? Title      { get; init; }
}

public sealed record NativeResponse
{
    [JsonPropertyName("type")]       public string?      Type      { get; init; }
    [JsonPropertyName("requestId")] public string?      RequestId { get; init; }
    [JsonPropertyName("status")]    public string?      Status    { get; init; }
    [JsonPropertyName("error")]     public string?      Error     { get; init; }
    [JsonPropertyName("result")]    public PrivacyScore? Result    { get; init; }

    public static NativeResponse Ack(string? requestId) => new()
    {
        Type = "ACK", RequestId = requestId, Status = "processing"
    };

    public static NativeResponse Pong(string? requestId) => new()
    {
        Type = "PONG", RequestId = requestId, Status = "ok"
    };

    public static NativeResponse Result(string? requestId, PrivacyScore score) => new()
    {
        Type = "ANALYSIS_RESULT", RequestId = requestId, Status = "complete", Result = score
    };

    public static NativeResponse Error(string? requestId, string msg) => new()
    {
        Type = "ERROR", RequestId = requestId, Status = "error", Error = msg
    };
}
