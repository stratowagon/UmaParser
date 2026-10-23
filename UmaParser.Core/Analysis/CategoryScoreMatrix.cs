using UmaParser.ObjectModel;

namespace UmaParser.Analysis;

/// <summary>
/// Builds the per-uma score rows that <see cref="UmaAnalysisEngine"/> analyzes.
/// Scores are lined up on the layout trial's trained-character ids. A uma missing
/// from a file contributes 0 for that file.
/// </summary>
public static class CategoryScoreMatrix
{
    public static IReadOnlyList<IReadOnlyList<double>> Build(
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
