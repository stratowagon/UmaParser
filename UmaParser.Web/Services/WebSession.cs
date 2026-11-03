using UmaParser.Import;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.Web;

/// <summary>
/// Captures kept for this browser session. A later pick is appended.
/// The same file (name, size, and last-modified time) is stored once, including a file that failed to import.
/// </summary>
public sealed class WebSession
{
    private const long MaxBytes = 64L * 1024 * 1024;

    private readonly List<CaptureImportResult> _imports = new();
    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    public WebSession(string masterStatus)
    {
        MasterStatus = masterStatus;
    }

    public string MasterStatus { get; }

    public string StatusText { get; private set; } = "Drop one or more .json capture files.";

    public SkillsScreen Skills { get; private set; } = SkillsScreen.Empty;

    public TracksScreen Tracks { get; private set; } = TracksScreen.Empty;

    public int LoadedCount => _imports.Count;

    public bool HasLoaded => _imports.Count > 0;

    public static long MaxFileBytes => MaxBytes;

    /// <summary>Returns how many of <paramref name="files"/> were new.</summary>
    public int Add(IReadOnlyList<IncomingCapture> files)
    {
        int added = 0;
        foreach (IncomingCapture file in files)
        {
            if (!_known.Add(Key(file)))
            {
                continue;
            }

            string name = UniqueName(file.FileName);
            _imports.Add(Import(name, file));
            added++;
        }

        if (added == 0)
        {
            if (files.Count > 0)
            {
                StatusText = "Already loaded.";
            }

            return 0;
        }

        Rebuild();
        return added;
    }

    public void Clear()
    {
        _imports.Clear();
        _known.Clear();
        _names.Clear();
        Skills = SkillsScreen.Empty;
        Tracks = TracksScreen.Empty;
        StatusText = DropStatusText.Cleared;
    }

    public void SelectSkills(int index) => Skills = Skills.Select(index);

    public void SelectTracks(int index) => Tracks = Tracks.Select(index);

    private void Rebuild()
    {
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(_imports);
        NonTeamTrialLoad? singleRaceLoad = batch.Kind == TeamTrialBatchKind.MixedOrNonTT
            ? NonTeamTrialLoad.Collect(batch.SingleRaceImports)
            : null;
        var preferences = new GridColumnGridPreferences();
        Skills = SkillsScreen.Load(batch, preferences, singleRaceLoad);
        Tracks = TracksScreen.Load(batch, preferences, singleRaceLoad);
        StatusText = DropStatusText.For(batch, singleRaceLoad);
    }

    private static CaptureImportResult Import(string fileName, IncomingCapture file)
    {
        CaptureImportResult result;
        if (!string.IsNullOrEmpty(file.ReadError))
        {
            result = Failed(fileName, ImportStatus.InvalidFormat, file.ReadError);
        }
        else if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            string extension = Path.GetExtension(fileName);
            string message = string.IsNullOrEmpty(extension)
                ? "Unsupported file."
                : $"Unsupported file extension: {extension}";
            result = Failed(fileName, ImportStatus.UnsupportedExtension, message);
        }
        else
        {
            result = CaptureImportService.TryImport(fileName, file.JsonText);
        }

        return result.WithFileTimestamp(file.LastModified.UtcDateTime);
    }

    private static CaptureImportResult Failed(string fileName, ImportStatus status, string? message) =>
        new()
        {
            FileName = fileName,
            Status = status,
            ErrorMessage = message,
        };

    private string UniqueName(string fileName)
    {
        string name = string.IsNullOrWhiteSpace(fileName) ? "capture.json" : fileName.Trim();
        if (_names.Add(name))
        {
            return name;
        }

        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);
        for (int n = 2; ; n++)
        {
            string candidate = $"{stem} ({n}){extension}";
            if (_names.Add(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Name, size, and last-modified ticks. The browser has no full path, so this is the stand-in
    /// for the desktop rule that the same path is kept once.
    /// </summary>
    private static string Key(IncomingCapture file) =>
        $"{file.FileName.Trim().ToUpperInvariant()}|{file.Size}|{file.LastModified.UtcDateTime.Ticks}";
}

public sealed record IncomingCapture(
    string FileName,
    string? JsonText,
    long Size,
    DateTimeOffset LastModified,
    string? ReadError);
