using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using UmaParser.Settings;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

/// <summary>
/// The WPF Skills view model displays a <see cref="SkillsScreen"/>. These tests cover the
/// filtering and summaries without a WPF dispatcher, which is what the window binds to.
/// </summary>
public sealed class SkillsScreenTests
{
    private static readonly GridColumnGridPreferences AllColumns = new();

    [Fact]
    public void Consistent_team_trials_list_the_roster_and_follow_the_uma_selector()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        SkillsScreen screen = SkillsScreen.Load(batch, AllColumns);

        Assert.Equal(
            RosterListing.FromTrial(batch.Trials.Values.First()).Select(uma => uma.Name),
            screen.Umas.Select(uma => uma.Name));
        Assert.Equal(15, screen.Umas.Count);
        Assert.Equal(0, screen.SelectedIndex);
        Assert.True(screen.IsTeamTrial);
        Assert.Contains(screen.Columns, column => column.Id == GridColumnIds.Skills.PtsRace);
        Assert.Equal(screen.Report!.HasSkillLotMetadata, screen.Columns.Any(column => column.Id == GridColumnIds.Skills.WitDelta));

        SkillActivationReport report = SkillActivationAnalyzer.Analyze(
            batch.Trials.Values,
            screen.Umas[0].TrainedCharaId,
            screen.Umas[0].Name);
        Assert.Equal(
            report.Rows.OrderByDescending(row => row.PointsPerRace).Select(row => row.SkillId),
            screen.Rows.Select(row => row.SkillId));
        Assert.Equal(SkillsScreen.FormatReport(report), screen.Summary);
        Assert.Equal(report.Rows.Max(row => row.PointsPerRace), screen.Rows[0].PointsPerRace);

        SkillsScreen second = screen.Select(1);
        Assert.Equal(screen.Umas[1].Name, second.Report!.UmaName);
        Assert.Contains("Click column headers to sort.", second.Summary);
        Assert.NotEqual(screen.Rows.Select(row => row.SkillId), second.Rows.Select(row => row.SkillId));
    }

    [Fact]
    public void Mismatch_keeps_the_stable_core_and_pins_the_load_summary_until_the_uma_changes()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-Mismatch");
        RosterConsistencyReport consistency = RosterConsistencyAnalyzer.Analyze(batch);
        SkillsScreen screen = SkillsScreen.Load(batch, AllColumns);

        Assert.Equal(consistency.StableCore.Select(uma => uma.DisplayName), screen.Umas.Select(uma => uma.Name));
        Assert.Contains("consistent uma(s)", screen.Summary);
        Assert.DoesNotContain("Click column headers", screen.Summary);
        Assert.NotEmpty(screen.Rows);
        Assert.Same(screen, screen.Select(0));

        SkillsScreen changed = screen.Select(1);
        Assert.Contains("Click column headers", changed.Summary);
        Assert.Equal(screen.Umas[1].Name, changed.Report!.UmaName);
    }

    [Fact]
    public void Single_race_files_use_the_local_player_list_and_hide_points_per_race()
    {
        List<CaptureImportResult> imports = FixtureCatalog.InGroup("Single-CM12")
            .Concat(FixtureCatalog.InGroup("Single-RoomMatch"))
            .Concat(FixtureCatalog.InGroup("Single-Practice"))
            .Select(FixtureCatalog.Import)
            .ToList();
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(imports);
        NonTeamTrialLoad load = NonTeamTrialLoad.Collect(batch.SingleRaceImports);
        SkillsScreen screen = SkillsScreen.Load(batch, AllColumns);

        Assert.Equal(TeamTrialBatchKind.MixedOrNonTT, batch.Kind);
        Assert.False(screen.IsTeamTrial);
        Assert.Equal(load.Identities.Select(identity => identity.GetDisplayName()), screen.Umas.Select(uma => uma.Name));
        Assert.True(screen.Umas.Count > 1);
        Assert.DoesNotContain(screen.Columns, column => column.Id == GridColumnIds.Skills.PtsRace);
        Assert.Contains("local player uma(s)", screen.Summary);
        Assert.Contains($"{batch.SingleRaceImports.Count} single-race file(s)", screen.Summary);
        Assert.Equal(
            screen.Rows.Max(row => row.ActivationCount),
            screen.Rows[0].ActivationCount);

        SkillsScreen changed = screen.Select(1);
        Assert.Equal(screen.Umas[1].Name, changed.Report!.UmaName);
        Assert.Contains("Click column headers", changed.Summary);
    }

    [Fact]
    public void Hidden_activations_column_is_honored_for_a_real_roster()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        var prefs = new GridColumnGridPreferences { Hidden = [GridColumnIds.Skills.Activations] };
        SkillsScreen screen = SkillsScreen.Load(batch, prefs);

        Assert.DoesNotContain(screen.Columns, column => column.Id == GridColumnIds.Skills.Activations);
        Assert.Contains(screen.Columns, column => column.Id == GridColumnIds.Skills.Skill);
    }

    [Fact]
    public void Wit_delta_text_and_sort_match_the_winforms_grid()
    {
        Assert.Equal("—", SkillCellText.FormatWitDelta(new SkillActivationRow()));
        Assert.Equal("+12.3", SkillCellText.FormatWitDelta(new SkillActivationRow { WitDeltaPercent = 12.34 }));
        Assert.Equal("+0", SkillCellText.FormatWitDelta(new SkillActivationRow { WitDeltaPercent = 0 }));
        Assert.Equal("-0.4", SkillCellText.FormatWitDelta(new SkillActivationRow { WitDeltaPercent = -0.44 }));

        Assert.Equal(1, SkillWitDeltaSort.Compare(null, 1));
        Assert.Equal(-1, SkillWitDeltaSort.Compare(1, null));
        Assert.Equal(0, SkillWitDeltaSort.Compare(null, null));
        Assert.True(SkillWitDeltaSort.Compare(1.5, 2) < 0);
        Assert.True(-SkillWitDeltaSort.Compare(null, 1) < 0);
    }
}
