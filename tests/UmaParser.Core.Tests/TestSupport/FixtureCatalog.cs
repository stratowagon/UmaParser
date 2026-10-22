using System.Text.Json;
using UmaParser.Import;

namespace UmaParser.Core.Tests.TestSupport;

internal sealed record FixtureEntry(
    string RelativePath,
    string OriginalPath,
    int OriginalSizeKb,
    string OriginalLastWritePacific,
    string Group,
    string ExpectedBatchKind,
    long ScrubbedSizeBytes)
{
    public string FileName => Path.GetFileName(RelativePath);
}

internal static class FixtureCatalog
{
    static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    static readonly Dictionary<string, CaptureImportResult> Imports = new(StringComparer.Ordinal);
    static readonly object Gate = new();

    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static IReadOnlyList<FixtureEntry> All { get; } = Load();

    public static IEnumerable<FixtureEntry> InGroup(string group) =>
        All.Where(entry => entry.Group == group)
            .OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> Groups =>
        All.Select(entry => entry.Group).Distinct(StringComparer.Ordinal);

    public static IEnumerable<FixtureEntry> TeamTrials =>
        All.Where(entry => entry.Group.StartsWith("TT-", StringComparison.Ordinal));

    public static string FullPath(FixtureEntry entry) =>
        Path.Combine(Root, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string ReadText(FixtureEntry entry) => File.ReadAllText(FullPath(entry));

    public static CaptureImportResult Import(FixtureEntry entry)
    {
        lock (Gate)
        {
            if (Imports.TryGetValue(entry.RelativePath, out CaptureImportResult? cached))
            {
                return cached;
            }
        }

        CaptureImportResult result = CaptureImportService.TryImport(entry.FileName, jsonText: ReadText(entry));
        lock (Gate)
        {
            Imports[entry.RelativePath] = result;
        }

        return result;
    }

    public static FixtureEntry Require(string relativePath)
    {
        FixtureEntry? entry = All.SingleOrDefault(item =>
            string.Equals(item.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            throw new InvalidOperationException($"Fixture not in the manifest: {relativePath}");
        }

        return entry;
    }

    static IReadOnlyList<FixtureEntry> Load()
    {
        string manifestPath = Path.Combine(Root, "manifest.json");
        ManifestDocument manifest = JsonSerializer.Deserialize<ManifestDocument>(File.ReadAllText(manifestPath), ManifestJson)
            ?? throw new InvalidOperationException("Fixture manifest was empty.");

        return manifest.Files
            .Select(file => new FixtureEntry(
                file.Path.Replace('\\', '/'),
                file.OriginalPath,
                file.OriginalSizeKb,
                file.OriginalLastWritePacific,
                file.Group,
                file.ExpectedBatchKind,
                file.ScrubbedSizeBytes))
            .ToList();
    }

    sealed class ManifestDocument
    {
        public List<ManifestFile> Files { get; set; } = new();
    }

    sealed class ManifestFile
    {
        public string Path { get; set; } = "";
        public string OriginalPath { get; set; } = "";
        public int OriginalSizeKb { get; set; }
        public string OriginalLastWritePacific { get; set; } = "";
        public string Group { get; set; } = "";
        public string ExpectedBatchKind { get; set; } = "";
        public long ScrubbedSizeBytes { get; set; }
    }
}
