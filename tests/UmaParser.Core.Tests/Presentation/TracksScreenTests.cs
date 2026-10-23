using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Presentation;
using UmaParser.Settings;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

/// <summary>
/// The WPF Tracks view model displays a <see cref="TracksScreen"/>. These tests cover the
/// uma list, the total row, and the summaries without a WPF dispatcher.
/// </summary>
public sealed class TracksScreenTests
{
    private static readonly GridColumnGridPreferences AllColumns = new();

    [Fact]
    public void Consistent_team_trials_list_the_roster_and_total_the_tracks()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        TracksScreen screen = TracksScreen.Load(batch, AllColumns);

        Assert.Equal(
            RosterListing.FromTrial(batch.Trials.Values.First()).Select(uma => uma.Name),
            screen.Umas.Select(uma => uma.Name));
        Assert.Equal(15, screen.Umas.Count);
        Assert.Equal(0, screen.SelectedIndex);
        Assert.True(screen.IsTeamTrial);
        Assert.Contains(screen.Columns, column => column.Id == GridColumnIds.Tracks.AvgSkillPts);
        Assert.Equal(GridColumnIds.Tracks.Track, screen.Columns[0].Id);

        TracksReport report = TracksAnalyzer.Analyze(
            batch.Trials.Values,
            screen.Umas[0].TrainedCharaId,
            screen.Umas[0].Name);
        Assert.Equal(report.Rows.Select(row => (row.Track, row.Distance)), screen.Rows.Select(row => (row.Track, row.Distance)));
        Assert.Equal(TrackCellText.Report(report), screen.Summary);
        Assert.NotEmpty(screen.Rows);

        TracksTotals expected = TracksTotals.Compute(report.Rows, includeSkillPoints: true);
        Assert.True(screen.Footer.HasValue);
        Assert.Equal(expected, screen.Footer.Value);
        Assert.Equal(screen.Rows.Count + 1, screen.GridRows.Count);
        Assert.False(screen.GridRows[0].IsFooter);
        Assert.Equal(CellBands.ForTrackRate(screen.Rows[0].SpurtRate), screen.GridRows[0].SpurtBand);
        Assert.Equal(CellBands.ForTrackRate(screen.Rows[0].Survival), screen.GridRows[0].SurvivalBand);
        TracksGridRow footer = screen.GridRows[^1];
        Assert.True(footer.IsFooter);
        Assert.Equal(TrackCellText.TotalLabel, footer.Track);
        Assert.Equal("", footer.DistanceText);
        Assert.Equal(expected.Races, footer.Races);
        Assert.Equal(TrackCellText.AveragePlace(expected.AvgPlace), footer.AvgPlaceText);
        Assert.Equal(TrackCellText.Percent(expected.SpurtRate), footer.SpurtRateText);
        Assert.Equal(CellBand.None, footer.SpurtBand);
        Assert.Equal(CellBand.None, footer.SurvivalBand);
        Assert.Equal(TrackCellText.SkillPoints(expected.AvgSkillPointsPerRace), footer.AvgSkillPointsText);

        TracksScreen second = screen.Select(1);
        TracksReport secondReport = TracksAnalyzer.Analyze(
            batch.Trials.Values,
            screen.Umas[1].TrainedCharaId,
            screen.Umas[1].Name);
        Assert.Equal(screen.Umas[1].Name, second.Report!.UmaName);
        Assert.Equal(
            secondReport.Rows.Select(row => (row.Track, row.Distance, row.Races)),
            second.Rows.Select(row => (row.Track, row.Distance, row.Races)));
        Assert.Contains("Team type:", second.Summary);
    }

    [Fact]
    public void Mismatch_keeps_the_stable_core_and_pins_the_load_summary_until_the_uma_changes()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-Mismatch");
        RosterConsistencyReport consistency = RosterConsistencyAnalyzer.Analyze(batch);
        TracksScreen screen = TracksScreen.Load(batch, AllColumns);

        Assert.Equal(consistency.StableCore.Select(uma => uma.DisplayName), screen.Umas.Select(uma => uma.Name));
        Assert.Equal(TrackCellText.ConsistentUmas(screen.Umas.Count), screen.Summary);
        Assert.DoesNotContain("Team type:", screen.Summary);
        Assert.NotEmpty(screen.Rows);
        Assert.True(screen.GridRows[^1].IsFooter);
        Assert.Same(screen, screen.Select(0));

        TracksScreen changed = screen.Select(1);
        Assert.Contains("Team type:", changed.Summary);
        Assert.Equal(screen.Umas[1].Name, changed.Report!.UmaName);
        Assert.Equal(TrackCellText.Report(changed.Report), changed.Summary);
    }

    [Fact]
    public void Mismatch_with_no_stable_core_explains_that_tracks_has_nothing_to_show()
    {
        Assert.Equal("No consistent umas for Tracks.", TrackCellText.ConsistentUmas(0));
        Assert.Equal("4 consistent uma(s) for track performance.", TrackCellText.ConsistentUmas(4));
    }

    [Fact]
    public void Single_race_files_use_the_local_player_list_and_hide_skill_points()
    {
        List<CaptureImportResult> imports = FixtureCatalog.InGroup("Single-CM12")
            .Concat(FixtureCatalog.InGroup("Single-RoomMatch"))
            .Concat(FixtureCatalog.InGroup("Single-Practice"))
            .Select(FixtureCatalog.Import)
            .ToList();
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(imports);
        NonTeamTrialLoad load = NonTeamTrialLoad.Collect(batch.SingleRaceImports);
        TracksScreen screen = TracksScreen.Load(batch, AllColumns, load);

        Assert.Equal(TeamTrialBatchKind.MixedOrNonTT, batch.Kind);
        Assert.False(screen.IsTeamTrial);
        Assert.Equal(load.Identities.Select(identity => identity.GetDisplayName()), screen.Umas.Select(uma => uma.Name));
        Assert.True(screen.Umas.Count > 1);
        Assert.DoesNotContain(screen.Columns, column => column.Id == GridColumnIds.Tracks.AvgSkillPts);
        Assert.Equal(TrackCellText.Report(screen.Report!), screen.Summary);
        Assert.Contains("Non-TT", screen.Summary);
        Assert.Equal("", screen.GridRows[^1].AvgSkillPointsText);
        Assert.True(screen.GridRows[^1].IsFooter);
        Assert.Equal(CellBand.None, screen.GridRows[^1].SpurtBand);

        TracksScreen changed = screen.Select(1);
        Assert.Equal(screen.Umas[1].Name, changed.Report!.UmaName);
        Assert.Contains("Team type:", changed.Summary);
    }

    [Fact]
    public void A_caller_supplied_single_race_load_is_the_selector_order()
    {
        List<CaptureImportResult> imports = FixtureCatalog.InGroup("Single-CM12")
            .Select(FixtureCatalog.Import)
            .ToList();
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(imports);
        NonTeamTrialLoad collected = NonTeamTrialLoad.Collect(batch.SingleRaceImports);
        var reversed = new NonTeamTrialLoad
        {
            Races = collected.Races,
            Identities = collected.Identities.Reverse().ToList(),
            ParsedSimulationCount = collected.ParsedSimulationCount,
            TotalHorseSlots = collected.TotalHorseSlots,
        };

        TracksScreen screen = TracksScreen.Load(batch, AllColumns, reversed);

        Assert.Equal(reversed.Identities.Select(identity => identity.GetDisplayName()), screen.Umas.Select(uma => uma.Name));
        Assert.Equal(reversed.Identities[0].GetDisplayName(), screen.Report!.UmaName);
    }

    [Fact]
    public void An_uma_with_no_appearances_clears_the_grid_and_says_so()
    {
        List<CaptureImportResult> imports = FixtureCatalog.InGroup("Single-Practice")
            .Select(FixtureCatalog.Import)
            .ToList();
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(imports);
        NonTeamTrialLoad collected = NonTeamTrialLoad.Collect(batch.SingleRaceImports);
        UmaIdentity ghost = collected.Identities[0] with { TrainedCharaId = -1, OwnerName = "nobody" };
        var load = new NonTeamTrialLoad
        {
            Races = collected.Races,
            Identities = [ghost],
        };

        TracksScreen screen = TracksScreen.Load(batch, AllColumns, load);

        Assert.Equal(TrackCellText.NoAppearances(ghost.GetDisplayName()), screen.Summary);
        Assert.Empty(screen.Rows);
        Assert.Empty(screen.GridRows);
        Assert.Null(screen.Footer);
        Assert.Null(screen.Report);
    }

    [Fact]
    public void Hidden_survival_column_is_honored_for_a_real_roster()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        var prefs = new GridColumnGridPreferences { Hidden = [GridColumnIds.Tracks.Survival] };
        TracksScreen screen = TracksScreen.Load(batch, prefs);

        Assert.DoesNotContain(screen.Columns, column => column.Id == GridColumnIds.Tracks.Survival);
        Assert.Contains(screen.Columns, column => column.Id == GridColumnIds.Tracks.Track);
        Assert.Contains(screen.Columns, column => column.Id == GridColumnIds.Tracks.AvgSkillPts);
    }

    [Fact]
    public void Empty_batch_has_nothing_to_show()
    {
        var batch = new TeamTrialBatch { Kind = TeamTrialBatchKind.Empty };
        TracksScreen screen = TracksScreen.Load(batch, AllColumns);

        Assert.Empty(screen.Umas);
        Assert.Equal(-1, screen.SelectedIndex);
        Assert.Equal("", screen.Summary);
        Assert.Empty(screen.GridRows);
        Assert.False(screen.IsTeamTrial);
    }
}
