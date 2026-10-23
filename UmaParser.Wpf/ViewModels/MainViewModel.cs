using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.Mvvm;
using UmaParser.Presentation;

namespace UmaParser.ViewModels;

/// <summary>
/// The window. Today it only hosts Skills. Later tabs get their own view models here.
/// A file drop calls <see cref="LoadFiles"/>, which uses the same import path as WinForms:
/// <see cref="CaptureImportService.TryImportPath"/> then <see cref="TeamTrialBatchBuilder.Build"/>.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private string _statusText;

    public MainViewModel()
    {
        Skills = new SkillsViewModel();
        LoadFilesCommand = new RelayCommand(LoadFiles);
        _statusText = GameMasterService.Current.GetStatusLine();
    }

    public SkillsViewModel Skills { get; }

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

        Skills.Apply(batch);
    }
}
