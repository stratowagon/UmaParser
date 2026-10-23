using System.Collections.ObjectModel;
using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.Mvvm;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.ViewModels;

/// <summary>
/// Skills tab state for the window. The combo box binds to <see cref="Umas"/> and
/// <see cref="SelectedUma"/>. The grid binds to <see cref="Rows"/>. Choosing an uma
/// calls <see cref="SkillsScreen.Select"/>, which asks Core for that uma's report.
/// </summary>
public sealed class SkillsViewModel : ObservableObject
{
    private readonly ObservableCollection<SkillsUmaOption> _umas = new();
    private readonly ObservableCollection<SkillRowViewModel> _rows = new();
    private SkillsScreen _screen = SkillsScreen.Empty;
    private SkillsUmaOption? _selectedUma;
    private string _summary = "Drop one or more .json capture files onto this window.";
    private bool _showing;
    private int _gridRevision;

    public SkillsViewModel()
    {
        Umas = _umas;
        Rows = _rows;
    }

    public ObservableCollection<SkillsUmaOption> Umas { get; }
    public ObservableCollection<SkillRowViewModel> Rows { get; }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>Columns to build in the view. DataGrid columns are not in the visual tree, so the view creates them from this list.</summary>
    public IReadOnlyList<GridColumnDefinition<SkillActivationRow>> VisibleColumns { get; private set; } =
        Array.Empty<GridColumnDefinition<SkillActivationRow>>();

    /// <summary>Bumps when the grid contents are replaced so the view can reset the header sort.</summary>
    public int GridRevision => _gridRevision;

    public SkillsUmaOption? SelectedUma
    {
        get => _selectedUma;
        set
        {
            if (!SetProperty(ref _selectedUma, value))
            {
                return;
            }

            // Show() sets this property while it refills the combo. Ignore that write,
            // and ignore the null the combo sends while the list is being cleared.
            if (_showing || value == null)
            {
                return;
            }

            int index = IndexOf(value);
            if (index < 0 || index == _screen.SelectedIndex)
            {
                return;
            }

            Show(_screen.Select(index), resetUmas: false);
        }
    }

    /// <param name="columnPreferences">
    /// Saved column prefs. The window passes null to read settings.json.
    /// Tests of <see cref="SkillsScreen"/> pass an explicit value and do not come through here.
    /// </param>
    public void Apply(TeamTrialBatch batch, GridColumnGridPreferences? columnPreferences = null)
    {
        GridColumnGridPreferences preferences = columnPreferences ?? SkillsColumnPreferences.Load();
        Show(SkillsScreen.Load(batch, preferences), resetUmas: true);
    }

    private void Show(SkillsScreen screen, bool resetUmas)
    {
        _showing = true;
        try
        {
            _screen = screen;
            if (resetUmas)
            {
                _umas.Clear();
                foreach (SkillsUmaOption uma in screen.Umas)
                {
                    _umas.Add(uma);
                }
            }

            SkillsUmaOption? selected = screen.SelectedIndex >= 0 && screen.SelectedIndex < _umas.Count
                ? _umas[screen.SelectedIndex]
                : null;
            SetProperty(ref _selectedUma, selected, nameof(SelectedUma));

            _rows.Clear();
            foreach (var row in screen.Rows)
            {
                _rows.Add(new SkillRowViewModel(row));
            }

            Summary = screen.Summary.Length > 0
                ? screen.Summary
                : "Drop one or more .json capture files onto this window.";
            VisibleColumns = screen.Columns;
            _gridRevision++;
            OnPropertyChanged(nameof(VisibleColumns));
            OnPropertyChanged(nameof(GridRevision));
        }
        finally
        {
            _showing = false;
        }
    }

    private int IndexOf(SkillsUmaOption uma)
    {
        for (int i = 0; i < _umas.Count; i++)
        {
            if (ReferenceEquals(_umas[i], uma))
            {
                return i;
            }
        }

        return -1;
    }
}
