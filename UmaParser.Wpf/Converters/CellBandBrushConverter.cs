using System.Globalization;
using System.Windows;
using System.Windows.Data;
using UmaParser.Analysis;
using UmaParser.Themes;

namespace UmaParser.Converters;

/// <summary>
/// Maps a <see cref="CellBand"/> to the shared theme brush.
/// <see cref="CellBand.None"/> has no fill, so the row color shows through.
/// Its text still comes from the theme. A style setter that yields
/// <see cref="DependencyProperty.UnsetValue"/> falls back to black, which hides the base cell color.
/// The brush instance stays put when the theme changes; only its color is rewritten.
/// </summary>
public sealed class CellBandBrushConverter : IValueConverter
{
    public bool Foreground { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CellBand band || band == CellBand.None)
        {
            return Foreground ? CellBandBrushSource.NeutralForeground : DependencyProperty.UnsetValue;
        }

        return CellBandBrushSource.For(band, Foreground)
            ?? (Foreground ? CellBandBrushSource.NeutralForeground : DependencyProperty.UnsetValue);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
