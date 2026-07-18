using UmaParser.MasterData;
using UmaParser.Settings;
using UmaParser.Ui;

namespace UmaParser;

public sealed class GridColumnOptionsGrid
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<GridColumnOptionsEntry> Columns { get; init; }
}

public sealed class GridColumnOptionsEntry
{
    public required string Id { get; init; }
    public required string Header { get; init; }
    public required string Tooltip { get; init; }
    public required string Description { get; init; }
    public bool IsLocked { get; init; }
    public required Func<GridColumnContext, bool> IsAvailable { get; init; }
    public required Func<GridColumnContext, string?> UnavailableReason { get; init; }

    public override string ToString() => Header;
}

public sealed class OptionsDialog : Form
{
    private readonly IReadOnlyList<GridColumnOptionsGrid> _grids;
    private readonly Func<GridColumnContext> _getContext;
    private readonly Dictionary<string, GridColumnGridPreferences> _workingPrefs;
    private readonly ListBox _gridList;
    private readonly Label _lockedColumnLabel;
    private readonly CheckedListBox _columnList;
    private readonly Button _moveUpButton;
    private readonly Button _moveDownButton;
    private readonly TextBox _descriptionBox;
    private readonly Button _restoreDefaultsButton;
    private readonly Button _okButton;
    private readonly Button _cancelButton;
    private readonly Button _applyButton;
    private string? _currentGridKey;
    private bool _suppressColumnEvents;

    public OptionsDialog(
        IReadOnlyList<GridColumnOptionsGrid> grids,
        Func<GridColumnContext> getContext)
    {
        _grids = grids;
        _getContext = getContext;
        _workingPrefs = LoadWorkingPreferences(grids);

        Text = "Options";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 480);
        MinimumSize = new Size(640, 480);

        var tabControl = new TabControl { Dock = DockStyle.Fill };
        var columnsTab = new TabPage("Columns") { Padding = new Padding(8) };
        tabControl.TabPages.Add(columnsTab);
        Controls.Add(tabControl);

        var columnsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        columnsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        columnsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        columnsTab.Controls.Add(columnsLayout);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 140,
            FixedPanel = FixedPanel.Panel1,
        };
        columnsLayout.Controls.Add(split, 0, 0);

        _gridList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (var grid in _grids)
        {
            _gridList.Items.Add(grid);
        }

        _gridList.DisplayMember = nameof(GridColumnOptionsGrid.DisplayName);
        _gridList.SelectedIndexChanged += (_, _) => ShowSelectedGrid();
        split.Panel1.Controls.Add(_gridList);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        split.Panel2.Controls.Add(rightPanel);

        _lockedColumnLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        rightPanel.Controls.Add(_lockedColumnLabel, 0, 0);

        _columnList = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
        };
        _columnList.SelectedIndexChanged += (_, _) => UpdateDescription();
        _columnList.ItemCheck += ColumnList_ItemCheck;
        rightPanel.Controls.Add(_columnList, 0, 1);

        var reorderPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        _moveUpButton = new Button { Text = "Move Up", AutoSize = true };
        _moveDownButton = new Button { Text = "Move Down", AutoSize = true };
        _restoreDefaultsButton = new Button { Text = "Restore Defaults", AutoSize = true };
        _moveUpButton.Click += (_, _) => MoveSelectedColumn(-1);
        _moveDownButton.Click += (_, _) => MoveSelectedColumn(1);
        _restoreDefaultsButton.Click += (_, _) => RestoreDefaultsForCurrentGrid();
        reorderPanel.Controls.AddRange([_moveUpButton, _moveDownButton, _restoreDefaultsButton]);
        rightPanel.Controls.Add(reorderPanel, 0, 2);

        var descriptionPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        descriptionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        descriptionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var descriptionCaption = new Label
        {
            Text = "Description",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
        };
        _descriptionBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.Control,
        };
        descriptionPanel.Controls.Add(descriptionCaption, 0, 0);
        descriptionPanel.Controls.Add(_descriptionBox, 0, 1);
        rightPanel.Controls.Add(descriptionPanel, 0, 3);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            AutoSize = true,
        };
        _okButton = new Button { Text = "OK", DialogResult = DialogResult.None, AutoSize = true };
        _cancelButton = new Button { Text = "Cancel", AutoSize = true };
        _applyButton = new Button { Text = "Apply", AutoSize = true };
        _okButton.Click += (_, _) =>
        {
            if (SavePreferences())
            {
                Close();
            }
        };
        _cancelButton.Click += (_, _) => Close();
        _applyButton.Click += (_, _) => SavePreferences();
        buttonPanel.Controls.AddRange([_okButton, _cancelButton, _applyButton]);
        columnsLayout.Controls.Add(buttonPanel, 0, 1);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
        };

        if (_gridList.Items.Count > 0)
        {
            _gridList.SelectedIndex = 0;
        }
    }

    public event EventHandler? PreferencesApplied;

    public void RefreshAvailability()
    {
        if (_currentGridKey != null)
        {
            UpdateColumnAvailability();
            UpdateDescription();
        }
    }

    private static Dictionary<string, GridColumnGridPreferences> LoadWorkingPreferences(
        IReadOnlyList<GridColumnOptionsGrid> grids)
    {
        var settings = GameMasterSettings.Load();
        var result = new Dictionary<string, GridColumnGridPreferences>(StringComparer.Ordinal);

        foreach (var grid in grids)
        {
            var defaultOrder = grid.Columns.Where(c => !c.IsLocked).Select(c => c.Id).ToList();
            var saved = settings.GetGridColumnPreferences(grid.Key);
            result[grid.Key] = GridColumnPreferencesMerger.Merge(defaultOrder, saved);
        }

        return result;
    }

    private GridColumnOptionsGrid? GetSelectedGrid() =>
        _gridList.SelectedItem as GridColumnOptionsGrid;

    private void ShowSelectedGrid()
    {
        SaveCurrentGridStateToWorking();

        var grid = GetSelectedGrid();
        if (grid == null)
        {
            return;
        }

        _currentGridKey = grid.Key;
        var locked = grid.Columns.FirstOrDefault(c => c.IsLocked);
        _lockedColumnLabel.Text = locked == null
            ? string.Empty
            : $"{locked.Header} — always shown first";

        _suppressColumnEvents = true;
        _columnList.Items.Clear();
        var prefs = _workingPrefs[grid.Key];
        var order = prefs.Order;
        var hidden = new HashSet<string>(prefs.Hidden, StringComparer.Ordinal);

        foreach (string id in order)
        {
            var entry = grid.Columns.First(c => c.Id == id);
            int index = _columnList.Items.Add(entry);
            _columnList.SetItemChecked(index, !hidden.Contains(id));
        }

        _suppressColumnEvents = false;
        if (_columnList.Items.Count > 0)
        {
            _columnList.SelectedIndex = 0;
        }

        UpdateColumnAvailability();
        UpdateDescription();
    }

    private void SaveCurrentGridStateToWorking()
    {
        if (_currentGridKey == null)
        {
            return;
        }

        var order = new List<string>();
        var hidden = new List<string>();
        for (int i = 0; i < _columnList.Items.Count; i++)
        {
            if (_columnList.Items[i] is not GridColumnOptionsEntry entry)
            {
                continue;
            }

            order.Add(entry.Id);
            if (!_columnList.GetItemChecked(i))
            {
                hidden.Add(entry.Id);
            }
        }

        _workingPrefs[_currentGridKey] = new GridColumnGridPreferences
        {
            Order = order,
            Hidden = hidden,
        };
    }

    private void ColumnList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_suppressColumnEvents)
        {
            return;
        }

        if (_columnList.Items[e.Index] is GridColumnOptionsEntry entry
            && !entry.IsAvailable(_getContext()))
        {
            e.NewValue = e.CurrentValue;
            return;
        }

        BeginInvoke(() =>
        {
            UpdateColumnAvailability();
            UpdateDescription();
        });
    }

    private void MoveSelectedColumn(int delta)
    {
        int index = _columnList.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        int newIndex = index + delta;
        if (newIndex < 0 || newIndex >= _columnList.Items.Count)
        {
            return;
        }

        var entry = _columnList.Items[index];
        bool checkedState = _columnList.GetItemChecked(index);

        _suppressColumnEvents = true;
        _columnList.Items.RemoveAt(index);
        _columnList.Items.Insert(newIndex, entry);
        _columnList.SetItemChecked(newIndex, checkedState);
        _columnList.SelectedIndex = newIndex;
        _suppressColumnEvents = false;

        UpdateColumnAvailability();
        UpdateDescription();
    }

    private void RestoreDefaultsForCurrentGrid()
    {
        var grid = GetSelectedGrid();
        if (grid == null)
        {
            return;
        }

        _workingPrefs[grid.Key] = new GridColumnGridPreferences
        {
            Order = grid.Columns.Where(c => !c.IsLocked).Select(c => c.Id).ToList(),
            Hidden = new List<string>(),
        };

        ShowSelectedGrid();
    }

    private void UpdateColumnAvailability()
    {
        int selected = _columnList.SelectedIndex;
        bool canMove = selected >= 0
            && _columnList.Items[selected] is GridColumnOptionsEntry selectedEntry
            && selectedEntry.IsAvailable(_getContext());

        _moveUpButton.Enabled = canMove && selected > 0
            && _columnList.Items[selected - 1] is GridColumnOptionsEntry prev
            && prev.IsAvailable(_getContext());
        _moveDownButton.Enabled = canMove && selected >= 0 && selected < _columnList.Items.Count - 1
            && _columnList.Items[selected + 1] is GridColumnOptionsEntry next
            && next.IsAvailable(_getContext());
    }

    private void UpdateDescription()
    {
        if (_columnList.SelectedItem is not GridColumnOptionsEntry entry)
        {
            _descriptionBox.Text = string.Empty;
            return;
        }

        var context = _getContext();
        var lines = new List<string> { entry.Description };
        if (!string.IsNullOrWhiteSpace(entry.Tooltip) && entry.Tooltip != entry.Description)
        {
            lines.Add(string.Empty);
            lines.Add($"Tooltip: {entry.Tooltip}");
        }

        string? reason = entry.UnavailableReason(context);
        if (!entry.IsAvailable(context) && !string.IsNullOrWhiteSpace(reason))
        {
            lines.Add(string.Empty);
            lines.Add($"Not available: {reason}");
        }

        _descriptionBox.Text = string.Join(Environment.NewLine, lines);
        UpdateColumnAvailability();
    }

    private bool SavePreferences()
    {
        SaveCurrentGridStateToWorking();

        var settings = GameMasterSettings.Load();
        foreach (var (gridKey, prefs) in _workingPrefs)
        {
            settings.SetGridColumnPreferences(gridKey, prefs);
        }

        settings.Save();
        PreferencesApplied?.Invoke(this, EventArgs.Empty);
        return true;
    }
}