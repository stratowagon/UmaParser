using System.Collections.ObjectModel;
using System.Globalization;
using UmaParser.Analysis;
using UmaParser.MasterData;
using UmaParser.Mvvm;
using UmaParser.Settings;

namespace UmaParser.ViewModels;

/// <summary>
/// One grid in the Options window. <see cref="DisplayName"/> is the text in the list.
/// </summary>
public sealed class ColumnOptionGridItem
{
    public ColumnOptionGridItem(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    public string Key { get; }

    public string DisplayName { get; }
}

/// <summary>
/// One customizable column. The checkbox binds to <see cref="IsVisible"/>.
/// A column that the current drop cannot show stays at its saved check and ignores a click.
/// </summary>
public sealed class ColumnOptionLine : ObservableObject
{
    private readonly OptionsViewModel _owner;
    private bool _isVisible;
    private bool _isAvailable;
    private bool _suppress;

    internal ColumnOptionLine(OptionsViewModel owner, ColumnOptionRow row)
    {
        _owner = owner;
        Id = row.Id;
        Header = row.Header;
        _isVisible = row.IsVisible;
        _isAvailable = row.IsAvailable;
    }

    public string Id { get; }

    public string Header { get; }

    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetProperty(ref _isAvailable, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            if (_suppress || _owner.TrySetVisible(this, value))
            {
                _isVisible = value;
                OnPropertyChanged();
                return;
            }

            OnPropertyChanged();
        }
    }

    internal void SetState(bool isVisible, bool isAvailable)
    {
        _suppress = true;
        IsVisible = isVisible;
        IsAvailable = isAvailable;
        _suppress = false;
    }
}

/// <summary>
/// Options window state. Edits are kept until <see cref="Save"/>, which writes the same
/// settings.json keys the WinForms Options dialog writes.
/// </summary>
public sealed class OptionsViewModel : ObservableObject
{
    private readonly ColumnOptionsSession _session;
    private readonly Func<GridColumnContext> _context;
    private readonly string? _settingsPath;
    private ColumnOptionGridItem? _selectedGrid;
    private ColumnOptionLine? _selectedColumn;
    private string _lockedColumnText = "";
    private string _description = "";
    private bool _canMoveUp;
    private bool _canMoveDown;
    private bool _suppress;
    private string _sampleSoftThresholdText;
    private string _sampleHardThresholdText;
    private string _sampleThresholdError = "";

    public OptionsViewModel(Func<GridColumnContext>? context = null, string? settingsPath = null)
    {
        _context = context ?? (() => GridColumnContext.Empty);
        _settingsPath = settingsPath;
        GameMasterSettings settings = GameMasterSettings.Load(settingsPath);
        _session = ColumnOptionsSession.Load(settings, _context());
        PerformanceSampleThresholds thresholds = PerformanceSampleThresholds.FromSaved(
            settings.PerformanceSampleSoftThreshold,
            settings.PerformanceSampleHardThreshold);
        _sampleSoftThresholdText = thresholds.Soft.ToString(CultureInfo.CurrentCulture);
        _sampleHardThresholdText = thresholds.Hard.ToString(CultureInfo.CurrentCulture);
        Grids = new ObservableCollection<ColumnOptionGridItem>(
            _session.Grids.Select(grid => new ColumnOptionGridItem(grid.Key, grid.DisplayName)));
        Columns = new ObservableCollection<ColumnOptionLine>();
        _suppress = true;
        _selectedGrid = Grids[0];
        OnPropertyChanged(nameof(SelectedGrid));
        _suppress = false;
        RebuildColumns(_session.SelectedIndex);
    }

    public ObservableCollection<ColumnOptionGridItem> Grids { get; }

    public ObservableCollection<ColumnOptionLine> Columns { get; }

    public event EventHandler? PreferencesApplied;

    public event EventHandler? Closed;

    public ColumnOptionGridItem? SelectedGrid
    {
        get => _selectedGrid;
        set
        {
            if (!SetProperty(ref _selectedGrid, value))
            {
                return;
            }

            if (_suppress || value == null)
            {
                return;
            }

            _session.SelectGrid(value.Key);
            RebuildColumns(_session.SelectedIndex);
        }
    }

    public ColumnOptionLine? SelectedColumn
    {
        get => _selectedColumn;
        set
        {
            if (!SetProperty(ref _selectedColumn, value))
            {
                return;
            }

            if (_suppress)
            {
                return;
            }

            _session.SelectColumn(value == null ? -1 : IndexOf(value));
            PullChrome();
        }
    }

    /// <summary>Locked identity column, for example "Uma — always shown first".</summary>
    public string LockedColumnText
    {
        get => _lockedColumnText;
        private set => SetProperty(ref _lockedColumnText, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public bool CanMoveUp
    {
        get => _canMoveUp;
        private set => SetProperty(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        private set => SetProperty(ref _canMoveDown, value);
    }

    /// <summary>Yellow sample line. Blank on save means 20.</summary>
    public string SampleSoftThresholdText
    {
        get => _sampleSoftThresholdText;
        set => SetProperty(ref _sampleSoftThresholdText, value);
    }

    /// <summary>Red sample line. Blank on save means 10. Has to be lower than the yellow line.</summary>
    public string SampleHardThresholdText
    {
        get => _sampleHardThresholdText;
        set => SetProperty(ref _sampleHardThresholdText, value);
    }

    public string SampleThresholdError
    {
        get => _sampleThresholdError;
        private set => SetProperty(ref _sampleThresholdError, value);
    }

    public void RestoreSampleDefaults()
    {
        SampleSoftThresholdText = PerformanceSampleThresholds.DefaultSoft.ToString(CultureInfo.CurrentCulture);
        SampleHardThresholdText = PerformanceSampleThresholds.DefaultHard.ToString(CultureInfo.CurrentCulture);
        SampleThresholdError = "";
    }

    public void MoveUp() => Move(-1);

    public void MoveDown() => Move(1);

    public void RestoreDefaults()
    {
        _session.RestoreDefaults();
        RebuildColumns(_session.SelectedIndex);
    }

    /// <summary>
    /// Re-reads which columns the open drop can show. Saved checks stay as they are.
    /// </summary>
    public void RefreshAvailability()
    {
        _session.SetContext(_context());
        foreach (ColumnOptionLine line in Columns)
        {
            ColumnOptionRow row = _session.Rows.First(candidate => candidate.Id == line.Id);
            line.SetState(row.IsVisible, row.IsAvailable);
        }

        PullChrome();
    }

    /// <summary>
    /// Writes every grid's working copy onto the settings file and leaves the other keys alone.
    /// </summary>
    public bool Save()
    {
        if (!PerformanceSampleThresholds.TryParse(
                _sampleSoftThresholdText,
                _sampleHardThresholdText,
                out PerformanceSampleThresholds thresholds,
                out string error))
        {
            SampleThresholdError = error;
            return false;
        }

        SampleThresholdError = "";
        GameMasterSettings settings = GameMasterSettings.Load(_settingsPath);
        _session.WriteTo(settings);
        settings.PerformanceSampleSoftThreshold = thresholds.Soft;
        settings.PerformanceSampleHardThreshold = thresholds.Hard;
        settings.Save(_settingsPath);
        PreferencesApplied?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void NotifyClosed() => Closed?.Invoke(this, EventArgs.Empty);

    internal bool TrySetVisible(ColumnOptionLine line, bool visible)
    {
        int index = IndexOf(line);
        if (index < 0 || !_session.TrySetVisible(index, visible))
        {
            return false;
        }

        if (!ReferenceEquals(_selectedColumn, line))
        {
            _suppress = true;
            _selectedColumn = line;
            OnPropertyChanged(nameof(SelectedColumn));
            _suppress = false;
            _session.SelectColumn(index);
        }

        PullChrome();
        return true;
    }

    private void Move(int delta)
    {
        if (!_session.TryMoveSelected(delta))
        {
            return;
        }

        RebuildColumns(_session.SelectedIndex);
    }

    private void RebuildColumns(int selectedIndex)
    {
        _suppress = true;
        Columns.Clear();
        foreach (ColumnOptionRow row in _session.Rows)
        {
            Columns.Add(new ColumnOptionLine(this, row));
        }

        _selectedColumn = selectedIndex >= 0 && selectedIndex < Columns.Count
            ? Columns[selectedIndex]
            : null;
        OnPropertyChanged(nameof(SelectedColumn));
        _suppress = false;
        PullChrome();
    }

    private void PullChrome()
    {
        LockedColumnText = _session.LockedColumnText;
        Description = _session.Description;
        CanMoveUp = _session.CanMoveUp;
        CanMoveDown = _session.CanMoveDown;
    }

    private int IndexOf(ColumnOptionLine line)
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            if (ReferenceEquals(Columns[i], line))
            {
                return i;
            }
        }

        return -1;
    }
}
