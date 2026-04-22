// ============================================================================
// App.xaml.cs — INTEGRATION PATCH
//
// This file shows the DIFF / additions needed to wire the new Native
// Messaging pipeline into your existing AegisSentinel WPF application.
//
// Your existing App.xaml.cs already handles:
//   - Gemini AI analysis of EULA windows (WindowObserver / AutomationService)
//   - DashboardWindow, AuditResultWindow, OverlayView
//
// This patch adds:
//   - NativeBridgeWindow  (shows live Chrome privacy policy results)
//   - PrivacyResultStore  (static event bus from NativeHost → WPF)
//   - System tray icon with right-click menu
// ============================================================================

using System.Windows;
using System.Windows.Forms;     // For NotifyIcon (add System.Windows.Forms ref)
using AegisSentinel.Core.Interfaces;
using AegisSentinel.Core.Services;
using AegisSentinel.Infrastructure.AI;
using AegisSentinel.Infrastructure.Automation;
using AegisSentinel.Infrastructure.Config;
using AegisSentinel.NativeHost.Messaging;  // PrivacyResultStore
using AegisSentinel.UI.ViewModels;
using AegisSentinel.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AegisSentinel.UI;

public partial class App : System.Windows.Application
{
    private IServiceProvider _services = null!;
    private NotifyIcon?      _trayIcon;
    private NativeBridgeWindow? _bridgeWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Hide main window — this is a tray app
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _services = BuildServices();

        // ── 1. Start legacy EULA watcher (your existing code) ────────────
        var orchestrator = _services.GetRequiredService<IAuditOrchestrator>();
        ((AuditOrchestrator)orchestrator).Start();

        // ── 2. Show dashboard (existing) ─────────────────────────────────
        var dashboard = _services.GetRequiredService<DashboardWindow>();
        dashboard.Show();

        // ── 3. Open Chrome Native Bridge window ──────────────────────────
        _bridgeWindow = new NativeBridgeWindow();
        _bridgeWindow.Show();

        // ── 4. System tray icon ───────────────────────────────────────────
        InitialiseTrayIcon();
    }

    // ── Tray icon setup ───────────────────────────────────────────────────────
    private void InitialiseTrayIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "shield.ico");

        _trayIcon = new NotifyIcon
        {
            Text    = "AegisSentinel — Privacy Guardian",
            Visible = true
        };

        if (System.IO.File.Exists(iconPath))
            _trayIcon.Icon = new System.Drawing.Icon(iconPath);

        var menu = new ContextMenuStrip();

        menu.Items.Add("Privacy Monitor",  null, (_, _) =>
        {
            if (_bridgeWindow is { IsVisible: false })
            {
                _bridgeWindow = new NativeBridgeWindow();
                _bridgeWindow.Show();
            }
            else _bridgeWindow?.Activate();
        });

        menu.Items.Add("EULA Dashboard",   null, (_, _) =>
            _services.GetRequiredService<DashboardWindow>().Show());

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            Shutdown();
        });

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => _bridgeWindow?.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        (_services.GetService<IAuditOrchestrator>() as AuditOrchestrator)?.Stop();
        (_services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    // ── DI container (extends your existing BuildServices) ────────────────────
    private static IServiceProvider BuildServices()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables(prefix: "AEGIS_")
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(b => b
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        // Config
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IAppConfiguration, AppConfiguration>();

        // Infrastructure (existing)
        services.AddHttpClient<IAiAuditService, GeminiClient>();
        services.AddSingleton<IWindowObserver,   WindowObserver>();
        services.AddSingleton<IAutomationService, AutomationService>();

        // Core (existing)
        services.AddSingleton<IAuditOrchestrator, AuditOrchestrator>();

        // UI (existing)
        services.AddSingleton<DashboardViewModel>();
        services.AddTransient<DashboardWindow>();
        services.AddTransient<AuditResultWindow>();
        services.AddTransient<OverlayView>();

        return services.BuildServiceProvider();
    }
}
