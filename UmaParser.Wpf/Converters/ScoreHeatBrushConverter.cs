using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using UmaParser.Presentation;

namespace UmaParser.Converters;

/// <summary>
/// Maps a <see cref="ScoreHeatColor"/> to a frozen brush. A null heat value leaves the cell uncolored.
/// </summary>
public sealed class ScoreHeatBrushConverter : IValueConverter
{
    private readonly Dictionary<ScoreHeatColor, SolidColorBrush> _brushes = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ScoreHeatColor color)
        {
            return DependencyProperty.UnsetValue;
        }

        if (!_brushes.TryGetValue(color, out SolidColorBrush? brush))
        {
            brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            _brushes.Add(color, brush);
        }

        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
