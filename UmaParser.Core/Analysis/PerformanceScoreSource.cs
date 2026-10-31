using UmaParser.MasterData;

namespace UmaParser.Analysis;

/// <summary>
/// One slice of a Team Trials base score. The stacked bar draws these left to right.
/// </summary>
public enum PerformanceScoreSource
{
    FinishAndMargin,
    SkillWhite,
    SkillGold,
    SkillUnique,
    StrongStart,
    StyleAndExtras,
}

/// <summary>
/// Maps a <c>team_stadium_raw_score</c> row onto a chart segment using the master name
/// and description (text_data 140/141). An id the catalog does not know falls into
/// <see cref="PerformanceScoreSource.StyleAndExtras"/> so the bar still sums to the
/// uma's base score.
/// </summary>
public static class PerformanceScoreSources
{
    public static IReadOnlyList<PerformanceScoreSource> StackOrder { get; } =
    [
        PerformanceScoreSource.FinishAndMargin,
        PerformanceScoreSource.SkillWhite,
        PerformanceScoreSource.SkillGold,
        PerformanceScoreSource.SkillUnique,
        PerformanceScoreSource.StrongStart,
        PerformanceScoreSource.StyleAndExtras,
    ];

    public static string Label(PerformanceScoreSource source) => source switch
    {
        PerformanceScoreSource.FinishAndMargin => "Finish + margin",
        PerformanceScoreSource.SkillWhite => "White skills",
        PerformanceScoreSource.SkillGold => "Gold skills",
        PerformanceScoreSource.SkillUnique => "Unique skills",
        PerformanceScoreSource.StrongStart => "Strong start",
        PerformanceScoreSource.StyleAndExtras => "Style & extras",
        _ => source.ToString(),
    };

    public static bool IsSkill(PerformanceScoreSource source) =>
        source is PerformanceScoreSource.SkillWhite
            or PerformanceScoreSource.SkillGold
            or PerformanceScoreSource.SkillUnique;

    internal static PerformanceScoreSource Classify(int rawScoreId, GameMasterCatalog catalog) =>
        catalog.TryGetTeamTrialsScore(rawScoreId, out TeamTrialsScoreEntry entry)
            ? Classify(entry)
            : PerformanceScoreSource.StyleAndExtras;

    internal static PerformanceScoreSource Classify(TeamTrialsScoreEntry entry)
    {
        string name = entry.Name ?? string.Empty;
        string description = entry.Description ?? string.Empty;

        if (Contains(name, "Unique Skill"))
        {
            return PerformanceScoreSource.SkillUnique;
        }

        if (Contains(name, "Rare Skill"))
        {
            return PerformanceScoreSource.SkillGold;
        }

        if (IsWhiteSkill(name, description))
        {
            return PerformanceScoreSource.SkillWhite;
        }

        if (Contains(name, "Strong Start") || Contains(description, "successful start"))
        {
            return PerformanceScoreSource.StrongStart;
        }

        // "1st place" … "12th place", plus win-margin rows ("winning by a nose", lengths, …).
        // Team awards such as "1st, 2nd & 3rd Place" do not match an ordinal place name.
        if (IsOrdinalPlace(name) || Contains(description, "winning by"))
        {
            return PerformanceScoreSource.FinishAndMargin;
        }

        return PerformanceScoreSource.StyleAndExtras;
    }

    private static bool IsWhiteSkill(string name, string description)
    {
        if (name.Equals("Skill Activated", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Contains(description, "Unique") || Contains(description, "Rare"))
        {
            return false;
        }

        return Contains(description, "activating skills");
    }

    private static bool IsOrdinalPlace(string name)
    {
        int space = name.IndexOf(' ');
        if (space <= 0 || space >= name.Length - 1)
        {
            return false;
        }

        if (!name.AsSpan(space + 1).Equals("place", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        ReadOnlySpan<char> ordinal = name.AsSpan(0, space);
        if (ordinal.Length < 3 || !char.IsDigit(ordinal[0]))
        {
            return false;
        }

        for (int i = 0; i < ordinal.Length - 2; i++)
        {
            if (!char.IsDigit(ordinal[i]))
            {
                return false;
            }
        }

        ReadOnlySpan<char> suffix = ordinal[^2..];
        return suffix.Equals("st", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("nd", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("rd", StringComparison.OrdinalIgnoreCase)
            || suffix.Equals("th", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string text, string value) =>
        text.Contains(value, StringComparison.OrdinalIgnoreCase);
}
