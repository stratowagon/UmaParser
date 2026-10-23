using UmaParser.Import;
using UmaParser.ObjectModel;

namespace UmaParser.Analysis;

/// <summary>
/// Results grid for a uniform Team Trials drop: one name column, one score column per file,
/// and a total per file. The name order is the newest file. Scores are matched by trained
/// character id, so a shuffle inside a sub-team still lines up.
/// </summary>
public sealed class RosterStatsTable
{
    public RosterStatsTable(
        IReadOnlyList<string> fileNames,
        IReadOnlyList<string> names,
        IReadOnlyList<int> trainedCharaIds,
        int[,] scores,
        IReadOnlyList<int> totals)
    {
        FileNames = fileNames;
        Names = names;
        TrainedCharaIds = trainedCharaIds;
        Scores = scores;
        Totals = totals;
    }

    public IReadOnlyList<string> FileNames { get; }

    /// <summary>Roster names from the newest file, in that file's roster order.</summary>
    public IReadOnlyList<string> Names { get; }

    public IReadOnlyList<int> TrainedCharaIds { get; }

    /// <summary><c>[rosterRow, fileIndex]</c>. A uma missing from a file scores 0.</summary>
    public int[,] Scores { get; }

    /// <summary>Each file's match total, including the calculated match bonus.</summary>
    public IReadOnlyList<int> Totals { get; }
}

public static class RosterStatsTableBuilder
{
    public static RosterStatsTable Build(TeamTrialBatch batch) =>
        Build(batch.Trials.ToList());

    public static RosterStatsTable Build(IReadOnlyList<KeyValuePair<string, TeamTrialResult>> files)
    {
        if (files.Count == 0)
        {
            return new RosterStatsTable(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<int>(),
                new int[0, 0],
                Array.Empty<int>());
        }

        TeamTrialResult layout = files[^1].Value;
        IReadOnlyList<int> canonicalIds = layout.RosterTrainedCharaIds;
        IReadOnlyList<string> canonicalNames = layout.RosterNames;
        var fileNames = new string[files.Count];
        var totals = new int[files.Count];
        var scores = new int[canonicalIds.Count, files.Count];

        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            fileNames[fileIndex] = files[fileIndex].Key;
            TeamTrialResult trial = files[fileIndex].Value;
            totals[fileIndex] = trial.TotalScore;

            IReadOnlyList<int> trialIds = trial.RosterTrainedCharaIds;
            IReadOnlyList<int> trialScores = trial.RosterScores;
            var scoreMap = new Dictionary<int, int>();
            for (int j = 0; j < trialIds.Count; j++)
            {
                scoreMap[trialIds[j]] = trialScores[j];
            }

            for (int row = 0; row < canonicalIds.Count; row++)
            {
                int id = canonicalIds[row];
                scores[row, fileIndex] = scoreMap.TryGetValue(id, out int score) ? score : 0;
            }
        }

        return new RosterStatsTable(fileNames, canonicalNames, canonicalIds, scores, totals);
    }
}
