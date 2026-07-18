using UmaParser.Analysis;
using UmaParser.MasterData;
using UmaParser.Settings;
using UmaParser.Ui;

namespace UmaParser;

public partial class Form1
{
    private OptionsDialog? _openOptionsDialog;

    private void InitializeOptionsMenu()
    {
        optionsPreferencesMenuItem.Click += OptionsPreferencesMenuItem_Click;
    }

    private void OptionsPreferencesMenuItem_Click(object? sender, EventArgs e)
    {
        if (_openOptionsDialog != null && !_openOptionsDialog.IsDisposed)
        {
            _openOptionsDialog.BringToFront();
            _openOptionsDialog.RefreshAvailability();
            return;
        }

        var dialog = new OptionsDialog(BuildGridColumnOptionsGrids(), BuildGridColumnContext);
        dialog.PreferencesApplied += (_, _) => RebindGridsFromCachedData();
        dialog.FormClosed += (_, _) => _openOptionsDialog = null;
        _openOptionsDialog = dialog;
        dialog.Show(this);
    }

    private GridColumnContext BuildGridColumnContext() =>
        new()
        {
            IsTeamTrial = _skillsTrialResults != null || _tracksTrialResults != null,
            HasSkillLotMetadata = _lastSkillReport?.HasSkillLotMetadata ?? false,
        };

    private static IReadOnlyList<GridColumnOptionsGrid> BuildGridColumnOptionsGrids() =>
    [
        BuildOptionsGrid(GridKeys.Analysis, "Team Analysis", AnalysisColumnSet.All),
        BuildOptionsGrid(GridKeys.Skills, "Skills", SkillsColumnSet.All),
        BuildOptionsGrid(GridKeys.Tracks, "Tracks", TracksColumnSet.All),
    ];

    private static GridColumnOptionsGrid BuildOptionsGrid<TRow>(
        string key,
        string displayName,
        IReadOnlyList<GridColumnDefinition<TRow>> columns) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            Columns = columns.Select(c => new GridColumnOptionsEntry
            {
                Id = c.Id,
                Header = c.Header,
                Tooltip = c.Tooltip,
                Description = c.Description,
                IsLocked = c.IsLocked,
                IsAvailable = c.CheckAvailable,
                UnavailableReason = ctx => c.GetUnavailableReason(ctx) ?? string.Empty,
            }).ToList(),
        };

    private GridColumnGridPreferences LoadGridColumnPreferences<TRow>(string gridKey)
    {
        var settings = GameMasterSettings.Load();
        var defaultOrder = GridColumnBinder.GetDefaultCustomizableOrder(
            GetRegistry<TRow>(gridKey));
        return GridColumnPreferencesMerger.Merge(defaultOrder, settings.GetGridColumnPreferences(gridKey));
    }

    private static IReadOnlyList<GridColumnDefinition<TRow>> GetRegistry<TRow>(string gridKey) =>
        gridKey switch
        {
            GridKeys.Analysis when typeof(TRow) == typeof(UmaAnalysisRow) =>
                (IReadOnlyList<GridColumnDefinition<TRow>>)(object)AnalysisColumnSet.All,
            GridKeys.Skills when typeof(TRow) == typeof(SkillActivationRow) =>
                (IReadOnlyList<GridColumnDefinition<TRow>>)(object)SkillsColumnSet.All,
            GridKeys.Tracks when typeof(TRow) == typeof(TracksRow) =>
                (IReadOnlyList<GridColumnDefinition<TRow>>)(object)TracksColumnSet.All,
            _ => throw new ArgumentOutOfRangeException(nameof(gridKey)),
        };

    private void RebindGridsFromCachedData()
    {
        if (_lastAnalysisReport != null)
        {
            BindAnalysisGrid(_lastAnalysisReport);
        }

        if (_lastSkillReport != null)
        {
            BindSkillsGrid(_lastSkillReport);
        }

        if (_lastTracksReport != null)
        {
            BindTracksGrid(_lastTracksReport);
        }

        _openOptionsDialog?.RefreshAvailability();
    }
}