using System.Windows;
using System.Windows.Media;
using UmaParser.Analysis;
using UmaParser.Presentation;

namespace UmaParser.Themes;

/// <summary>
/// One brush instance per band. A theme change writes a new color into that brush, so a
/// DataGrid cell that already holds it repaints without rebuilding rows or columns.
/// <see cref="NeutralForeground"/> is the text color for a cell with no band. It follows
/// <c>Grid.Foreground</c> so an uncolored cell is not stuck on the default black brush.
/// </summary>
internal static class CellBandBrushSource
{
    private static readonly Dictionary<CellBand, (SolidColorBrush Background, SolidColorBrush Foreground)> Bands = Create();

    public static SolidColorBrush NeutralForeground { get; } = new(Colors.Black);

    public static void Apply(ColorTheme theme)
    {
        foreach ((CellBand band, (SolidColorBrush background, SolidColorBrush foreground)) in Bands)
        {
            background.Color = ToColor(CellBandPalette.Background(band, theme));
            foreground.Color = ToColor(CellBandPalette.Foreground(band, theme));
        }

        NeutralForeground.Color = ThemeColor(
            "Grid.Foreground",
            theme == ColorTheme.Dark ? Color.FromRgb(0xF1, 0xF1, 0xF1) : Colors.Black);
    }

    public static Brush? For(CellBand band, bool foreground)
    {
        if (!Bands.TryGetValue(band, out (SolidColorBrush Background, SolidColorBrush Foreground) brushes))
        {
            return null;
        }

        return foreground ? brushes.Foreground : brushes.Background;
    }

    private static Dictionary<CellBand, (SolidColorBrush Background, SolidColorBrush Foreground)> Create()
    {
        var bands = new Dictionary<CellBand, (SolidColorBrush, SolidColorBrush)>();
        foreach (CellBand band in Enum.GetValues<CellBand>())
        {
            if (band == CellBand.None)
            {
                continue;
            }

            bands[band] = (new SolidColorBrush(), new SolidColorBrush());
        }

        return bands;
    }

    private static Color ToColor(RgbColor color) => Color.FromRgb(color.R, color.G, color.B);

    private static Color ThemeColor(string key, Color fallback)
    {
        if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return fallback;
    }
}
