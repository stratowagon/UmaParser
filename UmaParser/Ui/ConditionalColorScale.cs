using System.Drawing;

namespace UmaParser.Ui;

/// <summary>
/// Theme-aware 3-color scale (red = worst, neutral = middle, green = best).
/// Light mode uses Excel-style pastel anchors on white; dark mode tints into the grid background
/// so neutral cells blend in and light foreground text stays readable throughout.
/// </summary>
internal static class ConditionalColorScale
{
    // >1 pushes values away from the extremes toward the neutral anchor; 2.0 is a moderate flattening.
    private const double MidpointCompression = 2.0;

    /// <summary>
    /// Maps a value between <paramref name="min"/> and <paramref name="max"/> to the color scale.
    /// When min equals max, returns the theme's neutral midpoint color.
    /// </summary>
    public static Color ColorForValue(double value, double min, double max)
    {
        (Color bad, Color neutral, Color good) = GetAnchors();

        if (max <= min)
        {
            return neutral;
        }

        double t = Math.Clamp((value - min) / (max - min), 0d, 1d);
        double u = CompressTowardMidpoint(t);

        if (u <= 0.5)
        {
            return Blend(bad, neutral, (float)(u * 2d));
        }

        return Blend(neutral, good, (float)((u - 0.5) * 2d));
    }

    private static (Color bad, Color neutral, Color good) GetAnchors() =>
        (AppColors.HeatmapBadBack, AppColors.HeatmapNeutralBack, AppColors.HeatmapGoodBack);

    /// <summary>
    /// Remaps a linear 0–1 position so values near the center stay near 0.5 (neutral)
    /// while true extremes still reach 0 and 1.
    /// </summary>
    private static double CompressTowardMidpoint(double t)
    {
        double distanceFromCenter = Math.Abs(t - 0.5) * 2d;
        double compressed = Math.Pow(distanceFromCenter, MidpointCompression);
        return t < 0.5
            ? 0.5 - compressed * 0.5
            : 0.5 + compressed * 0.5;
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        int r = (int)(from.R + (to.R - from.R) * amount);
        int g = (int)(from.G + (to.G - from.G) * amount);
        int b = (int)(from.B + (to.B - from.B) * amount);
        return Color.FromArgb(from.A, r, g, b);
    }
}