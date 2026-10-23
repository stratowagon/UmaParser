using UmaParser.MasterData;

namespace UmaParser.Analysis;

/// <summary>
/// Colour band for a grid cell. The value is UI-neutral: WinForms maps it through
/// <c>AppColors</c>, and a WPF view can map it to a brush.
/// </summary>
public enum CellBand
{
    None = 0,
    Stable,
    NeedsWork,
    Critical,
    Gold,
    Unique,
}

/// <summary>
/// The CellFormatting colour rules from the Analysis, Skills, and Tracks tabs.
/// </summary>
public static class CellBands
{
    /// <summary>Average points per activation, inclusive, that paints a skill name gold.</summary>
    public const double SkillGoldAverageMin = 1150;

    /// <summary>Average points per activation, inclusive, that still paints a skill name gold.</summary>
    public const double SkillGoldAverageMax = 1250;

    /// <summary>Averages strictly above this paint the skill name as unique.</summary>
    public const double SkillUniqueAverageAbove = 1250;

    /// <summary>Positive averages strictly below this paint the skill name as unique.</summary>
    public const double SkillUniqueAverageBelow = 450;

    /// <summary>Spurt rate and survival strictly below this are critical.</summary>
    public const double TrackRateCriticalBelow = 95;

    /// <summary>Spurt rate and survival strictly below this (and at or above the critical line) need work.</summary>
    public const double TrackRateNeedsWorkBelow = 100;

    public static CellBand ForRetrain(UmaAnalysisRow row) => row.PriorityLevel switch
    {
        RetrainPriorityLevel.Critical => CellBand.Critical,
        RetrainPriorityLevel.NeedsWork => CellBand.NeedsWork,
        _ => CellBand.Stable,
    };

    public static CellBand ForStyleOverlap(UmaAnalysisRow row) => row.StyleOverlap switch
    {
        TeamStyleOverlap.FullTeam => CellBand.Critical,
        TeamStyleOverlap.Pair => CellBand.NeedsWork,
        _ => CellBand.None,
    };

    /// <summary>
    /// Ace-delta column. Coloured only when the delta is positive and this row is the suggested ace.
    /// A matching recommendation that is not a weak suggestion is critical; otherwise needs-work.
    /// </summary>
    public static CellBand ForAceDelta(UmaAnalysisReport report, UmaAnalysisRow row)
    {
        if (!(row.AceDelta > 0 && row.IsSuggestedAce))
        {
            return CellBand.None;
        }

        TeamAceRecommendation? recommendation = null;
        foreach (TeamAceRecommendation candidate in report.AceRecommendations)
        {
            if (candidate.Distance == row.Distance && candidate.BestSupport == row.Name)
            {
                recommendation = candidate;
                break;
            }
        }

        return recommendation != null && !recommendation.IsWeakSuggestion
            ? CellBand.Critical
            : CellBand.NeedsWork;
    }

    /// <summary>
    /// Skill-name column. Gold is the closed 1150–1250 band. Unique is above 1250,
    /// or a positive average below 450. Zero activations stay uncoloured.
    /// </summary>
    public static CellBand ForSkillName(SkillActivationRow row)
    {
        double average = row.ActivationCount > 0
            ? row.TotalPointsEarned / (double)row.ActivationCount
            : 0.0;

        if (average >= SkillGoldAverageMin && average <= SkillGoldAverageMax)
        {
            return CellBand.Gold;
        }

        if (average > SkillUniqueAverageAbove || (average > 0 && average < SkillUniqueAverageBelow))
        {
            return CellBand.Unique;
        }

        return CellBand.None;
    }

    /// <summary>
    /// Other skill columns. Wit underperformance wins, then a low activation that is not wit-gated.
    /// </summary>
    public static CellBand ForSkillMetric(SkillActivationRow row)
    {
        if (row.IsUnderperformingVsWit)
        {
            return CellBand.NeedsWork;
        }

        if (row.IsLowActivation && row.ActivateLot != SkillActivateLotKind.Wit)
        {
            return CellBand.NeedsWork;
        }

        return CellBand.None;
    }

    /// <summary>
    /// Spurt rate and survival, in percent. The footer row does not use this; the view skips it there.
    /// </summary>
    public static CellBand ForTrackRate(double percent)
    {
        if (percent < TrackRateCriticalBelow)
        {
            return CellBand.Critical;
        }

        if (percent < TrackRateNeedsWorkBelow)
        {
            return CellBand.NeedsWork;
        }

        return CellBand.None;
    }
}
