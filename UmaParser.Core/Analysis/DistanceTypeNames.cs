namespace UmaParser.Analysis;

/// <summary>
/// Distance-type labels. 1 Sprint, 2 Mile, 3 Medium, 4 Long, 5 Dirt.
/// The roster grids and the analysis engine both use this list.
/// </summary>
public static class DistanceTypeNames
{
    /// <summary>Roster display. Unknown types are "?".</summary>
    public static string Format(int distanceType) => distanceType switch
    {
        1 => "Sprint",
        2 => "Mile",
        3 => "Medium",
        4 => "Long",
        5 => "Dirt",
        _ => "?"
    };

    /// <summary>Analysis rows. Unknown types stay "Race N" so existing reports do not change.</summary>
    public static string FormatAnalysis(int distanceType) =>
        distanceType is >= 1 and <= 5 ? Format(distanceType) : $"Race {distanceType}";

    /// <summary>
    /// Label for a roster team when no race result is available.
    /// Team 0 is Sprint. Teams past Dirt stay "Team N".
    /// </summary>
    public static string FormatTeamSlot(int teamIndex) =>
        teamIndex is >= 0 and <= 4 ? Format(teamIndex + 1) : $"Team {teamIndex + 1}";
}
