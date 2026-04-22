// ============================================================================
// AegisSentinel — Native Messaging Host
// Entry point: reads from Chrome's stdin pipe, dispatches to analysis engine,
// writes results back over stdout, and fires Windows Toast Notifications.
// ============================================================================

using System.Text;
using System.Text.Json;
using AegisSentinel.NativeHost.Analysis;
using AegisSentinel.NativeHost.Messaging;
using AegisSentinel.NativeHost.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AegisSentinel.NativeHost;

/// <summary>
/// Chrome Native Messaging protocol:
///   - stdin:  4-byte little-endian length  +  UTF-8 JSON payload
///   - stdout: 4-byte little-endian length  +  UTF-8 JSON response
///
/// The host is launched by Chrome on demand and exits when Chrome closes
/// the pipe (stdin returns 0 bytes on read).
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // ── Service wiring ──────────────────────────────────────────────────
        var services = BuildServices();
        var log      = services.GetRequiredService<ILogger<object>>();
        var pipe     = services.GetRequiredService<NativeMessagingPipe>();
        var engine   = services.GetRequiredService<PrivacyAnalysisEngine>();
        var notifier = services.GetRequiredService<ToastNotifier>();

        log.LogInformation("AegisSentinel Native Host started (PID {Pid})", Environment.ProcessId);

        // ── Main message loop ───────────────────────────────────────────────
        try
        {
            await foreach (var message in pipe.ReadMessagesAsync())
            {
                log.LogDebug("Received message: Type={Type} Url={Url}",
                    message.Type, message.Url);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleMessageAsync(message, engine, notifier, pipe, log);
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Error handling message for {Url}", message.Url);
                        await pipe.WriteAsync(NativeResponse.Error(message.RequestId,
                            $"Internal error: {ex.Message}"));
                    }
                });
            }
        }
        catch (EndOfStreamException)
        {
            log.LogInformation("Chrome closed the pipe — host exiting cleanly");
        }
        catch (Exception ex)
        {
            log.LogCritical(ex, "Fatal error in message loop");
            return 1;
        }

        return 0;
    }

    private static async Task HandleMessageAsync(
        NativeMessage        message,
        PrivacyAnalysisEngine engine,
        ToastNotifier        notifier,
        NativeMessagingPipe  pipe,
        ILogger              log)
    {
        switch (message.Type)
        {
            case "PRIVACY_POLICY_DETECTED":
                // 1. Acknowledge receipt immediately so Chrome doesn't timeout
                await pipe.WriteAsync(NativeResponse.Ack(message.RequestId));

                // 2. Run full analysis pipeline (HTML clean → LLM)
                log.LogInformation("Analysing privacy policy from {Url}", message.Url);
                var result = await engine.AnalyseAsync(message.Url!, message.HtmlContent!);

                // 3. Send rich result back to extension (for badge/popup)
                await pipe.WriteAsync(NativeResponse.Result(message.RequestId, result));

                // 4. Fire Windows Toast Notification
                await notifier.ShowPrivacyResultAsync(message.Url!, result);
                break;

            case "PING":
                await pipe.WriteAsync(NativeResponse.Pong(message.RequestId));
                break;

            default:
                log.LogWarning("Unknown message type: {Type}", message.Type);
                await pipe.WriteAsync(NativeResponse.Error(message.RequestId,
                    $"Unknown message type: {message.Type}"));
                break;
        }
    }

    // ── DI container ─────────────────────────────────────────────────────────
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));

        services.AddHttpClient();
        services.AddSingleton<NativeMessagingPipe>();
        services.AddSingleton<HtmlCleaner>();
        services.AddSingleton<OpenAiClient>();
        services.AddSingleton<PrivacyAnalysisEngine>();
        services.AddSingleton<ToastNotifier>();

        return services.BuildServiceProvider();
    }
}
