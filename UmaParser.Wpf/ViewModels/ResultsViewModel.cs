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
    private IReadOnlyList<ResultsColumn> _columns = Array.Empty<ResultsColumn>();
    private bool _showGrid;
    private int _gridRevision;
    private int _repaintRequest;
    private ColorTheme _theme = ColorTheme.Light;

    public ResultsViewModel(ColorTheme theme = ColorTheme.Light)
    {
        _theme = theme;
        Rows = new ObservableCollection<ResultsGridRow>();
        Message = ResultsScreen.Welcome;
    }

    public ObservableCollection<ResultsGridRow> Rows { get; private set; }

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

    /// <summary>
    /// Bumps when the sheet should paint again without rebuilding columns or moving the scroll position.
    /// Column options do not change this grid. The sheet still repaints so an open Results tab refreshes with the others.
    /// </summary>
    public int RepaintRequest => _repaintRequest;

    public void RequestRepaint()
    {
        _repaintRequest++;
        OnPropertyChanged(nameof(RepaintRequest));
    }

    public void Apply(TeamTrialBatch batch, ColorTheme theme = ColorTheme.Light)
    {
        _theme = theme;
        ResultsScreen screen = ResultsScreen.Load(batch, theme);
        Columns = screen.Columns;
        ShowGrid = screen.ShowGrid;
        // One notification for the whole grid. Per-row adds would refresh the DataGrid
        // once per uma while the file columns are still the previous drop.
        Rows = new ObservableCollection<ResultsGridRow>(screen.Rows);
        OnPropertyChanged(nameof(Rows));
        _gridRevision++;
        OnPropertyChanged(nameof(GridRevision));
    }

    /// <summary>
    /// Recolors score cells for <paramref name="theme"/> and asks the sheet to paint again.
    /// The row collection, columns, and scroll position stay put.
    /// </summary>
    public void ApplyTheme(ColorTheme theme)
    {
        if (_theme == theme)
        {
            return;
        }

        _theme = theme;
        ResultsScreen.ApplyHeatTheme(Rows, theme);
        RequestRepaint();
    }
}
