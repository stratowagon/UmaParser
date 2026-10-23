using UmaParser.Analysis;
using UmaParser.Presentation;
using UmaParser.Settings;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class TracksColumnLayoutTests
{
    [Fact]
    public void Team_trials_show_every_tracks_column_and_keep_the_name_first()
    {
        var context = new GridColumnContext { IsTeamTrial = true };
        IReadOnlyList<GridColumnDefinition<TracksRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(TracksColumnSet.All, new GridColumnGridPreferences(), context);

        Assert.Equal(
            [
                GridColumnIds.Tracks.Track,
                GridColumnIds.Tracks.Distance,
                GridColumnIds.Tracks.Races,
                GridColumnIds.Tracks.AvgPlace,
                GridColumnIds.Tracks.PctFirst,
                GridColumnIds.Tracks.PctTop3,
                GridColumnIds.Tracks.PctTop5,
                GridColumnIds.Tracks.SpurtRate,
                GridColumnIds.Tracks.Survival,
                GridColumnIds.Tracks.AvgSkillPts,
            ],
            columns.Select(column => column.Id));
        Assert.Equal(
            ["Track", "Distance", "Races", "Avg Place", "% 1st", "% Top 3", "% Top 5", "Spurt Rate", "Survival", "Avg Skill Pts/Race"],
            columns.Select(column => column.Header));
    }

    [Fact]
    public void Skill_points_are_team_trials_only()
    {
        var context = new GridColumnContext { IsTeamTrial = false };
        IReadOnlyList<GridColumnDefinition<TracksRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(TracksColumnSet.All, new GridColumnGridPreferences(), context);

        Assert.DoesNotContain(columns, column => column.Id == GridColumnIds.Tracks.AvgSkillPts);
        Assert.Equal(GridColumnIds.Tracks.Track, columns[0].Id);
        Assert.Contains(columns, column => column.Id == GridColumnIds.Tracks.SpurtRate);
    }

    [Fact]
    public void A_saved_order_and_a_hidden_column_are_both_honored()
    {
        var prefs = new GridColumnGridPreferences
        {
            Order = [GridColumnIds.Tracks.Races, GridColumnIds.Tracks.Distance],
            Hidden = [GridColumnIds.Tracks.Survival, GridColumnIds.Tracks.Track],
        };
        var context = new GridColumnContext { IsTeamTrial = true };
        IReadOnlyList<GridColumnDefinition<TracksRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(TracksColumnSet.All, prefs, context);

        Assert.Equal(
            [
                GridColumnIds.Tracks.Track,
                GridColumnIds.Tracks.Races,
                GridColumnIds.Tracks.Distance,
                GridColumnIds.Tracks.AvgPlace,
                GridColumnIds.Tracks.PctFirst,
                GridColumnIds.Tracks.PctTop3,
                GridColumnIds.Tracks.PctTop5,
                GridColumnIds.Tracks.SpurtRate,
                GridColumnIds.Tracks.AvgSkillPts,
            ],
            columns.Select(column => column.Id));
    }
}
