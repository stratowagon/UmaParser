namespace UmaParser.Presentation;

/// <summary>An opaque sRGB color. Cell fills and their text live in Core so both themes can be tested.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Black { get; } = new(0, 0, 0);

    public static RgbColor White { get; } = new(255, 255, 255);

    public double ContrastWith(RgbColor other) => ColorContrast.Ratio(this, other);
}

/// <summary>
/// WCAG relative-luminance contrast. <see cref="BodyText"/> is the AA bar for normal text (4.5:1).
/// </summary>
public static class ColorContrast
{
    public const double BodyText = 4.5;

    public static double Ratio(RgbColor fill, RgbColor text) =>
        Ratio(fill.R, fill.G, fill.B, text.R, text.G, text.B);

    public static double Ratio(ScoreHeatColor fill, ScoreHeatColor text) =>
        Ratio(fill.R, fill.G, fill.B, text.R, text.G, text.B);

    public static double Ratio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        double lighter = Math.Max(Luminance(r1, g1, b1), Luminance(r2, g2, b2));
        double darker = Math.Min(Luminance(r1, g1, b1), Luminance(r2, g2, b2));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(byte r, byte g, byte b) =>
        (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));

    private static double Channel(byte component)
    {
        double s = component / 255d;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
