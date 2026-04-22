// ============================================================================
// PrivacyDetailWindow.cs
// Full detail popup when the user clicks a history item in NativeBridgeWindow.
// Shows score, verdict, all key risks, data selling flag, retention period.
// Pure code-behind (no XAML) — keeps the file self-contained for portability.
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AegisSentinel.UI.Views;

public sealed class PrivacyDetailWindow : Window
{
    private static readonly Dictionary<string, Color> RiskColors = new()
    {
        ["Safe"]    = Color.FromRgb(0x10, 0xB9, 0x81),
        ["Caution"] = Color.FromRgb(0xFB, 0xBF, 0x24),
        ["Warning"] = Color.FromRgb(0xF9, 0x73, 0x16),
        ["Danger"]  = Color.FromRgb(0xEF, 0x44, 0x44),
    };

    private static readonly Dictionary<string, string> RiskEmojis = new()
    {
        ["Safe"]    = "✅  SAFE",
        ["Caution"] = "⚠️  CAUTION",
        ["Warning"] = "🚨  WARNING",
        ["Danger"]  = "🛑  DANGER",
    };

    public PrivacyDetailWindow(PrivacyFeedItem item)
    {
        Width           = 460;
        Height          = 580;
        WindowStyle     = WindowStyle.None;
        AllowsTransparency = true;
        Background      = Brushes.Transparent;
        ResizeMode      = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var color  = RiskColors.GetValueOrDefault(item.RiskLevel, Color.FromRgb(0x47, 0x55, 0x69));
        var brush  = new SolidColorBrush(color);
        var accent = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B));

        Content = BuildUI(item, color, brush, accent);

        // Fade in
        Opacity = 0;
        Loaded += (_, _) =>
            BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180))));
    }

    private UIElement BuildUI(PrivacyFeedItem item, Color color,
        SolidColorBrush brush, SolidColorBrush accent)
    {
        var root = new Border
        {
            Margin       = new Thickness(8),
            CornerRadius = new CornerRadius(12),
            Background   = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
            Effect       = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 24, Opacity = 0.6,
                ShadowDepth = 4, Direction = 270,
                Color = Colors.Black
            }
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Child = grid;

        // ── Title bar ─────────────────────────────────────────────────────
        var titleBar = new Border
        {
            Background   = new SolidColorBrush(Color.FromRgb(0x0A, 0x0F, 0x1E)),
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            Cursor       = Cursors.SizeAll
        };
        titleBar.MouseLeftButtonDown += (_, _) => DragMove();

        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition());
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var shieldIcon = new TextBlock
        {
            Text = "🛡", FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 0)
        };
        Grid.SetColumn(shieldIcon, 0);

        var titleText = new TextBlock
        {
            Text = item.Domain,
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(titleText, 1);

        var closeBtn = new Button
        {
            Content = "✕", Width = 32, Height = 32,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
            FontSize = 12, Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        closeBtn.Click += (_, _) => Close();
        Grid.SetColumn(closeBtn, 2);

        titleGrid.Children.Add(shieldIcon);
        titleGrid.Children.Add(titleText);
        titleGrid.Children.Add(closeBtn);
        titleBar.Child = titleGrid;
        Grid.SetRow(titleBar, 0);
        grid.Children.Add(titleBar);

        // ── Score hero area ────────────────────────────────────────────────
        var heroPanel = new Grid
        {
            Background = accent,
            Margin = new Thickness(12, 8, 12, 0)
        };
        heroPanel.ColumnDefinitions.Add(new ColumnDefinition());
        heroPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heroLeft = new StackPanel { Margin = new Thickness(14, 12, 0, 12) };

        var verdict_badge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding      = new Thickness(8, 3, 8, 3),
            Background   = new SolidColorBrush(Color.FromArgb(0x30, color.R, color.G, color.B)),
            BorderBrush  = brush,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 6)
        };
        verdict_badge.Child = new TextBlock
        {
            Text = RiskEmojis.GetValueOrDefault(item.RiskLevel, item.RiskLevel).ToUpperInvariant(),
            FontSize = 9, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Cascadia Code, Consolas"),
            Foreground = brush
        };
        heroLeft.Children.Add(verdict_badge);

        heroLeft.Children.Add(new TextBlock
        {
            Text = $"Data Selling: {(item.DataSelling ? "⚠ YES" : "✓ NO")}",
            FontSize = 11,
            Foreground = item.DataSelling
                ? new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5))
                : new SolidColorBrush(Color.FromRgb(0x6E, 0xE7, 0xB7)),
            Margin = new Thickness(0, 0, 0, 3)
        });
        heroLeft.Children.Add(new TextBlock
        {
            Text = $"Retention: {item.Retention}",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
        });
        heroLeft.Children.Add(new TextBlock
        {
            Text = $"Analysed: {item.AnalysedAt:HH:mm:ss  dd MMM yyyy}",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
            Margin = new Thickness(0, 4, 0, 0)
        });

        Grid.SetColumn(heroLeft, 0);
        heroPanel.Children.Add(heroLeft);

        // Score circle
        var circleGrid = new Grid
        {
            Width = 72, Height = 72,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        circleGrid.Children.Add(new Ellipse
        {
            Width = 72, Height = 72,
            Stroke = brush, StrokeThickness = 3
        });
        circleGrid.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = item.SafetyPercent.ToString(),
                    FontSize = 22, FontWeight = FontWeights.Black,
                    Foreground = brush,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = "%", FontSize = 8, FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Cascadia Code, Consolas"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        });
        Grid.SetColumn(circleGrid, 1);
        heroPanel.Children.Add(circleGrid);

        // Wrap hero in rounded border
        var heroBorder = new Border
        {
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = heroPanel,
            Margin = new Thickness(12, 8, 12, 0)
        };
        Grid.SetRow(heroBorder, 1);
        grid.Children.Add(heroBorder);

        // ── Scrollable detail area ─────────────────────────────────────────
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(12, 8, 12, 12)
        };
        var detailStack = new StackPanel { Spacing = 10 };

        // Verdict
        if (!string.IsNullOrWhiteSpace(item.Verdict))
        {
            detailStack.Children.Add(SectionLabel("VERDICT"));
            detailStack.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Background   = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                Padding      = new Thickness(12, 10, 12, 10),
                Child = new TextBlock
                {
                    Text       = item.Verdict,
                    FontSize   = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                }
            });
        }

        // Key risks
        if (item.KeyRisks.Count > 0)
        {
            detailStack.Children.Add(SectionLabel("KEY RISKS"));
            foreach (var risk in item.KeyRisks)
            {
                var riskRow = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background   = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                    Padding      = new Thickness(10, 7, 10, 7),
                    Margin       = new Thickness(0, 0, 0, 4)
                };
                var riskGrid = new Grid();
                riskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                riskGrid.ColumnDefinitions.Add(new ColumnDefinition());
                riskGrid.Children.Add(new Ellipse
                {
                    Width = 6, Height = 6,
                    Fill  = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
                var riskText = new TextBlock
                {
                    Text = risk, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1))
                };
                Grid.SetColumn(riskText, 1);
                riskGrid.Children.Add(riskText);
                riskRow.Child = riskGrid;
                detailStack.Children.Add(riskRow);
            }
        }

        scroll.Content = detailStack;
        Grid.SetRow(scroll, 2);
        grid.Children.Add(scroll);

        return root;
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text       = text,
        FontSize   = 9, FontWeight = FontWeights.Bold,
        FontFamily = new FontFamily("Cascadia Code, Consolas"),
        Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
        Margin     = new Thickness(2, 0, 0, 2)
    };
}
