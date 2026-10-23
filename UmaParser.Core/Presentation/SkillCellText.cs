using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>Display text for Skills cells. WinForms and WPF both call this.</summary>
public static class SkillCellText
{
    public static string PointsPerRace(double value) => value.ToString("0");

    public static string OneDecimal(double value) => value.ToString("0.#");

    /// <summary>Em dash when the skill is not wit-gated. A real delta keeps its sign, including +0.</summary>
    public static string FormatWitDelta(SkillActivationRow row)
    {
        if (!row.WitDeltaPercent.HasValue)
        {
            return "—";
        }

        double delta = row.WitDeltaPercent.Value;
        return delta >= 0 ? $"+{delta:0.#}" : delta.ToString("0.#");
    }
}

/// <summary>
/// Comparison used by the Skills "Δ wit" column.
/// A missing delta sorts after a real number. WinForms then flips the result when the
/// header is descending, so nulls come first on a descending click. Callers that want
/// that same flip should negate this result for <see cref="System.ComponentModel.ListSortDirection.Descending"/>.
/// </summary>
public static class SkillWitDeltaSort
{
    public static int Compare(double? left, double? right)
    {
        if (!left.HasValue && !right.HasValue)
        {
            return 0;
        }

        if (!left.HasValue)
        {
            return 1;
        }

        if (!right.HasValue)
        {
            return -1;
        }

        return left.Value.CompareTo(right.Value);
    }
}
