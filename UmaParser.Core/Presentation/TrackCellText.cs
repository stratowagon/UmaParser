using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>
/// Display text for Tracks cells and the line under the uma selector.
/// WinForms cell formatting and the WPF grid both call this.
/// </summary>
public static class TrackCellText
{
    /// <summary>Label in the first cell of the total row.</summary>
    public const string TotalLabel = "Total";

    public static string Percent(double value) => value.ToString("0.#") + "%";

    public static string AveragePlace(double value) => value.ToString("0.00");

    public static string SkillPoints(double value) => value.ToString("0");

    /// <summary>Same sentence the WinForms Tracks tab writes under the uma selector.</summary>
    public static string Report(TracksReport report) =>
        $"{report.UmaName}: {report.Rows.Count} tracks, {report.TotalRaces} race(s) total. Team type: {report.TeamType}. ";

    /// <summary>
    /// Shown after a roster-mismatch drop, until the user picks a different uma.
    /// Zero means no uma kept the same team and running style in every file.
    /// </summary>
    public static string ConsistentUmas(int count) =>
        count > 0
            ? $"{count} consistent uma(s) for track performance."
            : "No consistent umas for Tracks.";

    public static string NoAppearances(string umaName) => $"{umaName}: No appearances found.";

    public static string Unavailable(string umaName) => $"{umaName}: tracks analysis not available.";
}

/// <summary>
/// Raw values for the Tracks total row, one per column id.
/// Distance is blank. Skill points are omitted outside Team Trials.
/// WinForms puts these in the footer row. <see cref="TracksGridRow"/> formats the same values for WPF.
/// </summary>
public static class TracksFooterValues
{
    public static object? Get(string columnId, TracksTotals totals, bool includeSkillPoints) => columnId switch
    {
        GridColumnIds.Tracks.Track => TrackCellText.TotalLabel,
        GridColumnIds.Tracks.Distance => null,
        GridColumnIds.Tracks.Races => totals.Races,
        GridColumnIds.Tracks.AvgPlace => totals.AvgPlace,
        GridColumnIds.Tracks.PctFirst => totals.PctFirst,
        GridColumnIds.Tracks.PctTop3 => totals.PctTop3,
        GridColumnIds.Tracks.PctTop5 => totals.PctTop5,
        GridColumnIds.Tracks.SpurtRate => totals.SpurtRate,
        GridColumnIds.Tracks.Survival => totals.Survival,
        GridColumnIds.Tracks.AvgSkillPts => includeSkillPoints ? totals.AvgSkillPointsPerRace : null,
        _ => null,
    };
}
