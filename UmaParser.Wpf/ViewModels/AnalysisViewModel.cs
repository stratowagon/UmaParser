using System.Collections.ObjectModel;
using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.Mvvm;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.ViewModels;

/// <summary>
/// Team Analysis tab state. A uniform roster fills <see cref="Rows"/> in roster order.
/// Any other drop shows <see cref="EmptyMessage"/> and hides the grid.
/// <see cref="Summary"/> is the status-bar text for a uniform roster. It is empty otherwise.
/// </summary>
public sealed class AnalysisViewModel : ObservableObject
{
    private IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> _visibleColumns =
        Array.Empty<GridColumnDefinition<UmaAnalysisRow>>();
    private TeamTrialBatch? _batch;
    private bool _showGrid = true;
    private string _emptyMessage = "";
    private string _summary = "";
    private int _gridRevision;

    public AnalysisViewModel()
    {
        Rows = new ObservableCollection<AnalysisGridRow>();
    }

    public ObservableCollection<AnalysisGridRow> Rows { get; private set; }

    public bool ShowGrid
    {
        get => _showGrid;
        private set => SetProperty(ref _showGrid, value);
    }

    public string EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }

    /// <summary>Multi-line status summary. The window copies this onto the status bar after a uniform roster loads.</summary>
    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> VisibleColumns
    {
        get => _visibleColumns;
        private set => SetProperty(ref _visibleColumns, value);
    }

    /// <summary>Bumps when the grid is replaced so the view can rebuild columns.</summary>
    public int GridRevision => _gridRevision;

    /// <param name="columnPreferences">
    /// Saved column prefs. The window passes null to read settings.json.
    /// Tests pass an explicit value and do not read the settings file.
    /// </param>
    public void Apply(TeamTrialBatch batch, GridColumnGridPreferences? columnPreferences = null)
    {
        _batch = batch;
        GridColumnGridPreferences preferences = columnPreferences ?? SavedGridColumns.ForAnalysis();
        AnalysisScreen screen = AnalysisScreen.Load(batch, preferences);
        Rows = new ObservableCollection<AnalysisGridRow>(screen.Rows);
        OnPropertyChanged(nameof(Rows));
        VisibleColumns = screen.Columns;
        ShowGrid = screen.ShowGrid;
        EmptyMessage = screen.EmptyMessage;
        Summary = screen.Summary;
        _gridRevision++;
        OnPropertyChanged(nameof(GridRevision));
    }

    /// <summary>
    /// Rebuilds the grid from the drop already loaded, using the latest column prefs.
    /// Does nothing before the first drop, and does not read the capture files again.
    /// </summary>
    public void ReloadColumns(GridColumnGridPreferences? columnPreferences = null)
    {
        if (_batch == null)
        {
            return;
        }

        Apply(_batch, columnPreferences ?? SavedGridColumns.ForAnalysis());
    }
}
