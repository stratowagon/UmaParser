using UmaParser.Core.Tests.TestSupport;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class TracksSnapshotTests
{
    [Theory]
    [InlineData("TT-K0", "K0")]
    [InlineData("TT-SwapTest", "SwapTest")]
    public void Every_uma_matches_golden_master(string group, string suffix)
    {
        var trials = TrialSets.Load(group);
        Snapshot.Match(Projections.TracksFor(suffix, trials), suffix);
    }
}
