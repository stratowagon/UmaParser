using UmaParser.Analysis;
using UmaParser.Presentation;

namespace UmaParser.Web;

/// <summary>CSS for a <see cref="CellBand"/>. <see cref="CellBand.None"/> leaves the row color alone.</summary>
internal static class BandStyle
{
    public static string? For(CellBand band, ColorTheme theme)
    {
        if (band == CellBand.None)
        {
            return null;
        }

        RgbColor background = CellBandPalette.Background(band, theme);
        RgbColor foreground = CellBandPalette.Foreground(band, theme);
        return $"background:rgb({background.R},{background.G},{background.B});color:rgb({foreground.R},{foreground.G},{foreground.B})";
    }
}
