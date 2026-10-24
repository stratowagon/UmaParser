using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>
/// Fill and text for a <see cref="CellBand"/>. Light fills are soft pastels on white.
/// Dark fills stay on the same hues as WinForms and only move when that pair missed 4.5:1.
/// </summary>
public static class CellBandPalette
{
    public static RgbColor Background(CellBand band, ColorTheme theme) => band switch
    {
        CellBand.Stable => theme == ColorTheme.Dark ? new RgbColor(46, 125, 50) : new RgbColor(183, 225, 184),
        CellBand.NeedsWork => theme == ColorTheme.Dark ? new RgbColor(255, 179, 0) : new RgbColor(255, 216, 168),
        CellBand.Critical => theme == ColorTheme.Dark ? new RgbColor(198, 40, 40) : new RgbColor(246, 193, 193),
        CellBand.Gold => theme == ColorTheme.Dark ? new RgbColor(255, 193, 7) : new RgbColor(255, 233, 160),
        CellBand.Unique => theme == ColorTheme.Dark ? new RgbColor(94, 84, 128) : new RgbColor(215, 198, 239),
        _ => RgbColor.Black,
    };

    public static RgbColor Foreground(CellBand band, ColorTheme theme) => band switch
    {
        // Dark stable, critical, and unique are the fills dark enough to need white text.
        // Light pastels, and the amber dark fills, stay with black.
        CellBand.Stable or CellBand.Critical or CellBand.Unique =>
            theme == ColorTheme.Dark ? RgbColor.White : RgbColor.Black,
        CellBand.NeedsWork or CellBand.Gold => RgbColor.Black,
        // No band. Same body text as the dark grid (#F1F1F1) and black on the light grid.
        _ => theme == ColorTheme.Dark ? new RgbColor(241, 241, 241) : RgbColor.Black,
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
