// ============================================================================
// NativeBridgeConverters.cs
// WPF value converters used by NativeBridgeWindow.xaml
// ============================================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AegisSentinel.UI.Converters;

/// <summary>Maps a RiskLevel string → the appropriate accent Brush.</summary>
[ValueConversion(typeof(string), typeof(Brush))]
public sealed class RiskLevelToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Map = new()
    {
        ["Safe"]    = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
        ["Caution"] = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)),
        ["Warning"] = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)),
        ["Danger"]  = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
    };

    private static readonly SolidColorBrush Default =
        new(Color.FromRgb(0x94, 0xA3, 0xB8));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Map.GetValueOrDefault(value as string ?? "", Default);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps SafetyPercent (0–100) → pixel Width proportionally.</summary>
[ValueConversion(typeof(int), typeof(double))]
public sealed class SafetyPercentToWidthConverter : IValueConverter
{
    public double MaxWidth { get; set; } = 200;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int pct)
            return Math.Max(4, (pct / 100.0) * MaxWidth);
        return 4;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Standard bool → Visibility with optional inversion.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
