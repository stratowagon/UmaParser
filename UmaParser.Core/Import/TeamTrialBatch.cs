using UmaBlobber.ObjectModel;

namespace UmaBlobber.Import;

public enum TeamTrialBatchKind
{
    Empty,
    RosterMismatch,
    RosterStats,
}

public sealed class TeamTrialBatch
{
    public TeamTrialBatchKind Kind { get; init; }
    public IReadOnlyDictionary<string, TeamTrialResult> Trials { get; init; }
        = new Dictionary<string, TeamTrialResult>();

    public int SkippedFileCount { get; init; }
}

public static class TeamTrialBatchBuilder
{
    public static TeamTrialBatch Build(IEnumerable<CaptureImportResult> imports)
    {
        var trials = new Dictionary<string, TeamTrialResult>();
        int skipped = 0;

        foreach (var import in imports)
        {
            if (import.Response is TeamTrialResult trial)
            {
                trials[import.FileName] = trial;
            }
            else
            {
                skipped++;
            }
        }

        if (trials.Count == 0)
        {
            return new TeamTrialBatch
            {
                Kind = TeamTrialBatchKind.Empty,
                SkippedFileCount = skipped,
            };
        }

        var firstRoster = trials.First().Value.RosterNames;
        bool rostersMatch = trials.Values.All(t => t.RosterNames.SequenceEqual(firstRoster));

        return new TeamTrialBatch
        {
            Kind = rostersMatch ? TeamTrialBatchKind.RosterStats : TeamTrialBatchKind.RosterMismatch,
            Trials = trials,
            SkippedFileCount = skipped,
        };
    }
}