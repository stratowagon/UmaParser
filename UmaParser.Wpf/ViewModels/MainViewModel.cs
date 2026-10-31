using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.Mvvm;
using UmaParser.Presentation;
using UmaParser.Services;
using UmaParser.Settings;
using UmaParser.Themes;

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
    public const int PerformanceTabIndex = 2;
    public const int SkillsTabIndex = 3;
    public const int TracksTabIndex = 4;

    private readonly IShellServices _shell;
    private OptionsViewModel? _options;
    private string _statusText;
    private int _selectedTabIndex;

    internal MainViewModel(IShellServices shell)
    {
        _shell = shell;
        Results = new ResultsViewModel(AppTheme.Current);
        Analysis = new AnalysisViewModel();
        Performance = new PerformanceViewModel();
        Skills = new SkillsViewModel();
        Tracks = new TracksViewModel();
        Tabs =
        [
            new ScreenTab { Title = "Results", Content = Results },
            new ScreenTab { Title = "Team Analysis", Content = Analysis },
            new ScreenTab { Title = "Performance", Content = Performance },
            new ScreenTab { Title = "Skills", Content = Skills },
            new ScreenTab { Title = "Tracks", Content = Tracks },
        ];
        LoadFilesCommand = new RelayCommand(LoadFiles);
        OpenPreferencesCommand = new RelayCommand(OpenPreferences);
        ShowMasterDataStatusCommand = new RelayCommand(ShowMasterDataStatus);
        RefreshMasterDataCommand = new RelayCommand(RefreshMasterData);
        UseDefaultMasterDataCommand = new RelayCommand(UseDefaultMasterData);
        BrowseMasterDataCommand = new RelayCommand(BrowseMasterData);
        OpenMasterDataFolderCommand = new RelayCommand(OpenMasterDataFolder);
        Skills.PropertyChanged += OnUmaSelectionChanged;
        Tracks.PropertyChanged += OnUmaSelectionChanged;
        AppTheme.Changed += (_, _) => Results.ApplyTheme(AppTheme.Current);
        _statusText = GameMasterService.Current.GetStatusLine();
    }

    /// <summary>View menu. Saves System, Light, or Dark and swaps the WPF theme without a restart.</summary>
    public void SetColorMode(ColorMode mode) => AppTheme.SetPreference(mode);

    public ResultsViewModel Results { get; }
    public AnalysisViewModel Analysis { get; }
    public PerformanceViewModel Performance { get; }
    public SkillsViewModel Skills { get; }
    public TracksViewModel Tracks { get; }

    /// <summary>Bound to the window's tab control. Order: Results, Team Analysis, Performance, Skills, Tracks.</summary>
    public IReadOnlyList<ScreenTab> Tabs { get; }

    public RelayCommand LoadFilesCommand { get; }

    public RelayCommand OpenPreferencesCommand { get; }

    public RelayCommand ShowMasterDataStatusCommand { get; }

    public RelayCommand RefreshMasterDataCommand { get; }

    public RelayCommand UseDefaultMasterDataCommand { get; }

    public RelayCommand BrowseMasterDataCommand { get; }

    public RelayCommand OpenMasterDataFolderCommand { get; }

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
        Results.Apply(batch, AppTheme.Current);
        double resultsMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Analysis.Apply(batch);
        double analysisMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Performance.Apply(batch);
        double performanceMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Skills.Apply(batch, singleRaceLoad: singleRaceLoad);
        double skillsMs = ElapsedMs(started);

        started = Stopwatch.GetTimestamp();
        Tracks.Apply(batch, singleRaceLoad: singleRaceLoad);
        double tracksMs = ElapsedMs(started);

        // Results/Analysis include the grid rebuild when that tab's view is already loaded.
        // Import and the single-race parse are the part a tab switch does not repeat.
        Trace.WriteLine(
            $"UmaParser load: files={paths.Count} kind={batch.Kind} import={importMs:0.0} ms, parse={parseMs:0.0} ms, results={resultsMs:0.0} ms, analysis={analysisMs:0.0} ms, performance={performanceMs:0.0} ms, skills={skillsMs:0.0} ms, tracks={tracksMs:0.0} ms");
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
        _options?.RefreshAvailability();
    }

    private void OpenPreferences(object? parameter)
    {
        if (_options != null)
        {
            _options.RefreshAvailability();
            _shell.ActivateOptions();
            return;
        }

        _options = new OptionsViewModel(ColumnContext);
        _options.PreferencesApplied += (_, _) => OnPreferencesApplied();
        _options.Closed += (_, _) => _options = null;
        _shell.ShowOptions(_options);
    }

    private void OnPreferencesApplied()
    {
        Analysis.ReloadColumns();
        Skills.ReloadColumns();
        Tracks.ReloadColumns();
        Results.RequestRepaint();
        _options?.RefreshAvailability();
    }

    private void OnUmaSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillsViewModel.SelectedUma))
        {
            _options?.RefreshAvailability();
        }
    }

    private GridColumnContext ColumnContext() =>
        new()
        {
            IsTeamTrial = Skills.IsTeamTrial || Tracks.IsTeamTrial,
            HasSkillLotMetadata = Skills.HasSkillLotMetadata,
        };

    private void ShowMasterDataStatus(object? parameter) =>
        _shell.ShowInformation("Master data", GameMasterService.Current.GetDetailedStatus());

    private void RefreshMasterData(object? parameter)
    {
        GameMasterService.Current.Refresh();
        StatusText = GameMasterService.Current.GetStatusLine();
    }

    private void UseDefaultMasterData(object? parameter)
    {
        GameMasterService.Current.UseDefaultDatabaseLocation();
        StatusText = GameMasterService.Current.GetStatusLine();
    }

    private void BrowseMasterData(object? parameter)
    {
        string initial = Directory.Exists(MasterDataPaths.DefaultMasterFolder)
            ? MasterDataPaths.DefaultMasterFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string? path = _shell.PickMasterDatabase(initial);
        if (path == null)
        {
            return;
        }

        if (!GameMasterService.Current.TrySetCustomDatabasePath(path))
        {
            _shell.ShowWarning(
                "Master data",
                GameMasterService.Current.LastError ?? "Could not load the selected file.");
        }

        StatusText = GameMasterService.Current.GetStatusLine();
    }

    private void OpenMasterDataFolder(object? parameter) =>
        _shell.OpenFolder(MasterDataPaths.DefaultMasterFolder);

    private static double ElapsedMs(long started) =>
        Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
