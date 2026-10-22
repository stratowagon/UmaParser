using UmaParser.Analysis;
using UmaParser.ObjectModel;

namespace UmaParser.Core.Tests.TestSupport;

/// <summary>
/// Builds the same inputs <c>Form1.PopulateAnalysis</c> passes to <see cref="UmaAnalysisEngine"/>.
/// This is a copy of that UI helper so the tests can run without WinForms. The layout trial is the
/// last one in the list, which is the newest file once <see cref="TrialSets"/> has ordered the batch.
/// </summary>
internal static class AnalysisInputs
{
    public static UmaAnalysisReport Analyze(IReadOnlyList<TeamTrialResult> trials)
    {
        if (trials.Count == 0)
        {
            throw new InvalidOperationException("No trials to analyze.");
        }

        return Analyze(trials, trials[^1]);
    }

    public static UmaAnalysisReport Analyze(IReadOnlyList<TeamTrialResult> trials, TeamTrialResult layout)
    {
        IReadOnlyList<int> ids = layout.RosterTrainedCharaIds;
        return UmaAnalysisEngine.Analyze(
            layout.RosterNames,
            BuildCategoryScoreMatrix(trials, ids, (trial, id) => trial.UmaBaseScore(id)),
            layout,
            BuildCategoryScoreMatrix(trials, ids, (trial, id) => trial.UmaSkillBaseScore(id)),
            BuildCategoryScoreMatrix(trials, ids, (trial, id) => trial.UmaOtherBaseScore(id)),
            ids,
            trials);
    }

    static List<IReadOnlyList<double>> BuildCategoryScoreMatrix(
        IEnumerable<TeamTrialResult> trials,
        IReadOnlyList<int> canonicalIds,
        Func<TeamTrialResult, int, int> getScore)
    {
        var matrix = new List<IReadOnlyList<double>>(canonicalIds.Count);
        var trialList = trials.ToList();

        var trialScoreMaps = new List<Dictionary<int, int>>(trialList.Count);
        foreach (TeamTrialResult trial in trialList)
        {
            var map = new Dictionary<int, int>();
            foreach (int id in trial.RosterTrainedCharaIds)
            {
                map[id] = getScore(trial, id);
            }

            trialScoreMaps.Add(map);
        }

        for (int i = 0; i < canonicalIds.Count; i++)
        {
            int id = canonicalIds[i];
            var scores = new double[trialList.Count];
            for (int t = 0; t < trialList.Count; t++)
            {
                scores[t] = trialScoreMaps[t].TryGetValue(id, out int score) ? score : 0;
            }

            matrix.Add(scores);
        }

        return matrix;
    }
}
