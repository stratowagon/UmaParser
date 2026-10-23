using System.Collections.ObjectModel;
using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.Mvvm;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.ViewModels;

/// <summary>
/// Tracks tab state for the window. The combo box binds to <see cref="Umas"/> and
/// <see cref="SelectedUma"/>. The grid binds to <see cref="Rows"/>, which already
/// includes the total row. Choosing an uma calls <see cref="TracksScreen.Select"/>.
/// </summary>
public sealed class TracksViewModel : ObservableObject
{
    /// <summary>Shown before any files are dropped. A later drop replaces it, even with a blank line.</summary>
    public const string DropPrompt = "Drop one or more .json capture files onto this window.";

    private readonly ObservableCollection<TracksUmaOption> _umas = new();
    private TracksScreen _screen = TracksScreen.Empty;
    private TracksUmaOption? _selectedUma;
    private string _summary = DropPrompt;
    private bool _showing;
    private int _gridRevision;

    public TracksViewModel()
    {
        Umas = _umas;
        Rows = new ObservableCollection<TracksGridRow>();
    }

    public ObservableCollection<TracksUmaOption> Umas { get; }
    public ObservableCollection<TracksGridRow> Rows { get; private set; }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>Columns to build in the view. DataGrid columns are not in the visual tree, so the view creates them from this list.</summary>
    public IReadOnlyList<GridColumnDefinition<TracksRow>> VisibleColumns { get; private set; } =
        Array.Empty<GridColumnDefinition<TracksRow>>();

    /// <summary>Bumps when the grid contents are replaced so the view can reset the header sort to Track, ascending.</summary>
    public int GridRevision => _gridRevision;

    public TracksUmaOption? SelectedUma
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
    /// Tests pass an explicit value and do not read the settings file.
    /// </param>
    /// <param name="singleRaceLoad">
    /// The load <see cref="MainViewModel"/> already built for this drop, shared with Skills.
    /// </param>
    public void Apply(
        TeamTrialBatch batch,
        GridColumnGridPreferences? columnPreferences = null,
        NonTeamTrialLoad? singleRaceLoad = null)
    {
        GridColumnGridPreferences preferences = columnPreferences ?? SavedGridColumns.ForTracks();
        Show(TracksScreen.Load(batch, preferences, singleRaceLoad), resetUmas: true);
    }

    /// <summary>True when the loaded drop is Team Trials, including a roster mismatch.</summary>
    public bool IsTeamTrial => _screen.IsTeamTrial;

    /// <summary>
    /// Rebuilds columns for the uma already selected. Does nothing before the first drop,
    /// and does not read the capture files again.
    /// </summary>
    public void ReloadColumns(GridColumnGridPreferences? columnPreferences = null)
    {
        if (ReferenceEquals(_screen, TracksScreen.Empty))
        {
            return;
        }

        Show(_screen.WithPreferences(columnPreferences ?? SavedGridColumns.ForTracks()), resetUmas: false);
    }

    private void Show(TracksScreen screen, bool resetUmas)
    {
        _showing = true;
        try
        {
            _screen = screen;
            if (resetUmas)
            {
                _umas.Clear();
                foreach (TracksUmaOption uma in screen.Umas)
                {
                    _umas.Add(uma);
                }
            }

            TracksUmaOption? selected = screen.SelectedIndex >= 0 && screen.SelectedIndex < _umas.Count
                ? _umas[screen.SelectedIndex]
                : null;
            SetProperty(ref _selectedUma, selected, nameof(SelectedUma));

            Rows = new ObservableCollection<TracksGridRow>(screen.GridRows);
            OnPropertyChanged(nameof(Rows));

            // A blank summary is real. WinForms clears the line when a non-TT drop has no local umas.
            // The drop hint is only the text set before the first Apply.
            Summary = screen.Summary;
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

    private int IndexOf(TracksUmaOption uma)
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
