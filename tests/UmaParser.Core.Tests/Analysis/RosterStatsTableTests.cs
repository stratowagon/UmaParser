using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class RosterStatsTableTests
{
    [Fact]
    public void Scores_follow_the_newest_file_even_when_a_sub_team_is_shuffled()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-SwapTest");
        List<KeyValuePair<string, TeamTrialResult>> files = batch.Trials.ToList();
        RosterStatsTable table = RosterStatsTableBuilder.Build(batch);

        Assert.Equal(files.Select(file => file.Key), table.FileNames);
        TeamTrialResult layout = files[^1].Value;
        Assert.Equal(layout.RosterNames, table.Names);
        Assert.Equal(layout.RosterTrainedCharaIds, table.TrainedCharaIds);

        bool sawShuffle = false;
        for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            TeamTrialResult trial = files[fileIndex].Value;
            Assert.Equal(trial.TotalScore, table.Totals[fileIndex]);

            IReadOnlyList<int> ids = trial.RosterTrainedCharaIds;
            IReadOnlyList<int> scores = trial.RosterScores;
            var scoreById = new Dictionary<int, int>();
            for (int i = 0; i < ids.Count; i++)
            {
                scoreById[ids[i]] = scores[i];
            }

            for (int row = 0; row < table.TrainedCharaIds.Count; row++)
            {
                int id = table.TrainedCharaIds[row];
                Assert.Equal(scoreById[id], table.Scores[row, fileIndex]);
                if (ids[row] != id)
                {
                    sawShuffle = true;
                    if (scores[row] != scoreById[id])
                    {
                        Assert.NotEqual(scores[row], table.Scores[row, fileIndex]);
                    }
                }
            }
        }

        Assert.True(sawShuffle);

        List<NamedRosterUma> listing = RosterListing.FromTrial(files[0].Value);
        Assert.Equal(files[0].Value.RosterTrainedCharaIds, listing.Select(entry => entry.TrainedCharaId));
        Assert.Equal(files[0].Value.RosterNames, listing.Select(entry => entry.Name));
    }

    [Fact]
    public void Consistent_roster_matrix_matches_each_file_total()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        RosterStatsTable table = RosterStatsTableBuilder.Build(batch);
        Assert.Equal(15, table.Names.Count);
        Assert.Equal(batch.Trials.Count, table.FileNames.Count);
        Assert.Equal(batch.Trials.Select(file => file.Value.TotalScore), table.Totals);
    }
}
