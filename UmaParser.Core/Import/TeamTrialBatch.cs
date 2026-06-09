using UmaBlobber.ObjectModel;

namespace UmaBlobber.Import;

public enum TeamTrialBatchKind
{
    Empty,
    RosterMismatch,
    RosterStats,
    MixedOrNonTT, // contains single-race captures (CM / Room / Practice) or a mix of TT + other
}

public sealed class TeamTrialBatch
{
    public TeamTrialBatchKind Kind { get; init; }
    public IReadOnlyDictionary<string, TeamTrialResult> Trials { get; init; }
        = new Dictionary<string, TeamTrialResult>();

    /// <summary>
    /// Successfully imported single-race captures. These carry the normalized JSON + the extracted
    /// SimDataBase64 (the race scenario blob) so Skills and Tracks can work with them.
    /// </summary>
    public IReadOnlyList<CaptureImportResult> SingleRaceImports { get; init; } = Array.Empty<CaptureImportResult>();

    public int SkippedFileCount { get; init; }

    public bool HasAnyData => Trials.Count > 0 || SingleRaceImports.Count > 0;
}

public static class TeamTrialBatchBuilder
{
    public static TeamTrialBatch Build(IEnumerable<CaptureImportResult> imports)
    {
        var trials = new Dictionary<string, TeamTrialResult>();
        var singleRaces = new List<CaptureImportResult>();
        int skipped = 0;

        foreach (var import in imports)
        {
            if (import.Response is TeamTrialResult trial)
            {
                trials[import.FileName] = trial;
            }
            else if (import.IsSingleRace && import.Status == ImportStatus.Success && !string.IsNullOrWhiteSpace(import.SingleRaceSimDataBase64))
            {
                singleRaces.Add(import);
            }
            else
            {
                skipped++;
            }
        }

        if (trials.Count == 0 && singleRaces.Count == 0)
        {
            return new TeamTrialBatch
            {
                Kind = TeamTrialBatchKind.Empty,
                SkippedFileCount = skipped,
            };
        }

        if (singleRaces.Count > 0)
        {
            return new TeamTrialBatch
            {
                Kind = TeamTrialBatchKind.MixedOrNonTT,
                Trials = trials,
                SingleRaceImports = singleRaces,
                SkippedFileCount = skipped,
            };
        }

        // Pure TT (original behavior preserved)
        var firstRoster = trials.First().Value.RosterTrainedCharaIds;
        bool rostersMatch = trials.Values.All(t => t.RosterTrainedCharaIds.SequenceEqual(firstRoster));

        return new TeamTrialBatch
        {
            Kind = rostersMatch ? TeamTrialBatchKind.RosterStats : TeamTrialBatchKind.RosterMismatch,
            Trials = trials,
            SingleRaceImports = Array.Empty<CaptureImportResult>(),
            SkippedFileCount = skipped,
        };
    }
}