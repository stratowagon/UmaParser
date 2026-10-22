using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class OrderInvarianceTests
{
    [Fact]
    public void SwapTest_scores_do_not_depend_on_trial_order()
    {
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-SwapTest");
        TeamTrialResult layout = trials[^1];
        var reversed = trials.Reverse().ToList();
        var shuffled = Shuffle(trials, seed: 42);

        var baseline = AnalysisInputs.Analyze(trials, layout);
        Snapshot.AssertSame(baseline, AnalysisInputs.Analyze(reversed, layout), "Reversed trial order changed the analysis.");
        Snapshot.AssertSame(baseline, AnalysisInputs.Analyze(shuffled, layout), "Shuffled trial order changed the analysis.");

        // Tracks and skills are per uma. The roster order itself comes from the layout trial
        // (member order can differ between files), so each uma is compared on its own id.
        IReadOnlyList<int> ids = layout.RosterTrainedCharaIds;
        IReadOnlyList<string> names = layout.RosterNames;
        for (int i = 0; i < ids.Count; i++)
        {
            Snapshot.AssertSame(
                TracksAnalyzer.Analyze(trials, ids[i], names[i]),
                TracksAnalyzer.Analyze(reversed, ids[i], names[i]),
                $"Reversed trial order changed tracks for {names[i]}.");
            Snapshot.AssertSame(
                Projections.Skills(SkillActivationAnalyzer.Analyze(trials, ids[i], names[i])),
                Projections.Skills(SkillActivationAnalyzer.Analyze(reversed, ids[i], names[i])),
                $"Reversed trial order changed skills for {names[i]}.");
        }
    }

    [Fact]
    public void SwapTest_ace_assignment_is_not_the_same_in_every_file()
    {
        // The app uses the newest trial as the analysis layout. These files do not all
        // mark the same umas as ace, so reversing the list and also switching the layout
        // trial would change ace labels. The order test above keeps the newest layout.
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-SwapTest");
        Assert.NotEqual(Aces(trials[0]), Aces(trials[^1]));
    }

    static List<int> Aces(TeamTrialResult trial) =>
        trial.RaceRoster
            .Where(pair => pair.Value.TeamMemberId == 1)
            .Select(pair => pair.Key)
            .OrderBy(id => id)
            .ToList();

    static List<TeamTrialResult> Shuffle(IReadOnlyList<TeamTrialResult> trials, uint seed)
    {
        var copy = trials.ToList();
        uint state = seed == 0 ? 1 : seed;
        for (int i = copy.Count - 1; i > 0; i--)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            int j = (int)(state % (uint)(i + 1));
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }
}
