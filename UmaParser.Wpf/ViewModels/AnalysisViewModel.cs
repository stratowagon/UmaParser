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
    private readonly ObservableCollection<AnalysisGridRow> _rows = new();
    private IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> _visibleColumns =
        Array.Empty<GridColumnDefinition<UmaAnalysisRow>>();
    private bool _showGrid = true;
    private string _emptyMessage = "";
    private string _summary = "";
    private int _gridRevision;

    public AnalysisViewModel()
    {
        Rows = _rows;
    }

    public ObservableCollection<AnalysisGridRow> Rows { get; }

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
    /// Tests pass an explicit value and do not read the WinForms settings file.
    /// </param>
    public void Apply(TeamTrialBatch batch, GridColumnGridPreferences? columnPreferences = null)
    {
        GridColumnGridPreferences preferences = columnPreferences ?? AnalysisColumnPreferences.Load();
        AnalysisScreen screen = AnalysisScreen.Load(batch, preferences);
        _rows.Clear();
        foreach (AnalysisGridRow row in screen.Rows)
        {
            _rows.Add(row);
        }

        VisibleColumns = screen.Columns;
        ShowGrid = screen.ShowGrid;
        EmptyMessage = screen.EmptyMessage;
        Summary = screen.Summary;
        _gridRevision++;
        OnPropertyChanged(nameof(GridRevision));
    }
}
