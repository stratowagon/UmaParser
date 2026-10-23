using System.Text.Json;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.MasterData;
using UmaParser.Presentation;
using UmaParser.Settings;
using UmaParser.ViewModels;
using Xunit;

namespace UmaParser.Core.Tests.Settings;

/// <summary>
/// settings.json is one file. A column edit from the WPF options view model must load through
/// <see cref="GameMasterSettings"/>, which is what the WinForms dialog reads, and the other way around.
/// Window placement and the other keys stay put.
/// </summary>
public sealed class SettingsRoundTripTests
{
    [Fact]
    public void A_winforms_column_save_is_what_the_wpf_options_view_model_shows()
    {
        using var file = new TempSettingsFile();
        var settings = new GameMasterSettings
        {
            CustomMasterDbPath = "D:/games/master.mdb",
            WindowX = 15,
            WindowY = 25,
            WindowWidth = 900,
            WindowHeight = 640,
            WindowState = WindowPlacement.Maximized,
            ColorMode = "Dark",
        };
        settings.SetGridColumnPreferences(GridKeys.Skills, new GridColumnGridPreferences
        {
            Order =
            [
                GridColumnIds.Skills.Activations,
                GridColumnIds.Skills.PtsRace,
                GridColumnIds.Skills.PerRacePct,
                GridColumnIds.Skills.ProcsPerRacePct,
                GridColumnIds.Skills.WitDelta,
            ],
            Hidden = [GridColumnIds.Skills.WitDelta],
        });
        settings.Save(file.FilePath);

        var viewModel = new OptionsViewModel(
            () => new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true },
            file.FilePath);
        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Skills);

        Assert.Equal(GridColumnIds.Skills.Activations, viewModel.Columns[0].Id);
        Assert.Equal(GridColumnIds.Skills.PtsRace, viewModel.Columns[1].Id);
        ColumnOptionLine wit = viewModel.Columns.Single(column => column.Id == GridColumnIds.Skills.WitDelta);
        Assert.True(wit.IsAvailable);
        Assert.False(wit.IsVisible);

        GameMasterSettings loaded = GameMasterSettings.Load(file.FilePath);
        Assert.Equal("Dark", loaded.ColorMode);
        Assert.Equal("D:/games/master.mdb", loaded.CustomMasterDbPath);
        Assert.True(WindowPlacement.IsMaximized(loaded));
        Assert.Equal(900, loaded.WindowWidth);
    }

    [Fact]
    public void A_wpf_options_save_is_readable_by_the_winforms_settings_loader()
    {
        using var file = new TempSettingsFile();
        var seeded = new GameMasterSettings
        {
            CustomMasterDbPath = "D:/games/master.mdb",
            WindowX = 15,
            WindowY = 25,
            WindowWidth = 900,
            WindowHeight = 640,
            WindowState = WindowPlacement.Maximized,
            ColorMode = "Dark",
        };
        seeded.Save(file.FilePath);

        var viewModel = new OptionsViewModel(
            () => new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true },
            file.FilePath);
        viewModel.SelectedGrid = viewModel.Grids.Single(grid => grid.Key == GridKeys.Tracks);
        viewModel.SelectedColumn = viewModel.Columns.Single(column => column.Id == GridColumnIds.Tracks.Races);
        viewModel.MoveUp();
        viewModel.Columns.Single(column => column.Id == GridColumnIds.Tracks.Survival).IsVisible = false;
        viewModel.Save();

        GameMasterSettings loaded = GameMasterSettings.Load(file.FilePath);
        GridColumnGridPreferences tracks = SavedGridColumns.ForTracks(loaded);
        Assert.Equal(GridColumnIds.Tracks.Races, tracks.Order[0]);
        Assert.Equal(GridColumnIds.Tracks.Distance, tracks.Order[1]);
        Assert.Contains(GridColumnIds.Tracks.Survival, tracks.Hidden);
        Assert.Equal("Dark", loaded.ColorMode);
        Assert.Equal("D:/games/master.mdb", loaded.CustomMasterDbPath);
        Assert.Equal(15, loaded.WindowX);
        Assert.Equal(25, loaded.WindowY);
        Assert.Equal(900, loaded.WindowWidth);
        Assert.Equal(640, loaded.WindowHeight);
        Assert.Equal(WindowPlacement.Maximized, loaded.WindowState);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file.FilePath));
        JsonElement root = document.RootElement;
        Assert.Equal("Maximized", root.GetProperty("WindowState").GetString());
        Assert.Equal("Dark", root.GetProperty("ColorMode").GetString());
        JsonElement savedTracks = root.GetProperty("GridColumns").GetProperty("Grids").GetProperty("Tracks");
        Assert.Equal(GridColumnIds.Tracks.Races, savedTracks.GetProperty("Order")[0].GetString());
        Assert.Equal(GridColumnIds.Tracks.Survival, savedTracks.GetProperty("Hidden")[0].GetString());
        Assert.True(root.GetProperty("GridColumns").GetProperty("Grids").TryGetProperty("Analysis", out _));
        Assert.True(root.GetProperty("GridColumns").GetProperty("Grids").TryGetProperty("Skills", out _));
    }

    [Fact]
    public void Hand_written_settings_json_drops_unknown_ids_and_keeps_the_other_keys()
    {
        using var file = new TempSettingsFile();
        File.WriteAllText(file.FilePath, """
            {
              "CustomMasterDbPath": "D:/games/master.mdb",
              "WindowX": 15,
              "WindowY": 25,
              "WindowWidth": 900,
              "WindowHeight": 640,
              "WindowState": "Maximized",
              "ColorMode": "Dark",
              "GridColumns": {
                "Grids": {
                  "Analysis": {
                    "Order": [ "analysis.not-a-column", "analysis.style", "analysis.avg" ],
                    "Hidden": [ "analysis.avg", "analysis.not-a-column" ]
                  }
                }
              }
            }
            """);

        var viewModel = new OptionsViewModel(settingsPath: file.FilePath);
        Assert.Equal(GridColumnIds.Analysis.Style, viewModel.Columns[0].Id);
        Assert.Equal(GridColumnIds.Analysis.Avg, viewModel.Columns[1].Id);
        Assert.False(viewModel.Columns[1].IsVisible);
        Assert.DoesNotContain(viewModel.Columns, column => column.Id == "analysis.not-a-column");
        Assert.True(viewModel.Columns[2].IsVisible);

        viewModel.Save();
        GameMasterSettings loaded = GameMasterSettings.Load(file.FilePath);
        Assert.Equal("Dark", loaded.ColorMode);
        Assert.Equal(WindowPlacement.Maximized, loaded.WindowState);
        Assert.Equal("D:/games/master.mdb", loaded.CustomMasterDbPath);
        GridColumnGridPreferences analysis = SavedGridColumns.ForAnalysis(loaded);
        Assert.Equal(GridColumnIds.Analysis.Style, analysis.Order[0]);
        Assert.DoesNotContain("analysis.not-a-column", analysis.Order);
        Assert.Contains(GridColumnIds.Analysis.Avg, analysis.Hidden);
        Assert.DoesNotContain("analysis.not-a-column", analysis.Hidden);
    }

    [Fact]
    public void Saving_the_window_does_not_drop_columns_and_saving_columns_does_not_drop_the_window()
    {
        using var file = new TempSettingsFile();
        var settings = new GameMasterSettings();
        settings.SetGridColumnPreferences(GridKeys.Skills, new GridColumnGridPreferences
        {
            Order =
            [
                GridColumnIds.Skills.Activations,
                GridColumnIds.Skills.PtsRace,
                GridColumnIds.Skills.PerRacePct,
                GridColumnIds.Skills.ProcsPerRacePct,
                GridColumnIds.Skills.WitDelta,
            ],
            Hidden = [GridColumnIds.Skills.PtsRace],
        });
        settings.Save(file.FilePath);

        GameMasterSettings windowSave = GameMasterSettings.Load(file.FilePath);
        WindowPlacement.Write(windowSave, 4, 8, 1000, 700, maximized: false);
        windowSave.Save(file.FilePath);

        GameMasterSettings afterWindow = GameMasterSettings.Load(file.FilePath);
        GridColumnGridPreferences skills = SavedGridColumns.ForSkills(afterWindow);
        Assert.Equal(GridColumnIds.Skills.Activations, skills.Order[0]);
        Assert.Contains(GridColumnIds.Skills.PtsRace, skills.Hidden);
        Assert.Equal(WindowPlacement.Normal, afterWindow.WindowState);
        Assert.Equal(1000, afterWindow.WindowWidth);

        var session = ColumnOptionsSession.Load(
            afterWindow,
            new GridColumnContext { IsTeamTrial = true, HasSkillLotMetadata = true });
        session.SelectGrid(GridKeys.Skills);
        Assert.Equal(GridColumnIds.Skills.Activations, session.Rows[0].Id);
        Assert.False(session.Rows.Single(row => row.Id == GridColumnIds.Skills.PtsRace).IsVisible);
        session.TryMoveSelected(1);
        session.WriteTo(afterWindow);
        afterWindow.Save(file.FilePath);

        GameMasterSettings afterColumns = GameMasterSettings.Load(file.FilePath);
        Assert.Equal(4, afterColumns.WindowX);
        Assert.Equal(8, afterColumns.WindowY);
        Assert.Equal(1000, afterColumns.WindowWidth);
        Assert.Equal(700, afterColumns.WindowHeight);
        Assert.Equal(WindowPlacement.Normal, afterColumns.WindowState);
        Assert.Equal(GridColumnIds.Skills.PtsRace, SavedGridColumns.ForSkills(afterColumns).Order[0]);
    }
}
