using UmaParser.Core.Tests.TestSupport;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class UmaAnalysisSnapshotTests
{
    [Theory]
    [InlineData("TT-K0", "K0")]
    [InlineData("TT-SwapTest", "SwapTest")]
    [InlineData("TT-Older-Pasa", "Pasa")]
    [InlineData("TT-Older-Cafe1", "Cafe1")]
    [InlineData("TT-Older-SGolshi", "SGolshi")]
    [InlineData("TT-Older-Xoguri", "Xoguri")]
    public void Roster_matches_golden_master(string group, string suffix)
    {
        var trials = TrialSets.Load(group);
        Snapshot.Match(AnalysisInputs.Analyze(trials), suffix);
    }
}
