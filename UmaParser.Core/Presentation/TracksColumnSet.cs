using UmaParser.Analysis;
using UmaParser.Settings;

namespace UmaParser.Presentation;

public static class TracksColumnSet
{
    public static IReadOnlyList<GridColumnDefinition<TracksRow>> All { get; } = Build();

    private static IReadOnlyList<GridColumnDefinition<TracksRow>> Build()
    {
        return
        [
            Col(GridColumnIds.Tracks.Track, "Track",
                "Race course + distance.",
                "Race course and distance. This identity column is always shown first.  KNOWN ISSUE: Tracks that have more than one course with the same distance are being combined.",
                typeof(string), true, r => r.Track),
            Col(GridColumnIds.Tracks.Distance, "Distance",
                "Distance in meters.",
                "Race distance in meters.",
                typeof(int), false, r => r.Distance),
            Col(GridColumnIds.Tracks.Races, "Races",
                "Number of races on this specific track & distance.",
                "Number of races on this specific track & distance.",
                typeof(int), false, r => r.Races),
            Col(GridColumnIds.Tracks.AvgPlace, "Avg Place",
                "Average finish position on this track.",
                "Average finish position on this track.",
                typeof(double), false, r => r.AvgPlace),
            Col(GridColumnIds.Tracks.PctFirst, "% 1st",
                "Percentage of races on this track where the uma finished 1st.",
                "Percentage of races on this track where the uma finished 1st.",
                typeof(double), false, r => r.PctFirst),
            Col(GridColumnIds.Tracks.PctTop3, "% Top 3",
                "Percentage of races finishing 3rd or better.",
                "Percentage of races finishing 3rd or better.",
                typeof(double), false, r => r.PctTop3),
            Col(GridColumnIds.Tracks.PctTop5, "% Top 5",
                "Percentage of races finishing 5th or better.",
                "Percentage of races finishing 5th or better.",
                typeof(double), false, r => r.PctTop5),
            Col(GridColumnIds.Tracks.SpurtRate, "Spurt Rate",
                "% of races where final spurt was not delayed.",
                "Percent of races where the final spurt was not delayed. Cells below 100% may be shaded.",
                typeof(double), false, r => r.SpurtRate),
            Col(GridColumnIds.Tracks.Survival, "Survival",
                "% of races where the uma did not run out of HP before the goal.",
                "Percent of races where the uma did not run out of HP before the goal. Cells below 100% may be shaded.",
                typeof(double), false, r => r.Survival),
            Col(GridColumnIds.Tracks.AvgSkillPts, "Avg Skill Pts/Race",
                "Average skill points earned per race on this track (without bonuses).",
                "Average skill points earned per race on this track (without bonuses). Only available when Team Trial captures are loaded.",
                typeof(double), false, r => r.AvgSkillPointsPerRace,
                ctx => ctx.IsTeamTrial,
                _ => "Only shown for Team Trial captures."),
        ];
    }

    private static GridColumnDefinition<TracksRow> Col(
        string id,
        string header,
        string tooltip,
        string description,
        Type valueType,
        bool isLocked,
        Func<TracksRow, object?> getValue,
        Func<GridColumnContext, bool>? isAvailable = null,
        Func<GridColumnContext, string?>? unavailableReason = null) =>
        new()
        {
            Id = id,
            Header = header,
            Tooltip = tooltip,
            Description = description,
            ValueType = valueType,
            IsLocked = isLocked,
            GetValue = getValue,
            IsAvailable = isAvailable,
            UnavailableReason = unavailableReason,
        };
}