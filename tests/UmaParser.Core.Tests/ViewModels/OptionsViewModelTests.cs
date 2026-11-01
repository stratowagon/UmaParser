using UmaParser.Core.Tests.TestSupport;
using UmaParser.MasterData;
using UmaParser.Presentation;
using UmaParser.Settings;
using UmaParser.ViewModels;
using Xunit;

namespace UmaParser.Core.Tests.ViewModels;

/// <summary>
/// <see cref="OptionsViewModel"/> is compiled into this project from the WPF sources.
/// It edits column prefs through <see cref="ColumnOptionsSession"/>, the same type the WinForms dialog uses.
/// </summary>
public sealed class OptionsViewModelTests
{
    [Fact]
    public void The_dialog_opens_on_team_analysis_with_the_locked_column_called_out()
    {
        using var file = new TempSettingsFile();
        var viewModel = new OptionsViewModel(settingsPath: file.FilePath);

        Assert.Equal(GridKeys.Analysis, viewModel.SelectedGrid!.Key);
        Assert.Equal(["Team Analysis", "Skills", "Tracks"], viewModel.Grids.Select(grid => grid.DisplayName));
        Assert.Equal("Uma — always shown first", viewModel.LockedColumnText);
        Assert.Equal(GridColumnIds.Analysis.Style, viewModel.SelectedColumn!.Id);
        Assert.False(viewModel.CanMoveUp);
        Assert.True(viewModel.CanMoveDown);
        Assert.DoesNotContain(viewModel.Columns, column => column.Id == GridColumnIds.Analysis.Uma);
        Assert.All(viewModel.Columns, column => Assert.True(column.IsVisible));
        Assert.Contains("Running style", viewModel.Description);
    }

    [Fact]
    public void Move_keeps_the_check_and_restore_defaults_puts_the_catalog_order_back()
    {
        using var file = new TempSettingsFile();
        var viewModel = new OptionsViewModel(
            () => new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true },
            file.FilePath);
        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Skills);

        ColumnOptionLine second = viewModel.Columns[1];
        second.IsVisible = false;
        viewModel.SelectedColumn = second;
        string movedId = second.Id;
        Assert.True(viewModel.CanMoveUp);
        viewModel.MoveUp();

        Assert.Equal(movedId, viewModel.Columns[0].Id);
        Assert.False(viewModel.Columns[0].IsVisible);
        Assert.Equal(movedId, viewModel.SelectedColumn!.Id);

        viewModel.MoveUp();
        Assert.Equal(movedId, viewModel.Columns[0].Id);

        viewModel.RestoreDefaults();
        Assert.Equal(GridColumnIds.Skills.PtsRace, viewModel.Columns[0].Id);
        Assert.Equal(GridColumnIds.Skills.Activations, viewModel.Columns[1].Id);
        Assert.All(viewModel.Columns, column => Assert.True(column.IsVisible));
        Assert.Equal(viewModel.Columns[0], viewModel.SelectedColumn);
    }

    [Fact]
    public void An_unavailable_column_cannot_be_toggled_or_moved_across()
    {
        using var file = new TempSettingsFile();
        var viewModel = new OptionsViewModel(
            () => new GridColumnContext { IsTeamTrial = false, HasSkillLotMetadata = false },
            file.FilePath);
        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Skills);

        ColumnOptionLine points = viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.PtsRace);
        Assert.False(points.IsAvailable);
        Assert.True(points.IsVisible);
        points.IsVisible = false;
        Assert.True(points.IsVisible);

        viewModel.SelectedColumn = points;
        Assert.False(viewModel.CanMoveDown);
        viewModel.MoveDown();
        Assert.Equal(GridColumnIds.Skills.PtsRace, viewModel.Columns[0].Id);
        Assert.Contains("Not available: Only shown for Team Trial captures.", viewModel.Description);
        Assert.Contains("Tooltip: Average skill points per race.", viewModel.Description);

        viewModel.SelectedColumn = viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.Activations);
        Assert.False(viewModel.CanMoveUp);

        ColumnOptionLine perRace = viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.PerRacePct);
        viewModel.SelectedColumn = perRace;
        Assert.DoesNotContain("Tooltip:", viewModel.Description);
    }

    [Fact]
    public void Refreshing_availability_updates_the_description_and_keeps_the_check()
    {
        GridColumnContext context = new() { IsTeamTrial = false, HasSkillLotMetadata = false };
        using var file = new TempSettingsFile();
        var viewModel = new OptionsViewModel(() => context, file.FilePath);
        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Skills);
        viewModel.SelectedColumn = viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.PtsRace);
        viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.Activations).IsVisible = false;

        context = new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true };
        viewModel.RefreshAvailability();

        ColumnOptionLine points = viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.PtsRace);
        Assert.True(points.IsAvailable);
        Assert.True(points.IsVisible);
        Assert.False(viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.Activations).IsVisible);
        Assert.DoesNotContain("Not available", viewModel.Description);
        Assert.True(viewModel.CanMoveDown);
    }

    [Fact]
    public void Switching_grids_keeps_the_edit_on_the_grid_you_left()
    {
        using var file = new TempSettingsFile();
        var viewModel = new OptionsViewModel(settingsPath: file.FilePath);
        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Skills);
        ColumnOptionLine activations = viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.Activations);
        activations.IsVisible = false;
        Assert.False(activations.IsVisible);

        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Tracks);
        Assert.Equal("Track — always shown first", viewModel.LockedColumnText);
        Assert.All(viewModel.Columns, column => Assert.True(column.IsVisible));

        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Skills);
        Assert.False(viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.Activations).IsVisible);
    }

    [Fact]
    public void Save_raises_applied_and_a_new_view_model_reads_the_file_back()
    {
        using var file = new TempSettingsFile();
        var context = new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true };
        var viewModel = new OptionsViewModel(() => context, file.FilePath);
        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Analysis);
        viewModel.SelectedColumn = viewModel.Columns.Single(column => column.Id == GridColumnIds.Analysis.Avg);
        viewModel.MoveUp();
        viewModel.Columns.Single(column => column.Id == GridColumnIds.Analysis.Gap).IsVisible = false;

        int applied = 0;
        viewModel.PreferencesApplied += (_, _) => applied++;
        Assert.True(viewModel.Save());
        Assert.Equal(1, applied);

        var again = new OptionsViewModel(() => context, file.FilePath);
        Assert.Equal(GridColumnIds.Analysis.Avg, again.Columns[0].Id);
        Assert.Equal(GridColumnIds.Analysis.Style, again.Columns[1].Id);
        Assert.False(again.Columns.Single(column => column.Id == GridColumnIds.Analysis.Gap).IsVisible);

        GameMasterSettings settings = GameMasterSettings.Load(file.FilePath);
        GridColumnGridPreferences analysis = SavedGridColumns.ForAnalysis(settings);
        Assert.Equal(GridColumnIds.Analysis.Avg, analysis.Order[0]);
        Assert.Contains(GridColumnIds.Analysis.Gap, analysis.Hidden);
    }

    [Fact]
    public void Performance_sample_thresholds_save_with_the_other_settings_keys()
    {
        using var file = new TempSettingsFile();
        var seeded = new GameMasterSettings
        {
            ColorMode = "Dark",
            PerformanceSort = "P90",
            PerformanceSortAscending = false,
            WindowWidth = 900,
        };
        seeded.Save(file.FilePath);

        var viewModel = new OptionsViewModel(settingsPath: file.FilePath);
        Assert.Equal("20", viewModel.SampleSoftThresholdText);
        Assert.Equal("10", viewModel.SampleHardThresholdText);

        viewModel.SampleSoftThresholdText = "25";
        viewModel.SampleHardThresholdText = "8";
        Assert.True(viewModel.Save());

        GameMasterSettings saved = GameMasterSettings.Load(file.FilePath);
        Assert.Equal(25, saved.PerformanceSampleSoftThreshold);
        Assert.Equal(8, saved.PerformanceSampleHardThreshold);
        Assert.Equal("Dark", saved.ColorMode);
        Assert.Equal("P90", saved.PerformanceSort);
        Assert.False(saved.PerformanceSortAscending);
        Assert.Equal(900, saved.WindowWidth);

        var again = new OptionsViewModel(settingsPath: file.FilePath);
        Assert.Equal("25", again.SampleSoftThresholdText);
        Assert.Equal("8", again.SampleHardThresholdText);

        again.SampleHardThresholdText = "25";
        Assert.False(again.Save());
        Assert.False(string.IsNullOrWhiteSpace(again.SampleThresholdError));
        Assert.Equal(8, GameMasterSettings.Load(file.FilePath).PerformanceSampleHardThreshold);
        Assert.Equal("Dark", GameMasterSettings.Load(file.FilePath).ColorMode);

        again.RestoreSampleDefaults();
        Assert.Equal("20", again.SampleSoftThresholdText);
        Assert.Equal("10", again.SampleHardThresholdText);
        Assert.Equal("", again.SampleThresholdError);
    }
}
