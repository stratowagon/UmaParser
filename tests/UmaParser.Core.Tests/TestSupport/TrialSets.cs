using UmaParser.Import;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.TestSupport;

internal static class TrialSets
{
    public static TeamTrialBatch Build(string group)
    {
        List<CaptureImportResult> imports = FixtureCatalog.InGroup(group)
            .Select(FixtureCatalog.Import)
            .ToList();
        return TeamTrialBatchBuilder.Build(imports);
    }

    /// <summary>
    /// Trials in the same order <see cref="TeamTrialBatchBuilder"/> uses when file timestamps are absent:
    /// file name, ordinal ignore case. That is also the order the app analyzes after a fresh checkout.
    /// </summary>
    public static IReadOnlyList<TeamTrialResult> Load(string group)
    {
        TeamTrialBatch batch = Build(group);
        var trials = new List<TeamTrialResult>(batch.Trials.Count);
        foreach (TeamTrialResult trial in batch.Trials.Values)
        {
            trials.Add(trial);
        }

        Assert.Equal(FixtureCatalog.InGroup(group).Count(), trials.Count);
        return trials;
    }
}
