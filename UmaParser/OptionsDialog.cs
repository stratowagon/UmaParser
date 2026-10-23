using UmaParser.MasterData;
using UmaParser.Settings;

namespace UmaParser;

internal sealed class OptionsDialog : Form
{
    private readonly Func<GridColumnContext> _getContext;
    private readonly ColumnOptionsSession _session;
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
    private bool _suppressColumnEvents;

    public OptionsDialog(Func<GridColumnContext> getContext)
    {
        _getContext = getContext;
        _session = ColumnOptionsSession.Load(GameMasterSettings.Load(), getContext());

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
        foreach (GridColumnOptionsGrid grid in _session.Grids)
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
        _columnList.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressColumnEvents)
            {
                return;
            }

            _session.SelectColumn(_columnList.SelectedIndex);
            UpdateDetails();
        };
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
        _session.SetContext(_getContext());
        UpdateDetails();
    }

    private void ShowSelectedGrid()
    {
        if (_suppressColumnEvents)
        {
            return;
        }

        if (_gridList.SelectedItem is not GridColumnOptionsGrid grid)
        {
            return;
        }

        _session.SelectGrid(grid.Key);
        RenderColumns();
    }

    private void RenderColumns()
    {
        _suppressColumnEvents = true;
        _columnList.Items.Clear();
        foreach (ColumnOptionRow row in _session.Rows)
        {
            GridColumnOptionsEntry entry = _session.CurrentGrid.Columns.First(column => column.Id == row.Id);
            int index = _columnList.Items.Add(entry);
            _columnList.SetItemChecked(index, row.IsVisible);
        }

        if (_columnList.Items.Count > 0)
        {
            _columnList.SelectedIndex = _session.SelectedIndex;
        }

        _suppressColumnEvents = false;
        UpdateDetails();
    }

    private void ColumnList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_suppressColumnEvents)
        {
            return;
        }

        bool visible = e.NewValue == CheckState.Checked;
        if (!_session.TrySetVisible(e.Index, visible))
        {
            e.NewValue = e.CurrentValue;
            return;
        }

        BeginInvoke(new Action(UpdateDetails));
    }

    private void MoveSelectedColumn(int delta)
    {
        if (!_session.TryMoveSelected(delta))
        {
            return;
        }

        RenderColumns();
    }

    private void RestoreDefaultsForCurrentGrid()
    {
        _session.RestoreDefaults();
        RenderColumns();
    }

    private void UpdateDetails()
    {
        _lockedColumnLabel.Text = _session.LockedColumnText;
        _descriptionBox.Text = _session.Description;
        _moveUpButton.Enabled = _session.CanMoveUp;
        _moveDownButton.Enabled = _session.CanMoveDown;
    }

    private bool SavePreferences()
    {
        var settings = GameMasterSettings.Load();
        _session.WriteTo(settings);
        settings.Save();
        PreferencesApplied?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
