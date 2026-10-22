using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using Xunit;

namespace UmaParser.Core.Tests.Batch;

public sealed class BatchKindTests
{
    public static IEnumerable<object[]> Groups() =>
        FixtureCatalog.Groups.Select(group => new object[] { group });

    [Theory]
    [MemberData(nameof(Groups))]
    public void Group_builds_the_expected_batch(string group)
    {
        List<FixtureEntry> entries = FixtureCatalog.InGroup(group).ToList();
        string expectedKind = entries[0].ExpectedBatchKind;
        Assert.All(entries, entry => Assert.Equal(expectedKind, entry.ExpectedBatchKind));

        TeamTrialBatch batch = TrialSets.Build(group);
        Assert.Equal(Enum.Parse<TeamTrialBatchKind>(expectedKind), batch.Kind);

        switch (batch.Kind)
        {
            case TeamTrialBatchKind.RosterStats:
            case TeamTrialBatchKind.RosterMismatch:
                Assert.Equal(entries.Count, batch.Trials.Count);
                Assert.Empty(batch.SingleRaceImports);
                Assert.Equal(0, batch.SkippedFileCount);
                break;
            case TeamTrialBatchKind.MixedOrNonTT:
                Assert.Empty(batch.Trials);
                Assert.Single(batch.SingleRaceImports);
                Assert.Equal(0, batch.SkippedFileCount);
                break;
            case TeamTrialBatchKind.Empty:
                Assert.Empty(batch.Trials);
                Assert.Empty(batch.SingleRaceImports);
                Assert.Equal(entries.Count, batch.SkippedFileCount);
                break;
        }
    }
}
