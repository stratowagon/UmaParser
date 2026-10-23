namespace UmaParser.Presentation;

/// <summary>
/// One cell on the Results score heat map. Light-mode anchors from <c>AppColors</c>:
/// worst <c>#F8696B</c>, middle white, best <c>#63BE7B</c>.
/// The WPF window paints this directly. WinForms still blends through
/// <c>ConditionalColorScale</c> so its dark theme is unchanged.
/// </summary>
public readonly record struct ScoreHeatColor(byte R, byte G, byte B)
{
    public static ScoreHeatColor Bad { get; } = new(248, 105, 107);

    public static ScoreHeatColor Neutral { get; } = new(255, 255, 255);

    public static ScoreHeatColor Good { get; } = new(99, 190, 123);
}

/// <summary>
/// Maps a score between the grid's minimum and maximum onto the light-mode heat map.
/// The curve matches WinForms: values near the middle stay near white, and only the
/// real extremes reach red or green.
/// </summary>
public static class ScoreHeat
{
    // >1 pushes values away from the extremes toward the neutral anchor; 2.0 is a moderate flattening.
    private const double MidpointCompression = 2.0;

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
    /// When <paramref name="min"/> equals <paramref name="max"/>, returns the neutral midpoint.
    /// </summary>
    public static ScoreHeatColor ColorFor(double value, double min, double max)
    {
        if (max <= min)
        {
            return ScoreHeatColor.Neutral;
        }

        double t = Math.Clamp((value - min) / (max - min), 0d, 1d);
        double u = CompressTowardMidpoint(t);
        if (u <= 0.5)
        {
            return Blend(ScoreHeatColor.Bad, ScoreHeatColor.Neutral, (float)(u * 2d));
        }

        return Blend(ScoreHeatColor.Neutral, ScoreHeatColor.Good, (float)((u - 0.5) * 2d));
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
