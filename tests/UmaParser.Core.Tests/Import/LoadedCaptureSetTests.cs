using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Import;

public sealed class LoadedCaptureSetTests
{
    [Fact]
    public void A_second_drop_keeps_the_earlier_file_and_does_not_count_a_repeat()
    {
        List<FixtureEntry> entries = FixtureCatalog.InGroup("TT-K0").Take(2).ToList();
        string first = FixtureCatalog.FullPath(entries[0]);
        string second = FixtureCatalog.FullPath(entries[1]);
        var set = new LoadedCaptureSet();

        Assert.Equal(1, set.Add([first]));
        Assert.Equal(1, set.Add([first, second, "  "]));
        Assert.Equal(0, set.Add([second.ToUpperInvariant()]));
        Assert.Equal([LoadedCaptureSet.Normalize(first), LoadedCaptureSet.Normalize(second)], set.Paths);

        TeamTrialBatch combined = TeamTrialBatchBuilder.Build(set.Paths.Select(CaptureImportService.TryImportPath));
        TeamTrialBatch direct = TeamTrialBatchBuilder.Build(
            new[] { first, second }.Select(CaptureImportService.TryImportPath));

        Assert.Equal(direct.Kind, combined.Kind);
        Assert.Equal(direct.Trials.Keys, combined.Trials.Keys);
        Assert.Equal(direct.Trials.Count, combined.Trials.Count);
        Assert.Equal(2, combined.Trials.Count);
        Assert.NotNull(combined.MostRecentTrial);
        Assert.Equal(
            direct.Trials.Last().Key,
            combined.Trials.Last().Key);
    }

    [Fact]
    public void A_bad_path_is_skipped_once_and_does_not_remove_captures_already_loaded()
    {
        string capture = FixtureCatalog.FullPath(FixtureCatalog.InGroup("TT-K0").First());
        string notes = Path.Combine(Path.GetTempPath(), "umaparser-loaded-capture-set-notes.txt");
        File.WriteAllText(notes, "not a capture");
        var set = new LoadedCaptureSet();

        Assert.Equal(1, set.Add([notes]));
        Assert.Equal(0, set.Add([notes]));
        TeamTrialBatch skippedOnly = TeamTrialBatchBuilder.Build(set.Paths.Select(CaptureImportService.TryImportPath));
        Assert.False(skippedOnly.HasAnyData);
        Assert.Equal(1, skippedOnly.SkippedFileCount);
        Assert.Equal(DropStatusText.NoneLoaded(1), DropStatusText.For(skippedOnly));

        Assert.Equal(1, set.Add([capture]));
        TeamTrialBatch merged = TeamTrialBatchBuilder.Build(set.Paths.Select(CaptureImportService.TryImportPath));
        Assert.True(merged.HasAnyData);
        Assert.Single(merged.Trials);
        Assert.Equal(1, merged.SkippedFileCount);
        Assert.Equal(TeamTrialBatchKind.RosterStats, merged.Kind);

        set.Clear();
        Assert.Equal(0, set.Count);
        Assert.Equal(1, set.Add([capture]));
        Assert.Equal(DropStatusText.Cleared, "Cleared loaded files.");
    }
}
