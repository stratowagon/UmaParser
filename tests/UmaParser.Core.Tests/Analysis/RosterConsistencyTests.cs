using UmaParser.Core.Tests.TestSupport;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class RosterConsistencyTests
{
    [Fact]
    public void Style_change_is_two_rosters_differing_only_by_one_running_style()
    {
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-StyleChange");
        var groups = trials.GroupBy(trial => trial.RosterCompositionKey).ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { 3, 5 }, groups.Select(group => group.Count()).OrderBy(count => count).ToArray());

        Dictionary<int, (int Distance, int Style)> left = Parse(groups[0].Key);
        Dictionary<int, (int Distance, int Style)> right = Parse(groups[1].Key);
        Assert.Equal(left.Keys.OrderBy(id => id), right.Keys.OrderBy(id => id));

        var different = left.Keys.Where(id => left[id] != right[id]).ToList();
        int changed = Assert.Single(different);
        Assert.Equal(1788, changed);
        Assert.Equal(left[changed].Distance, right[changed].Distance);
        Assert.NotEqual(left[changed].Style, right[changed].Style);
    }

    [Fact]
    public void Mismatch_shapes_share_a_key_and_the_other_file_swaps_four_umas()
    {
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-Mismatch");
        TeamTrialResult different = trials.Single(trial =>
            string.Equals(NameOf(trial), "different team.json", StringComparison.OrdinalIgnoreCase));
        TeamTrialResult[] shapes = trials.Where(trial => !ReferenceEquals(trial, different)).ToArray();
        Assert.Equal(3, shapes.Length);
        Assert.Single(shapes.Select(trial => trial.RosterCompositionKey).Distinct());

        var shapeIds = Parse(shapes[0].RosterCompositionKey).Keys.ToHashSet();
        var otherIds = Parse(different.RosterCompositionKey).Keys.ToHashSet();
        Assert.Equal(4, shapeIds.Except(otherIds).Count());
        Assert.Equal(4, otherIds.Except(shapeIds).Count());
    }

    static string NameOf(TeamTrialResult trial)
    {
        // The batch dictionary key is the file name. Recover it from the batch for this group.
        return TrialSets.Build("TT-Mismatch").Trials
            .Single(pair => ReferenceEquals(pair.Value, trial))
            .Key;
    }

    static Dictionary<int, (int Distance, int Style)> Parse(string key)
    {
        var parsed = new Dictionary<int, (int Distance, int Style)>();
        foreach (string part in key.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] bits = part.Split(':');
            parsed[int.Parse(bits[0])] = (int.Parse(bits[1]), int.Parse(bits[2]));
        }

        return parsed;
    }
}
