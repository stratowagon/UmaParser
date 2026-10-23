using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.Mvvm;
using UmaParser.Presentation;

namespace UmaParser.ViewModels;

/// <summary>
/// The window. <see cref="Tabs"/> is the list of screens, in tab order.
/// A file drop calls <see cref="LoadFiles"/>, which uses the same import path as WinForms:
/// <see cref="CaptureImportService.TryImportPath"/> then <see cref="TeamTrialBatchBuilder.Build"/>.
/// That one batch is handed to every screen. Skills and Tracks both read it.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private string _statusText;

    public MainViewModel()
    {
        Skills = new SkillsViewModel();
        Tracks = new TracksViewModel();
        Tabs =
        [
            new ScreenTab { Title = "Skills", Content = Skills },
            new ScreenTab { Title = "Tracks", Content = Tracks },
        ];
        LoadFilesCommand = new RelayCommand(LoadFiles);
        _statusText = GameMasterService.Current.GetStatusLine();
    }

    public SkillsViewModel Skills { get; }
    public TracksViewModel Tracks { get; }

    /// <summary>Bound to the window's tab control. Add a screen here when the next tab is ported.</summary>
    public IReadOnlyList<ScreenTab> Tabs { get; }

    public RelayCommand LoadFilesCommand { get; }

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
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(paths.Select(CaptureImportService.TryImportPath));
        StatusText = ImportStatusText.Format(batch);
        if (!batch.HasAnyData)
        {
            return;
        }

        // One parse of the sim blobs, shared by both screens. Team Trials does not need it.
        NonTeamTrialLoad? singleRaceLoad = batch.Kind == TeamTrialBatchKind.MixedOrNonTT
            ? NonTeamTrialLoad.Collect(batch.SingleRaceImports)
            : null;
        Skills.Apply(batch, singleRaceLoad: singleRaceLoad);
        Tracks.Apply(batch, singleRaceLoad: singleRaceLoad);
    }
}
