using UmaParser.Import;
using UmaParser.ObjectModel;

namespace UmaParser.Analysis;

/// <summary>
/// One uma that keeps the same distance team and running style in every file.
/// Slot changes inside that 3-uma team still count as consistent.
/// </summary>
public sealed record StableRosterUma(
    int TrainedCharaId,
    string DisplayName,
    int DistanceType,
    int RunningStyle);

/// <summary>
/// Majority roster, per-cell outliers, and the stable core for a Team Trials drop
/// whose files are not one identical roster.
/// </summary>
public sealed class RosterConsistencyReport
{
    public RosterConsistencyReport(
        IReadOnlyList<TeamTrialResult> trials,
        int majorityFileIndex,
        bool[,] outliers,
        IReadOnlyList<StableRosterUma> stableCore)
    {
        Trials = trials;
        MajorityFileIndex = majorityFileIndex;
        Outliers = outliers;
        StableCore = stableCore;
    }

    /// <summary>Files in the same order as the first index of <see cref="Outliers"/>.</summary>
    public IReadOnlyList<TeamTrialResult> Trials { get; }

    /// <summary>
    /// First file in the largest positional id group. -1 when there are no files.
    /// When two groups are the same size, the one that appeared first wins.
    /// </summary>
    public int MajorityFileIndex { get; }

    public TeamTrialResult? MajorityTrial =>
        MajorityFileIndex >= 0 && MajorityFileIndex < Trials.Count
            ? Trials[MajorityFileIndex]
            : null;

    /// <summary>
    /// <c>[fileIndex, rosterSlot]</c>. True when that slot's veteran or running style
    /// differs from the majority file. Files with the majority composition key are all false,
    /// so a shuffle inside a sub-team is not highlighted.
    /// </summary>
    public bool[,] Outliers { get; }

    public IReadOnlyList<StableRosterUma> StableCore { get; }
}

/// <summary>
/// Roster comparison used by the Results tab when Team Trials files do not share one roster.
/// The stable-core rule is unchanged: an uma stays if every file has that trained character
/// on the same distance team and the same running style. Position inside the team may change.
/// </summary>
public static class RosterConsistencyAnalyzer
{
    /// <summary>Fixed Team Trials roster length. Outlier rows use this many slots.</summary>
    public const int SlotCount = 15;

    public static RosterConsistencyReport Analyze(TeamTrialBatch batch)
    {
        var trials = new List<TeamTrialResult>(batch.Trials.Count);
        foreach (TeamTrialResult trial in batch.Trials.Values)
        {
            trials.Add(trial);
        }

        return Analyze(trials);
    }

    public static RosterConsistencyReport Analyze(IReadOnlyList<TeamTrialResult> trials)
    {
        int fileCount = trials.Count;
        var outliers = new bool[fileCount, SlotCount];
        if (fileCount == 0)
        {
            return new RosterConsistencyReport(trials, -1, outliers, Array.Empty<StableRosterUma>());
        }

        // Majority roster is the most common positional id list, not the composition key.
        // A style change keeps the same ids, so those files stay in one group.
        var rosterGroups = new Dictionary<string, List<int>>();
        for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
        {
            string key = string.Join(",", trials[fileIndex].RosterTrainedCharaIds);
            if (!rosterGroups.TryGetValue(key, out List<int>? group))
            {
                group = new List<int>();
                rosterGroups[key] = group;
            }

            group.Add(fileIndex);
        }

        string majorityKey = string.Empty;
        int majorityCount = -1;
        foreach (KeyValuePair<string, List<int>> group in rosterGroups)
        {
            if (group.Value.Count > majorityCount)
            {
                majorityCount = group.Value.Count;
                majorityKey = group.Key;
            }
        }

        int majorityFileIndex = rosterGroups[majorityKey][0];
        TeamTrialResult majority = trials[majorityFileIndex];
        IReadOnlyList<int> majorityIds = majority.RosterTrainedCharaIds;
        IReadOnlyList<int> referenceStyles = majority.RosterRunningStyles;

        for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
        {
            TeamTrialResult trial = trials[fileIndex];
            if (trial.RosterCompositionKey == majority.RosterCompositionKey)
            {
                continue;
            }

            IReadOnlyList<int> fileIds = trial.RosterTrainedCharaIds;
            IReadOnlyList<int> fileStyles = trial.RosterRunningStyles;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                bool idDiffers = fileIds[slot] != majorityIds[slot];
                bool styleDiffers = fileStyles[slot] != referenceStyles[slot];
                outliers[fileIndex, slot] = idDiffers || styleDiffers;
            }
        }

        IReadOnlyList<StableRosterUma> stableCore = BuildStableCore(trials);
        return new RosterConsistencyReport(trials, majorityFileIndex, outliers, stableCore);
    }

    static IReadOnlyList<StableRosterUma> BuildStableCore(IReadOnlyList<TeamTrialResult> trials)
    {
        var stableCore = new List<StableRosterUma>();
        TeamTrialResult first = trials[0];
        IReadOnlyDictionary<int, (int DistanceType, int RunningStyle)> firstDetails = first.RosterDetails;
        IReadOnlyList<string> firstNames = first.RosterNames;
        IReadOnlyList<int> firstIds = first.RosterTrainedCharaIds;
        var nameById = new Dictionary<int, string>();
        for (int i = 0; i < firstIds.Count; i++)
        {
            nameById[firstIds[i]] = firstNames[i];
        }

        foreach (KeyValuePair<int, (int DistanceType, int RunningStyle)> entry in firstDetails)
        {
            int id = entry.Key;
            (int distance, int style) = entry.Value;
            bool consistent = true;
            for (int fileIndex = 1; fileIndex < trials.Count && consistent; fileIndex++)
            {
                IReadOnlyDictionary<int, (int DistanceType, int RunningStyle)> otherDetails =
                    trials[fileIndex].RosterDetails;
                if (!otherDetails.TryGetValue(id, out (int DistanceType, int RunningStyle) other)
                    || other.DistanceType != distance
                    || other.RunningStyle != style)
                {
                    consistent = false;
                }
            }

            if (!consistent)
            {
                continue;
            }

            string display =
                $"{DistanceTypeNames.Format(distance)}: {nameById[id]} ({RunningStyleNames.FormatRoster(style)})";
            stableCore.Add(new StableRosterUma(id, display, distance, style));
        }

        return stableCore;
    }
}
