// ============================================================================
// NativeBridgeWindow.xaml.cs
//
// Optional WPF companion window that connects to the named pipe server
// (or reads from a shared PrivacyResultStore) to display a live feed of
// every privacy policy analysed by Chrome. This bridges the existing
// AegisSentinel WPF shell with the new Native Messaging results.
//
// Architecture:
//   NativeHost.exe ──[PrivacyResultStore (static/thread-safe)]──► WPF Window
//
// Because the Native Host is a headless console process that Chrome spawns,
// it cannot own WPF windows directly. The WPF app polls/subscribes through
// a shared in-process store when both components run in the same AppDomain,
// OR through a named-pipe IPC channel when deployed as separate processes.
// ============================================================================

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AegisSentinel.NativeHost.Messaging;

namespace AegisSentinel.UI.Views;

// ── View model for a history feed item ────────────────────────────────────────
public sealed class PrivacyFeedItem
{
    public string       Domain       { get; init; } = "";
    public string       FullUrl      { get; init; } = "";
    public int          SafetyPercent { get; init; }
    public string       RiskLevel    { get; init; } = "";
    public bool         DataSelling  { get; init; }
    public string       Retention    { get; init; } = "";
    public List<string> KeyRisks     { get; init; } = new();
    public string       Verdict      { get; init; } = "";
    public DateTime     AnalysedAt   { get; init; } = DateTime.Now;
}

// ── Thread-safe static store — NativeHost writes, WPF reads ──────────────────
public static class PrivacyResultStore
{
    public static event EventHandler<PrivacyFeedItem>? ResultReceived;

    public static void Publish(string url, PrivacyScore score)
    {
        var item = new PrivacyFeedItem
        {
            Domain        = TryExtractDomain(url),
            FullUrl       = url,
            SafetyPercent = score.SafetyPercent,
            RiskLevel     = score.RiskLevel,
            DataSelling   = score.DataSellingDetected,
            Retention     = score.RetentionPeriod,
            KeyRisks      = score.KeyRisks,
            Verdict       = score.Verdict,
            AnalysedAt    = DateTime.Now
        };
        ResultReceived?.Invoke(null, item);
    }

    private static string TryExtractDomain(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url.Length > 40 ? url[..40] + "…" : url; }
    }
}

// ── Window code-behind ────────────────────────────────────────────────────────
public partial class NativeBridgeWindow : Window
{
    private readonly ObservableCollection<PrivacyFeedItem> _feed = new();
    private readonly DispatcherTimer _connectionPoller;
    private int _analysisCount;

    // Colour map matching the risk level strings from the LLM
    private static readonly Dictionary<string, Color> RiskColors = new()
    {
        ["Safe"]    = Color.FromRgb(0x10, 0xB9, 0x81),  // emerald
        ["Caution"] = Color.FromRgb(0xFB, 0xBF, 0x24),  // amber
        ["Warning"] = Color.FromRgb(0xF9, 0x73, 0x16),  // orange
        ["Danger"]  = Color.FromRgb(0xEF, 0x44, 0x44),  // red
    };

    public NativeBridgeWindow()
    {
        InitializeComponent();

        HistoryList.ItemsSource = _feed;

        // Subscribe to results published by the native host (in-process)
        PrivacyResultStore.ResultReceived += OnResultReceived;

        // Pulse the connection dot every 3 s to simulate "live" state
        _connectionPoller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _connectionPoller.Tick += PulseConnectionDot;
        _connectionPoller.Start();
    }

    // ── Incoming result ──────────────────────────────────────────────────────
    private void OnResultReceived(object? sender, PrivacyFeedItem item)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _analysisCount++;

            // Insert at the top of the history feed
            _feed.Insert(0, item);

            // Trim feed to 50 items
            while (_feed.Count > 50) _feed.RemoveAt(_feed.Count - 1);

            // Update hero card
            UpdateHeroCard(item);

            StatusText.Text =
                $"Analysis #{_analysisCount} complete · " +
                $"{DateTime.Now:HH:mm:ss}";
        });
    }

    // ── Hero card rendering ──────────────────────────────────────────────────
    private void UpdateHeroCard(PrivacyFeedItem item)
    {
        var color = RiskColors.GetValueOrDefault(item.RiskLevel, Color.FromRgb(0x47, 0x55, 0x69));

        // Animate score number
        AnimateTextSwap(ScoreNumber, item.SafetyPercent.ToString());
        HeroTitle.Text    = item.Domain;
        HeroDomain.Text   = $"$ {item.FullUrl.Length > 55 ? item.FullUrl[..55] + "…" : item.FullUrl}";
        RetentionText.Text = $"🕐 Retention: {item.Retention}";

        // Score bar animation
        var targetWidth = (item.SafetyPercent / 100.0) * (HeroCard.ActualWidth - 32);
        var widthAnim   = new DoubleAnimation(ScoreBar.Width, Math.Max(targetWidth, 4),
            new Duration(TimeSpan.FromMilliseconds(600)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ScoreBar.BeginAnimation(WidthProperty, widthAnim);

        // Colour transitions
        AnimateColor(ScoreBarColor,      color);
        AnimateColor(ScoreCircleColor,   color);
        AnimateColor(ScoreNumberColor,   color);

        // Risk badge
        RiskBadgeText.Text = item.RiskLevel.ToUpperInvariant();
        RiskBadgeText.Foreground = new SolidColorBrush(color);
        RiskBadge.BorderBrush    = new SolidColorBrush(Color.FromArgb(0x40,
            color.R, color.G, color.B));
        RiskBadge.Background = new SolidColorBrush(Color.FromArgb(0x20,
            color.R, color.G, color.B));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static void AnimateColor(SolidColorBrush brush, Color to)
    {
        var anim = new ColorAnimation(to, new Duration(TimeSpan.FromMilliseconds(400)))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
    }

    private static void AnimateTextSwap(System.Windows.Controls.TextBlock tb, string newText)
    {
        var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(120)));
        fadeOut.Completed += (_, _) =>
        {
            tb.Text = newText;
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
            tb.BeginAnimation(OpacityProperty, fadeIn);
        };
        tb.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void PulseConnectionDot(object? sender, EventArgs e)
    {
        var pulse = new ColorAnimation(
            Color.FromRgb(0x10, 0xB9, 0x81),
            Color.FromRgb(0x06, 0x4E, 0x3B),
            new Duration(TimeSpan.FromMilliseconds(400)))
        {
            AutoReverse    = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        ConnectionDot.BeginAnimation(SolidColorBrush.ColorProperty, pulse);
    }

    // ── History item click → open AuditResultWindow ──────────────────────────
    private void HistoryItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as System.Windows.FrameworkElement)?.Tag is PrivacyFeedItem item)
            ShowDetailWindow(item);
    }

    private void ShowDetailWindow(PrivacyFeedItem item)
    {
        var detail = new PrivacyDetailWindow(item);
        detail.Owner = this;
        detail.ShowDialog();
    }

    // ── Title bar ─────────────────────────────────────────────────────────────
    private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void Minimise_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
        => Close();

    protected override void OnClosed(EventArgs e)
    {
        PrivacyResultStore.ResultReceived -= OnResultReceived;
        _connectionPoller.Stop();
        base.OnClosed(e);
    }
}
