namespace UmaParser.Presentation;

/// <summary>
/// One cell on the Results score heat map.
/// Light anchors match WinForms <c>AppColors</c>: worst <c>#F8696B</c>, middle white, best <c>#63BE7B</c>.
/// Dark anchors match the WinForms dark heat map: worst <c>#913A3E</c>, middle <c>#202020</c>, best <c>#328048</c>.
/// WinForms still blends through <c>ConditionalColorScale</c>. The WPF sheet paints <see cref="ScoreHeat.ColorFor"/>.
/// </summary>
public readonly record struct ScoreHeatColor(byte R, byte G, byte B)
{
    public static ScoreHeatColor Bad { get; } = new(248, 105, 107);

    public static ScoreHeatColor Neutral { get; } = new(255, 255, 255);

    public static ScoreHeatColor Good { get; } = new(99, 190, 123);
}

/// <summary>Heat-map anchors and the text color that stays readable on every blend of those anchors.</summary>
public readonly record struct ScoreHeatPalette(
    ScoreHeatColor Bad,
    ScoreHeatColor Neutral,
    ScoreHeatColor Good,
    ScoreHeatColor Text);

/// <summary>
/// Maps a score between the grid's minimum and maximum onto the heat map.
/// The curve matches WinForms: values near the middle stay near the neutral anchor, and only the
/// real extremes reach red or green. <see cref="ColorFor(double, double, double)"/> is the light palette.
/// </summary>
public static class ScoreHeat
{
    // >1 pushes values away from the extremes toward the neutral anchor; 2.0 is a moderate flattening.
    private const double MidpointCompression = 2.0;

    private static readonly ScoreHeatPalette LightPalette = new(
        ScoreHeatColor.Bad,
        ScoreHeatColor.Neutral,
        ScoreHeatColor.Good,
        new ScoreHeatColor(0, 0, 0));

    // Same bytes as AppColors.DarkHeatmapBad / the dark grid / DarkHeatmapGood.
    private static readonly ScoreHeatPalette DarkPalette = new(
        new ScoreHeatColor(145, 58, 62),
        new ScoreHeatColor(32, 32, 32),
        new ScoreHeatColor(50, 128, 72),
        new ScoreHeatColor(255, 255, 255));

    public static ScoreHeatPalette Palette(ColorTheme theme) =>
        theme == ColorTheme.Dark ? DarkPalette : LightPalette;

    public static bool TryGetRange(IEnumerable<int> scores, out double min, out double max)
    {
        min = double.MaxValue;
        max = double.MinValue;
        bool any = false;
        foreach (int score in scores)
        {
            any = true;
            if (score < min)
            {
                min = score;
            }

            if (score > max)
            {
                max = score;
            }
        }

        return any && min <= max;
    }

    /// <summary>
    /// Light-mode color. When <paramref name="min"/> equals <paramref name="max"/>, returns white.
    /// </summary>
    public static ScoreHeatColor ColorFor(double value, double min, double max) =>
        ColorFor(value, min, max, ColorTheme.Light);

    /// <summary>
    /// When <paramref name="min"/> equals <paramref name="max"/>, returns that theme's neutral anchor.
    /// </summary>
    public static ScoreHeatColor ColorFor(double value, double min, double max, ColorTheme theme)
    {
        ScoreHeatPalette palette = Palette(theme);
        if (max <= min)
        {
            return palette.Neutral;
        }

        double t = Math.Clamp((value - min) / (max - min), 0d, 1d);
        double u = CompressTowardMidpoint(t);
        if (u <= 0.5)
        {
            return Blend(palette.Bad, palette.Neutral, (float)(u * 2d));
        }

        return Blend(palette.Neutral, palette.Good, (float)((u - 0.5) * 2d));
    }

    private static double CompressTowardMidpoint(double t)
    {
        double distanceFromCenter = Math.Abs(t - 0.5) * 2d;
        double compressed = Math.Pow(distanceFromCenter, MidpointCompression);
        return t < 0.5
            ? 0.5 - compressed * 0.5
            : 0.5 + compressed * 0.5;
    }

    private static ScoreHeatColor Blend(ScoreHeatColor from, ScoreHeatColor to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        int r = (int)(from.R + (to.R - from.R) * amount);
        int g = (int)(from.G + (to.G - from.G) * amount);
        int b = (int)(from.B + (to.B - from.B) * amount);
        return new ScoreHeatColor((byte)r, (byte)g, (byte)b);
    }
}
