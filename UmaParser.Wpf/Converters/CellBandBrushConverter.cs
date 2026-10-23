using System.Globalization;
using System.Windows;
using System.Windows.Data;
using UmaParser.Analysis;

namespace UmaParser.Converters;

/// <summary>
/// Maps a <see cref="CellBand"/> to the brush in Themes/CellBandBrushes.xaml.
/// <see cref="CellBand.None"/> returns nothing so the cell keeps the grid's normal colors,
/// which is what WinForms does when <c>AppColors</c> has no band color.
/// </summary>
public sealed class CellBandBrushConverter : IValueConverter
{
    public bool Foreground { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not CellBand band || band == CellBand.None)
        {
            return DependencyProperty.UnsetValue;
        }

        string role = Foreground ? "Foreground" : "Background";
        return Application.Current?.TryFindResource($"CellBand.{band}.{role}")
            ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
