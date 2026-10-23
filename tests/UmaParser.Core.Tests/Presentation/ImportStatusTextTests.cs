using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class ImportStatusTextTests
{
    [Fact]
    public void Empty_drop_names_the_skipped_files()
    {
        Assert.Equal("No supported race files loaded.", ImportStatusText.Format(new TeamTrialBatch()));
        Assert.Equal(
            "No supported race files loaded (2 file(s) skipped).",
            ImportStatusText.Format(new TeamTrialBatch { SkippedFileCount = 2 }));
    }

    [Fact]
    public void Loaded_batches_name_the_kind_and_the_skip_count()
    {
        Assert.Equal(
            "Roster stats — 2 team trial file(s) loaded.",
            ImportStatusText.Format(Batch(TeamTrialBatchKind.RosterStats, trials: 2, skipped: 0)));
        Assert.Equal(
            "Roster mismatch — 4 team trial file(s) loaded. (1 file(s) skipped)",
            ImportStatusText.Format(Batch(TeamTrialBatchKind.RosterMismatch, trials: 4, skipped: 1)));
        Assert.Equal(
            "Single-race — 3 file(s) loaded.",
            ImportStatusText.Format(Batch(TeamTrialBatchKind.MixedOrNonTT, trials: 0, skipped: 0, singleRace: 3)));
        Assert.Equal(
            "Single-race — 2 file(s) loaded (1 team trial file(s) mixed in).",
            ImportStatusText.Format(Batch(TeamTrialBatchKind.MixedOrNonTT, trials: 1, skipped: 0, singleRace: 2)));
    }

    [Fact]
    public void An_unsupported_file_next_to_a_capture_is_counted_as_skipped()
    {
        string json = FixtureCatalog.FullPath(FixtureCatalog.InGroup("TT-K0").First());
        string folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "umaparser-drop-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string notes = Path.Combine(folder, "notes.txt");
            File.WriteAllText(notes, "not a capture");

            CaptureImportResult droppedFolder = CaptureImportService.TryImportPath(folder);
            Assert.Equal(ImportStatus.UnsupportedExtension, droppedFolder.Status);

            TeamTrialBatch batch = TeamTrialBatchBuilder.Build(
            [
                CaptureImportService.TryImportPath(json),
                CaptureImportService.TryImportPath(notes),
                droppedFolder,
            ]);

            Assert.Equal(TeamTrialBatchKind.RosterStats, batch.Kind);
            Assert.Single(batch.Trials);
            Assert.Equal(2, batch.SkippedFileCount);
            Assert.Equal(
                "Roster stats — 1 team trial file(s) loaded. (2 file(s) skipped)",
                ImportStatusText.Format(batch));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Non_race_fixture_is_an_empty_batch()
    {
        TeamTrialBatch batch = TrialSets.Build("NonRace");
        Assert.Equal(TeamTrialBatchKind.Empty, batch.Kind);
        Assert.Equal(
            $"No supported race files loaded ({batch.SkippedFileCount} file(s) skipped).",
            ImportStatusText.Format(batch));
    }

    private static TeamTrialBatch Batch(TeamTrialBatchKind kind, int trials, int skipped, int singleRace = 0)
    {
        var trialMap = new Dictionary<string, TeamTrialResult>();
        for (int i = 0; i < trials; i++)
        {
            trialMap[$"{i}.json"] = null!;
        }

        var singles = new List<CaptureImportResult>();
        for (int i = 0; i < singleRace; i++)
        {
            singles.Add(new CaptureImportResult { FileName = $"race{i}.json", IsSingleRace = true, Status = ImportStatus.Success });
        }

        return new TeamTrialBatch
        {
            Kind = kind,
            Trials = trialMap,
            SingleRaceImports = singles,
            SkippedFileCount = skipped,
        };
    }
}
