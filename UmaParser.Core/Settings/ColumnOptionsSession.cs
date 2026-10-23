using UmaParser.MasterData;

namespace UmaParser.Settings;

/// <summary>
/// Working copy of the Options dialog. Edits stay here until <see cref="WriteTo"/>.
/// WinForms and the WPF options view model both drive this, so a check, a move,
/// and Restore Defaults do the same thing in both apps.
/// </summary>
internal sealed class ColumnOptionsSession
{
    private readonly Dictionary<string, GridColumnGridPreferences> _working;
    private GridColumnContext _context;
    private string _currentGridKey;
    private int _selectedIndex;

    private ColumnOptionsSession(
        IReadOnlyList<GridColumnOptionsGrid> grids,
        Dictionary<string, GridColumnGridPreferences> working,
        GridColumnContext context,
        string currentGridKey)
    {
        Grids = grids;
        _working = working;
        _context = context;
        _currentGridKey = currentGridKey;
        _selectedIndex = 0;
    }

    public IReadOnlyList<GridColumnOptionsGrid> Grids { get; }

    public string CurrentGridKey => _currentGridKey;

    public int SelectedIndex => _selectedIndex;

    public GridColumnOptionsGrid CurrentGrid =>
        Grids.First(grid => grid.Key == _currentGridKey);

    public string LockedColumnText => ColumnOptionsCatalog.LockedColumnText(CurrentGrid);

    public bool CanMoveUp => CanMove(-1);

    public bool CanMoveDown => CanMove(1);

    public string Description
    {
        get
        {
            if (_selectedIndex < 0 || _selectedIndex >= CurrentPreferences.Order.Count)
            {
                return string.Empty;
            }

            return ColumnOptionsCatalog.Description(EntryAt(_selectedIndex), _context);
        }
    }

    public IReadOnlyList<ColumnOptionRow> Rows
    {
        get
        {
            GridColumnGridPreferences prefs = CurrentPreferences;
            var hidden = new HashSet<string>(prefs.Hidden, StringComparer.Ordinal);
            var rows = new List<ColumnOptionRow>(prefs.Order.Count);
            for (int i = 0; i < prefs.Order.Count; i++)
            {
                GridColumnOptionsEntry entry = EntryAt(i);
                rows.Add(new ColumnOptionRow(
                    entry.Id,
                    entry.Header,
                    IsVisible: !hidden.Contains(entry.Id),
                    IsAvailable: entry.IsAvailable(_context)));
            }

            return rows;
        }
    }

    private GridColumnGridPreferences CurrentPreferences => _working[_currentGridKey];

    public static ColumnOptionsSession Load(GameMasterSettings settings, GridColumnContext context)
    {
        IReadOnlyList<GridColumnOptionsGrid> grids = ColumnOptionsCatalog.All;
        var working = new Dictionary<string, GridColumnGridPreferences>(StringComparer.Ordinal);
        foreach (GridColumnOptionsGrid grid in grids)
        {
            List<string> defaultOrder = grid.Columns.Where(column => !column.IsLocked).Select(column => column.Id).ToList();
            working[grid.Key] = Clone(SavedGridColumns.Load(grid.Key, defaultOrder, settings));
        }

        return new ColumnOptionsSession(grids, working, context, grids[0].Key);
    }

    public void SetContext(GridColumnContext context) => _context = context;

    public void SelectGrid(string key)
    {
        if (key == _currentGridKey || !_working.ContainsKey(key))
        {
            return;
        }

        _currentGridKey = key;
        _selectedIndex = 0;
    }

    public void SelectColumn(int index)
    {
        if (index < 0 || index >= CurrentPreferences.Order.Count)
        {
            _selectedIndex = -1;
            return;
        }

        _selectedIndex = index;
    }

    /// <summary>Returns false when the column cannot be shown or hidden in the current context.</summary>
    public bool TrySetVisible(int index, bool visible)
    {
        GridColumnGridPreferences prefs = CurrentPreferences;
        if (index < 0 || index >= prefs.Order.Count)
        {
            return false;
        }

        if (!EntryAt(index).IsAvailable(_context))
        {
            return false;
        }

        string id = prefs.Order[index];
        var hidden = new HashSet<string>(prefs.Hidden, StringComparer.Ordinal);
        if (visible)
        {
            hidden.Remove(id);
        }
        else
        {
            hidden.Add(id);
        }

        prefs.Hidden = prefs.Order.Where(hidden.Contains).ToList();
        return true;
    }

    public bool TryMoveSelected(int delta)
    {
        if (!CanMove(delta))
        {
            return false;
        }

        int index = _selectedIndex;
        int target = index + delta;
        GridColumnGridPreferences prefs = CurrentPreferences;
        (prefs.Order[index], prefs.Order[target]) = (prefs.Order[target], prefs.Order[index]);
        _selectedIndex = target;
        return true;
    }

    public void RestoreDefaults()
    {
        GridColumnOptionsGrid grid = CurrentGrid;
        _working[grid.Key] = new GridColumnGridPreferences
        {
            Order = grid.Columns.Where(column => !column.IsLocked).Select(column => column.Id).ToList(),
            Hidden = [],
        };
        _selectedIndex = 0;
    }

    public void WriteTo(GameMasterSettings settings)
    {
        foreach (var (gridKey, prefs) in _working)
        {
            settings.SetGridColumnPreferences(gridKey, Clone(prefs));
        }
    }

    public GridColumnGridPreferences PreferencesFor(string gridKey) => Clone(_working[gridKey]);

    private bool CanMove(int delta)
    {
        int index = _selectedIndex;
        int target = index + delta;
        GridColumnGridPreferences prefs = CurrentPreferences;
        if (index < 0 || target < 0 || target >= prefs.Order.Count)
        {
            return false;
        }

        return EntryAt(index).IsAvailable(_context) && EntryAt(target).IsAvailable(_context);
    }

    private GridColumnOptionsEntry EntryAt(int index)
    {
        string id = CurrentPreferences.Order[index];
        return CurrentGrid.Columns.First(column => column.Id == id);
    }

    private static GridColumnGridPreferences Clone(GridColumnGridPreferences prefs) =>
        new()
        {
            Order = prefs.Order.ToList(),
            Hidden = prefs.Hidden.ToList(),
        };
}
