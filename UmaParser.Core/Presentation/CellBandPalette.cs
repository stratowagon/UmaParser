using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>
/// Fill and text for a <see cref="CellBand"/>. Light fills match WinForms
/// <c>AppColors</c> except where that text missed 4.5:1. Dark fills stay on the same hues
/// and only move when the WinForms pair missed that bar.
/// </summary>
public static class CellBandPalette
{
    public static RgbColor Background(CellBand band, ColorTheme theme) => band switch
    {
        CellBand.Stable => new RgbColor(46, 125, 50),
        CellBand.NeedsWork => theme == ColorTheme.Dark ? new RgbColor(255, 179, 0) : new RgbColor(180, 134, 11),
        CellBand.Critical => theme == ColorTheme.Dark ? new RgbColor(198, 40, 40) : new RgbColor(183, 28, 28),
        CellBand.Gold => theme == ColorTheme.Dark ? new RgbColor(255, 193, 7) : new RgbColor(255, 215, 0),
        CellBand.Unique => theme == ColorTheme.Dark ? new RgbColor(94, 84, 128) : new RgbColor(176, 166, 210),
        _ => RgbColor.Black,
    };

    public static RgbColor Foreground(CellBand band, ColorTheme theme) => band switch
    {
        // Light amber #B4860B is 3.3:1 under white text and 6.4:1 under black.
        CellBand.NeedsWork => RgbColor.Black,
        CellBand.Gold => RgbColor.Black,
        // WinForms paints dark unique and dark stable with white text that falls under 4.5:1.
        CellBand.Unique => theme == ColorTheme.Dark ? RgbColor.White : RgbColor.Black,
        CellBand.Stable => RgbColor.White,
        CellBand.Critical => RgbColor.White,
        _ => theme == ColorTheme.Dark ? RgbColor.White : RgbColor.Black,
    };
}

/// <summary>
/// Roster-mismatch cells. Light yellow and the WinForms dark olive, with the text that clears 4.5:1.
/// </summary>
public static class ResultsMarks
{
    public static RgbColor Fill(ColorTheme theme) =>
        theme == ColorTheme.Dark ? new RgbColor(90, 80, 30) : new RgbColor(255, 255, 200);

    public static RgbColor Text(ColorTheme theme) =>
        theme == ColorTheme.Dark ? RgbColor.White : RgbColor.Black;
}
