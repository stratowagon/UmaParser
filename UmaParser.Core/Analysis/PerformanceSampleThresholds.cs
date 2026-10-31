using System.Globalization;
using UmaParser.MasterData;

namespace UmaParser.Analysis;

/// <summary>
/// How thin a Performance row's sample is.
/// Red when the race count is below <see cref="PerformanceSampleThresholds.Hard"/>.
/// Yellow when the count is at least that hard threshold and below <see cref="PerformanceSampleThresholds.Soft"/>.
/// No warning at or above the soft threshold.
/// Defaults are red below 10, yellow from 10 up to but not including 20, and quiet at 20 or more.
/// </summary>
public enum PerformanceSampleWarning
{
    None,
    Soft,
    Hard,
}

/// <summary>
/// The two Options thresholds for a short Performance sample.
/// <see cref="Soft"/> is the yellow line (default 20). <see cref="Hard"/> is the red line (default 10).
/// </summary>
public readonly record struct PerformanceSampleThresholds(int Soft, int Hard)
{
    public const int DefaultSoft = 20;
    public const int DefaultHard = 10;

    public static PerformanceSampleThresholds Default { get; } = new(DefaultSoft, DefaultHard);

    /// <summary>
    /// Red when <paramref name="raceCount"/> is below <see cref="Hard"/>.
    /// Yellow when <see cref="Hard"/> &lt;= count &lt; <see cref="Soft"/>.
    /// None when the count is at least <see cref="Soft"/>.
    /// </summary>
    public PerformanceSampleWarning Classify(int raceCount)
    {
        if (raceCount < Hard)
        {
            return PerformanceSampleWarning.Hard;
        }

        if (raceCount < Soft)
        {
            return PerformanceSampleWarning.Soft;
        }

        return PerformanceSampleWarning.None;
    }

    /// <summary>
    /// Values stored in settings.json. A missing key uses that threshold's default.
    /// A value below 1, or a red threshold that is not strictly below the yellow one, uses both defaults.
    /// </summary>
    public static PerformanceSampleThresholds FromSaved(int? soft, int? hard)
    {
        int resolvedSoft = soft ?? DefaultSoft;
        int resolvedHard = hard ?? DefaultHard;
        if (resolvedSoft < 1 || resolvedHard < 1 || resolvedHard >= resolvedSoft)
        {
            return Default;
        }

        return new PerformanceSampleThresholds(resolvedSoft, resolvedHard);
    }

    /// <summary>
    /// Options text boxes. Blank uses that box's default. Anything else has to be a whole number of 1 or more,
    /// and the red number has to be lower than the yellow number.
    /// </summary>
    public static bool TryParse(string? softText, string? hardText, out PerformanceSampleThresholds thresholds, out string error)
    {
        if (!TryRead(softText, DefaultSoft, "Yellow", out int soft, out error)
            || !TryRead(hardText, DefaultHard, "Red", out int hard, out error))
        {
            thresholds = default;
            return false;
        }

        if (hard >= soft)
        {
            thresholds = default;
            error = "The red threshold has to be lower than the yellow threshold. "
                + "Red is a race count below the red number. "
                + "Yellow starts at the red number and stops before the yellow number.";
            return false;
        }

        thresholds = new PerformanceSampleThresholds(soft, hard);
        error = "";
        return true;
    }

    private static bool TryRead(string? text, int blankDefault, string label, out int value, out string error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = blankDefault;
            error = "";
            return true;
        }

        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            && !int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            value = 0;
            error = $"{label} threshold has to be a whole number of 1 or more, or blank for the default.";
            return false;
        }

        if (value < 1)
        {
            error = $"{label} threshold has to be a whole number of 1 or more, or blank for the default.";
            return false;
        }

        error = "";
        return true;
    }
}

/// <summary>
/// Reads the Performance sample thresholds from the shared settings file.
/// Other keys in that file are left as they were. Writing goes through Options, which loads the same object.
/// </summary>
internal static class PerformanceSamplePreferences
{
    public static PerformanceSampleThresholds Load(string? path)
    {
        GameMasterSettings settings = GameMasterSettings.Load(path);
        return PerformanceSampleThresholds.FromSaved(
            settings.PerformanceSampleSoftThreshold,
            settings.PerformanceSampleHardThreshold);
    }
}
