using System.Collections.ObjectModel;
using UmaParser.Import;
using UmaParser.Mvvm;
using UmaParser.Presentation;

namespace UmaParser.ViewModels;

/// <summary>
/// Results tab state. A uniform roster or a roster mismatch fills <see cref="Rows"/>.
/// A single-race drop, a mix with Team Trials, or an empty window leaves the welcome text up.
/// Columns are one per file. The view builds them when <see cref="GridRevision"/> changes.
/// </summary>
public sealed class ResultsViewModel : ObservableObject
{
    private readonly ObservableCollection<ResultsGridRow> _rows = new();
    private IReadOnlyList<ResultsColumn> _columns = Array.Empty<ResultsColumn>();
    private bool _showGrid;
    private int _gridRevision;

    public ResultsViewModel()
    {
        Rows = _rows;
        Message = ResultsScreen.Welcome;
    }

    public ObservableCollection<ResultsGridRow> Rows { get; }

    /// <summary>Welcome paragraph. Shown when <see cref="ShowGrid"/> is false.</summary>
    public string Message { get; }

    public bool ShowGrid
    {
        get => _showGrid;
        private set => SetProperty(ref _showGrid, value);
    }

    public IReadOnlyList<ResultsColumn> Columns
    {
        get => _columns;
        private set => SetProperty(ref _columns, value);
    }

    /// <summary>Bumps when the grid is replaced so the view can rebuild the file columns.</summary>
    public int GridRevision => _gridRevision;

    public void Apply(TeamTrialBatch batch)
    {
        ResultsScreen screen = ResultsScreen.Load(batch);
        _rows.Clear();
        foreach (ResultsGridRow row in screen.Rows)
        {
            _rows.Add(row);
        }

        Columns = screen.Columns;
        ShowGrid = screen.ShowGrid;
        _gridRevision++;
        OnPropertyChanged(nameof(GridRevision));
    }
}
