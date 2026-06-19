namespace UmaParser.Analysis;

/// <summary>
/// Pre-race wit activation chance for skills with <c>skill_data.activate_lot = 1</c>.
/// Uses base wit (mood-adjusted in race); floor 20%.
/// </summary>
public static class WitSkillActivationChance
{
    public const double MinimumPercent = 20.0;

    public static double PercentFromBaseWit(int baseWit)
    {
        if (baseWit <= 0)
        {
            return MinimumPercent;
        }

        return Math.Max(MinimumPercent, 100.0 - 9000.0 / baseWit);
    }
}