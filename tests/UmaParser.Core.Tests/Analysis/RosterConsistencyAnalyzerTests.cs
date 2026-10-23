using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class RosterConsistencyAnalyzerTests
{
    [Fact]
    public void Style_change_highlights_only_the_changed_running_style()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-StyleChange");
        RosterConsistencyReport report = RosterConsistencyAnalyzer.Analyze(batch);
        TeamTrialResult majority = report.MajorityTrial!;
        int slot = IndexOf(majority, 1788);
        Assert.All(report.Trials, trial => Assert.Equal(majority.RosterTrainedCharaIds, trial.RosterTrainedCharaIds));

        for (int file = 0; file < report.Trials.Count; file++)
        {
            TeamTrialResult trial = report.Trials[file];
            bool sameRoster = trial.RosterCompositionKey == majority.RosterCompositionKey;
            for (int position = 0; position < RosterConsistencyAnalyzer.SlotCount; position++)
            {
                bool expected = !sameRoster && position == slot;
                Assert.Equal(expected, report.Outliers[file, position]);
            }
        }

        Assert.DoesNotContain(report.StableCore, uma => uma.TrainedCharaId == 1788);
        Assert.Equal(14, report.StableCore.Count);
        AssertDisplayNames(report);
    }

    [Fact]
    public void Mismatch_keeps_umas_shared_by_every_file_and_flags_the_swapped_file()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-Mismatch");
        RosterConsistencyReport report = RosterConsistencyAnalyzer.Analyze(batch);
        TeamTrialResult majority = report.MajorityTrial!;
        TeamTrialResult different = report.Trials.Single(trial =>
            string.Equals(FileName(batch, trial), "different team.json", StringComparison.OrdinalIgnoreCase));

        Assert.NotEqual(majority.RosterCompositionKey, different.RosterCompositionKey);
        Assert.Equal(3, report.Trials.Count(trial => trial.RosterCompositionKey == majority.RosterCompositionKey));

        int differentIndex = report.Trials.ToList().FindIndex(trial => ReferenceEquals(trial, different));
        Assert.Contains(true, OutlierRow(report, differentIndex));
        foreach (TeamTrialResult trial in report.Trials.Where(trial => trial.RosterCompositionKey == majority.RosterCompositionKey))
        {
            int index = report.Trials.ToList().FindIndex(candidate => ReferenceEquals(candidate, trial));
            Assert.All(OutlierRow(report, index), flagged => Assert.False(flagged));
        }

        HashSet<int> shared = report.Trials
            .Select(trial => trial.RosterTrainedCharaIds.ToHashSet())
            .Aggregate((left, right) => left.Intersect(right).ToHashSet());
        Assert.Equal(11, shared.Count);
        Assert.Equal(shared.OrderBy(id => id), report.StableCore.Select(uma => uma.TrainedCharaId).OrderBy(id => id));
        Assert.Equal(4, different.RosterTrainedCharaIds.Except(shared).Count());
        AssertDisplayNames(report);
    }

    [Fact]
    public void Shuffle_inside_a_sub_team_stays_in_the_stable_core_and_is_not_an_outlier()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-SwapTest");
        RosterConsistencyReport report = RosterConsistencyAnalyzer.Analyze(batch);

        Assert.Equal(15, report.StableCore.Count);
        Assert.Single(report.Trials.Select(trial => trial.RosterCompositionKey).Distinct());
        Assert.True(report.Trials.Select(PositionalKey).Distinct(StringComparer.Ordinal).Count() > 1);

        for (int file = 0; file < report.Trials.Count; file++)
        {
            Assert.All(OutlierRow(report, file), flagged => Assert.False(flagged));
        }
    }

    [Fact]
    public void Equal_sized_roster_groups_keep_the_group_that_appeared_first()
    {
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-Mismatch");
        TeamTrialResult anchor = trials[0];
        string anchorKey = PositionalKey(anchor);
        TeamTrialResult other = trials.First(trial => PositionalKey(trial) != anchorKey);

        RosterConsistencyReport otherFirst = RosterConsistencyAnalyzer.Analyze(
            new[] { other, anchor, other, anchor });
        Assert.Equal(PositionalKey(other), PositionalKey(otherFirst.MajorityTrial!));

        RosterConsistencyReport anchorFirst = RosterConsistencyAnalyzer.Analyze(
            new[] { anchor, other, anchor, other });
        Assert.Equal(anchorKey, PositionalKey(anchorFirst.MajorityTrial!));
    }

    [Fact]
    public void Empty_batch_has_no_majority_and_no_stable_core()
    {
        RosterConsistencyReport report = RosterConsistencyAnalyzer.Analyze(Array.Empty<TeamTrialResult>());
        Assert.Null(report.MajorityTrial);
        Assert.Empty(report.StableCore);
        Assert.Equal(0, report.Outliers.GetLength(0));
        Assert.Equal(RosterConsistencyAnalyzer.SlotCount, report.Outliers.GetLength(1));
    }

    [Fact]
    public void Distance_and_running_style_names_match_the_roster_labels()
    {
        Assert.Equal(new[] { "Sprint", "Mile", "Medium", "Long", "Dirt" },
            new[] { 1, 2, 3, 4, 5 }.Select(DistanceTypeNames.Format).ToArray());
        Assert.Equal("?", DistanceTypeNames.Format(0));
        Assert.Equal("Race 0", DistanceTypeNames.FormatAnalysis(0));
        Assert.Equal("Sprint", DistanceTypeNames.FormatTeamSlot(0));
        Assert.Equal("Team 6", DistanceTypeNames.FormatTeamSlot(5));

        Assert.Equal(new[] { "Front", "Pace", "Late", "End" },
            new[] { 1, 2, 3, 4 }.Select(RunningStyleNames.FormatRoster).ToArray());
        Assert.Equal("9", RunningStyleNames.FormatRoster(9));
        Assert.Equal("#9", RunningStyleNames.FormatAnalysis(9));
    }

    static void AssertDisplayNames(RosterConsistencyReport report)
    {
        TeamTrialResult first = report.Trials[0];
        var names = new Dictionary<int, string>();
        IReadOnlyList<int> ids = first.RosterTrainedCharaIds;
        IReadOnlyList<string> rosterNames = first.RosterNames;
        for (int i = 0; i < ids.Count; i++)
        {
            names[ids[i]] = rosterNames[i];
        }

        foreach (StableRosterUma uma in report.StableCore)
        {
            (int distance, int style) = first.RosterDetails[uma.TrainedCharaId];
            string distanceName = distance switch
            {
                1 => "Sprint",
                2 => "Mile",
                3 => "Medium",
                4 => "Long",
                5 => "Dirt",
                _ => "?",
            };
            string styleName = style switch
            {
                1 => "Front",
                2 => "Pace",
                3 => "Late",
                4 => "End",
                _ => style.ToString(),
            };
            Assert.Equal($"{distanceName}: {names[uma.TrainedCharaId]} ({styleName})", uma.DisplayName);
        }
    }

    static int IndexOf(TeamTrialResult trial, int trainedCharaId)
    {
        int index = trial.RosterTrainedCharaIds.ToList().IndexOf(trainedCharaId);
        Assert.True(index >= 0);
        return index;
    }

    static string FileName(TeamTrialBatch batch, TeamTrialResult trial) =>
        batch.Trials.Single(pair => ReferenceEquals(pair.Value, trial)).Key;

    static string PositionalKey(TeamTrialResult trial) =>
        string.Join(",", trial.RosterTrainedCharaIds);

    static IEnumerable<bool> OutlierRow(RosterConsistencyReport report, int file)
    {
        for (int slot = 0; slot < RosterConsistencyAnalyzer.SlotCount; slot++)
        {
            yield return report.Outliers[file, slot];
        }
    }
}
