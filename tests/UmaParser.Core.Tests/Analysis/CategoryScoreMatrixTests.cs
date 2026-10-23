using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class CategoryScoreMatrixTests
{
    [Fact]
    public void Lines_scores_up_on_the_layout_ids_and_uses_zero_when_an_uma_is_missing()
    {
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-Mismatch");
        IReadOnlyList<int> ids = trials[0].RosterTrainedCharaIds;
        IReadOnlyList<IReadOnlyList<double>> matrix = CategoryScoreMatrix.Build(
            trials,
            ids,
            (trial, id) => trial.UmaBaseScore(id));

        Assert.Equal(ids.Count, matrix.Count);
        bool sawMissing = false;
        for (int trialIndex = 0; trialIndex < trials.Count; trialIndex++)
        {
            TeamTrialResult trial = trials[trialIndex];
            var present = trial.RosterTrainedCharaIds.ToHashSet();
            Assert.Equal(trials.Count, matrix[0].Count);
            for (int row = 0; row < ids.Count; row++)
            {
                int id = ids[row];
                if (!present.Contains(id))
                {
                    sawMissing = true;
                    Assert.Equal(0, matrix[row][trialIndex]);
                }
                else
                {
                    Assert.Equal(trial.UmaBaseScore(id), matrix[row][trialIndex]);
                }
            }
        }

        Assert.True(sawMissing);

        IReadOnlyList<IReadOnlyList<double>> skills = CategoryScoreMatrix.Build(
            trials,
            ids,
            (trial, id) => trial.UmaSkillBaseScore(id));
        IReadOnlyList<IReadOnlyList<double>> other = CategoryScoreMatrix.Build(
            trials,
            ids,
            (trial, id) => trial.UmaOtherBaseScore(id));
        for (int row = 0; row < ids.Count; row++)
        {
            for (int trialIndex = 0; trialIndex < trials.Count; trialIndex++)
            {
                if (matrix[row][trialIndex] == 0 && !trials[trialIndex].RosterTrainedCharaIds.Contains(ids[row]))
                {
                    Assert.Equal(0, skills[row][trialIndex]);
                    Assert.Equal(0, other[row][trialIndex]);
                }
                else
                {
                    Assert.Equal(matrix[row][trialIndex], skills[row][trialIndex] + other[row][trialIndex]);
                }
            }
        }
    }

    [Fact]
    public void Analyze_uses_the_last_trial_as_the_layout()
    {
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-K0");
        UmaAnalysisReport report = UmaAnalysisEngine.Analyze(trials);
        Assert.Equal(trials[^1].RosterNames, report.Rows.Select(row => row.Name));
        Assert.Equal(trials.Count, report.TrialCount);
    }
}
