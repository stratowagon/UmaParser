using UmaParser.ObjectModel;

namespace UmaParser.Import;

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
        var trialImports = new List<(CaptureImportResult Import, TeamTrialResult Trial)>();
        var singleRaces = new List<CaptureImportResult>();
        int skipped = 0;

        foreach (var import in imports)
        {
            if (import.Response is TeamTrialResult trial)
            {
                trialImports.Add((import, trial));
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

        if (trialImports.Count == 0 && singleRaces.Count == 0)
        {
            return new TeamTrialBatch
            {
                Kind = TeamTrialBatchKind.Empty,
                SkippedFileCount = skipped,
            };
        }

        trialImports.Sort((a, b) => CompareImportsChronologically(a.Import, b.Import));
        singleRaces.Sort(CompareImportsChronologically);

        var trials = new Dictionary<string, TeamTrialResult>();
        foreach (var (import, trial) in trialImports)
        {
            trials[import.FileName] = trial;
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
        var firstKey = trialImports[0].Trial.RosterCompositionKey;
        bool rostersMatch = trialImports.All(entry => entry.Trial.RosterCompositionKey == firstKey);

        return new TeamTrialBatch
        {
            Kind = rostersMatch ? TeamTrialBatchKind.RosterStats : TeamTrialBatchKind.RosterMismatch,
            Trials = trials,
            SingleRaceImports = Array.Empty<CaptureImportResult>(),
            SkippedFileCount = skipped,
        };
    }

    private static int CompareImportsChronologically(CaptureImportResult a, CaptureImportResult b)
    {
        DateTime aTime = a.SourceFileLastWriteTimeUtc ?? DateTime.MaxValue;
        DateTime bTime = b.SourceFileLastWriteTimeUtc ?? DateTime.MaxValue;
        int timeCompare = aTime.CompareTo(bTime);
        return timeCompare != 0
            ? timeCompare
            : string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
    }
}