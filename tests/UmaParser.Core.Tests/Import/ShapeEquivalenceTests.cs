using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Import;

public sealed class ShapeEquivalenceTests
{
    const int ExpectedTotal = 782_981;

    static readonly string[] Shapes =
    [
        "TT-Mismatch/20260409_102840_534.json",
        "TT-Mismatch/capture_20260409_102842_0012.json",
        "TT-Mismatch/20260409_102840_534 - Copy.json",
    ];

    [Fact]
    public void Three_capture_shapes_of_the_same_trial_match()
    {
        TeamTrialResult[] trials = Shapes.Select(Load).ToArray();
        TeamTrialResult first = trials[0];

        Assert.Equal(ExpectedTotal, first.TotalScore);
        Assert.True(first.MatchBonus > 0);
        foreach (TeamTrialResult trial in trials.Skip(1))
        {
            Assert.Equal(first.TotalScore, trial.TotalScore);
            Assert.Equal(first.MatchBonus, trial.MatchBonus);
            Assert.Equal(first.OpponentMultiplier, trial.OpponentMultiplier);
            Assert.Equal(first.SupportCardBonus, trial.SupportCardBonus);
            Assert.Equal(first.RosterCompositionKey, trial.RosterCompositionKey);
            Assert.Equal(BaseScores(first), BaseScores(trial));
        }
    }

    static TeamTrialResult Load(string relativePath)
    {
        CaptureImportResult result = FixtureCatalog.Import(FixtureCatalog.Require(relativePath));
        Assert.Equal(ImportStatus.Success, result.Status);
        return Assert.IsType<TeamTrialResult>(result.Response);
    }

    static IReadOnlyList<KeyValuePair<int, int>> BaseScores(TeamTrialResult trial) =>
        trial.RosterTrainedCharaIds
            .Select(id => new KeyValuePair<int, int>(id, trial.UmaBaseScore(id)))
            .OrderBy(pair => pair.Key)
            .ToList();
}
