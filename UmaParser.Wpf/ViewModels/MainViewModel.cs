using System.Diagnostics;
using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.Mvvm;
using UmaParser.Presentation;

namespace UmaParser.ViewModels;

/// <summary>
/// The window. <see cref="Tabs"/> is the list of screens, in tab order.
/// A file drop calls <see cref="LoadFiles"/>, which uses the same import path as WinForms:
/// <see cref="CaptureImportService.TryImportPath"/> then <see cref="TeamTrialBatchBuilder.Build"/>.
/// That one batch is handed to every screen.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    public const int ResultsTabIndex = 0;
    public const int AnalysisTabIndex = 1;
    public const int SkillsTabIndex = 2;
    public const int TracksTabIndex = 3;

    private string _statusText;
    private int _selectedTabIndex;

    public MainViewModel()
    {
        Results = new ResultsViewModel();
        Analysis = new AnalysisViewModel();
        Skills = new SkillsViewModel();
        Tracks = new TracksViewModel();
        Tabs =
        [
            new ScreenTab { Title = "Results", Content = Results },
            new ScreenTab { Title = "Team Analysis", Content = Analysis },
            new ScreenTab { Title = "Skills", Content = Skills },
            new ScreenTab { Title = "Tracks", Content = Tracks },
        ];
        LoadFilesCommand = new RelayCommand(LoadFiles);
        _statusText = GameMasterService.Current.GetStatusLine();
    }

    public ResultsViewModel Results { get; }
    public AnalysisViewModel Analysis { get; }
    public SkillsViewModel Skills { get; }
    public TracksViewModel Tracks { get; }

    /// <summary>Bound to the window's tab control. Order matches WinForms: Results, Team Analysis, Skills, Tracks.</summary>
    public IReadOnlyList<ScreenTab> Tabs { get; }

    public RelayCommand LoadFilesCommand { get; }

    /// <summary>
    /// The open tab. A drop selects the same tab WinForms selects:
    /// Team Analysis for a uniform roster, Results for a mismatch, Skills for anything else with data.
    /// </summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private void LoadFiles(object? parameter)
    {
        if (parameter is not IEnumerable<string> dropped)
        {
            return;
        }

        var paths = new List<string>();
        foreach (string path in dropped)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            return;
        }

        // Each dropped path is imported on its own. A folder is not walked: WinForms does not
        // walk folders either, and Core reports a path that is not a .json file as skipped.
        long started = Stopwatch.GetTimestamp();
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(paths.Select(CaptureImportService.TryImportPath));
        double importMs = ElapsedMs(started);
        if (!batch.HasAnyData)
        {
            StatusText = DropStatusText.NoneLoaded(batch.SkippedFileCount);
            Trace.WriteLine($"UmaParser load: files={paths.Count} import={importMs:0.0} ms, no data");
            return;
        }

        // One parse of the sim blobs, shared by Skills and Tracks. Results and Team Analysis
        // do not read the blobs. A uniform Team Trials drop does not parse them at all.
        started = Stopwatch.GetTimestamp();
        NonTeamTrialLoad? singleRaceLoad = batch.Kind == TeamTrialBatchKind.MixedOrNonTT
            ? NonTeamTrialLoad.Collect(batch.SingleRaceImports)
            : null;
        double parseMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Results.Apply(batch);
        double resultsMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Analysis.Apply(batch);
        double analysisMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Skills.Apply(batch, singleRaceLoad: singleRaceLoad);
        double skillsMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Tracks.Apply(batch, singleRaceLoad: singleRaceLoad);
        double tracksMs = ElapsedMs(started);

        // Results/Analysis include the grid rebuild when that tab's view is already loaded.
        // Import and the single-race parse are the part a tab switch does not repeat.
        Trace.WriteLine(
            $"UmaParser load: files={paths.Count} kind={batch.Kind} import={importMs:0.0} ms, parse={parseMs:0.0} ms, results={resultsMs:0.0} ms, analysis={analysisMs:0.0} ms, skills={skillsMs:0.0} ms, tracks={tracksMs:0.0} ms");
        StatusText = batch.Kind == TeamTrialBatchKind.RosterStats && Analysis.Summary.Length > 0
            ? Analysis.Summary
            : DropStatusText.For(batch, singleRaceLoad);
        SelectedTabIndex = batch.Kind switch
        {
            TeamTrialBatchKind.RosterStats => AnalysisTabIndex,
            TeamTrialBatchKind.RosterMismatch => ResultsTabIndex,
            TeamTrialBatchKind.MixedOrNonTT => SkillsTabIndex,
            _ => SelectedTabIndex,
        };
    }

    private static double ElapsedMs(long started) =>
        Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
