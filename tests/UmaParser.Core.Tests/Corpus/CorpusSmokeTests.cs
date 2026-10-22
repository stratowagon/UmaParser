using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Corpus;

public sealed class CorpusSmokeTests(ITestOutputHelper output)
{
    [Fact]
    public void Imports_and_analyzes_when_corpus_is_configured()
    {
        string? root = Environment.GetEnvironmentVariable("UMAPARSER_CORPUS");
        if (string.IsNullOrWhiteSpace(root))
        {
            Assert.Skip(
                "Skipped. Set UMAPARSER_CORPUS to a folder of race captures to import and analyze every JSON file there. " +
                "Folders named \"from other players\", \"API responses\", and \"_sympathy_scan\" are left out. " +
                "This test does not write to that folder. In Visual Studio, set the variable and restart, " +
                "or select a .runsettings file that defines it under Test > Configure Run Settings.");
        }

        Assert.True(Directory.Exists(root), $"UMAPARSER_CORPUS does not exist: {root}");

        List<string> files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
            .Where(path => !Excluded(Path.GetRelativePath(root, path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.NotEmpty(files);

        var counts = new Dictionary<ImportStatus, int>();
        var imported = new List<(string Directory, CaptureImportResult Result)>();
        foreach (string path in files)
        {
            string relative = Path.GetRelativePath(root, path);
            CaptureImportResult result;
            try
            {
                result = CaptureImportService.TryImport(Path.GetFileName(path), jsonText: File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Assert.Fail($"{relative} threw {ex.GetType().Name}: {ex.Message}");
                return;
            }

            Assert.True(
                result.Status is ImportStatus.Success or ImportStatus.UnsupportedType or ImportStatus.InvalidFormat,
                $"{relative} status {result.Status}: {result.ErrorMessage}");
            counts[result.Status] = counts.GetValueOrDefault(result.Status) + 1;
            imported.Add((Path.GetDirectoryName(relative) ?? "", result));
        }

        Assert.Equal(files.Count, counts.Values.Sum());
        foreach ((ImportStatus status, int count) in counts.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            output.WriteLine($"{status}: {count}");
        }

        foreach (IGrouping<string, (string Directory, CaptureImportResult Result)> folder in imported.GroupBy(item => item.Directory))
        {
            TeamTrialBatch batch;
            try
            {
                batch = TeamTrialBatchBuilder.Build(folder.Select(item => item.Result));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Batch {folder.Key} threw {ex.GetType().Name}: {ex.Message}");
                return;
            }

            output.WriteLine($"BATCH {folder.Key} | {batch.Kind} trials={batch.Trials.Count} single={batch.SingleRaceImports.Count} skipped={batch.SkippedFileCount}");
            if (batch.Kind != TeamTrialBatchKind.RosterStats)
            {
                continue;
            }

            var trials = batch.Trials.Values.ToList();
            UmaAnalysisReport report = AnalysisInputs.Analyze(trials);
            Assert.NotEmpty(report.Rows);
            TeamTrialResult layout = trials[^1];
            IReadOnlyList<int> ids = layout.RosterTrainedCharaIds;
            IReadOnlyList<string> names = layout.RosterNames;
            for (int i = 0; i < ids.Count; i++)
            {
                TracksAnalyzer.Analyze(trials, ids[i], names[i]);
                SkillActivationAnalyzer.Analyze(trials, ids[i], names[i]);
            }
        }
    }

    static bool Excluded(string relative)
    {
        foreach (string part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (part.Equals("from other players", StringComparison.OrdinalIgnoreCase)
                || part.Equals("API responses", StringComparison.OrdinalIgnoreCase)
                || part.Equals("_sympathy_scan", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
