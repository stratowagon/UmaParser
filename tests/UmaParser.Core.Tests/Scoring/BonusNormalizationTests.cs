using UmaParser.Core.Tests.TestSupport;
using UmaParser.DataModel.ResponseData;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Scoring;

public sealed class BonusNormalizationTests
{
    public static IEnumerable<object[]> TeamTrialFiles() =>
        FixtureCatalog.TeamTrials.Select(entry => new object[] { entry.RelativePath });

    [Theory]
    [MemberData(nameof(TeamTrialFiles))]
    public void Score_buckets_follow_the_bonus_model(string relativePath)
    {
        var trial = Assert.IsType<TeamTrialResult>(FixtureCatalog.Import(FixtureCatalog.Require(relativePath)).Response);
        Assert.NotNull(trial.Data);

        int wins = trial.Data.RaceResultArray.Count(race => race.WinType == 1);
        if (wins < 3)
        {
            Assert.Equal(0, trial.MatchBonus);
        }
        else
        {
            Assert.True(trial.MatchBonus > 0, relativePath);
        }

        int raceScoreSum = 0;
        foreach (RaceResult race in trial.Data.RaceResultArray)
        {
            List<TeamTrialsScore> buckets = race.TeamScoreArray
                .Concat(race.CharaResultArray.Where(chara => chara.TeamId == 1).SelectMany(chara => chara.ScoreArray))
                .ToList();
            int displayed = buckets.Sum(bucket => bucket.Score);
            Assert.True(race.TeamTotalScore == displayed,
                $"{relativePath} round {race.Round}: team_total_score {race.TeamTotalScore} vs buckets {displayed}");
            raceScoreSum += race.TeamTotalScore;

            foreach (TeamTrialsScore bucket in buckets)
            {
                CheckBucket(relativePath, race.Round, bucket, trial);
            }
        }

        Assert.Equal(raceScoreSum + trial.MatchBonus, trial.TotalScore);

        foreach (int id in trial.RosterTrainedCharaIds)
        {
            Assert.Equal(trial.UmaBaseScore(id), trial.UmaSkillBaseScore(id) + trial.UmaOtherBaseScore(id));
        }
    }

    static void CheckBucket(string relativePath, int round, TeamTrialsScore bucket, TeamTrialResult trial)
    {
        int bonusSum = bucket.BonusArray.Sum(bonus => bonus.BonusScore);
        int baseScore = bucket.Score - bonusSum;
        Assert.Equal(baseScore, bucket.BaseScore);

        bool rushed = bucket.RawScoreId is 23 or 24;
        if (rushed)
        {
            Assert.True(bonusSum == 0, $"{relativePath} round {round} raw {bucket.RawScoreId} has bonuses {bonusSum}");
            Assert.True(baseScore < 0, $"{relativePath} round {round} raw {bucket.RawScoreId} base {baseScore}");
            return;
        }

        Assert.True(baseScore >= 0,
            $"{relativePath} round {round} raw {bucket.RawScoreId} base {baseScore} is negative");

        foreach (ScoreBonus bonus in bucket.BonusArray)
        {
            double predicted = bonus.ScoreBonusId switch
            {
                ScoreBonusType.Ace
                    or ScoreBonusType.Streak2
                    or ScoreBonusType.Streak3
                    or ScoreBonusType.Streak4
                    or ScoreBonusType.Streak5 => baseScore * (double)bonus.ScoreRate / 10000.0,
                ScoreBonusType.Support => baseScore * (double)trial.SupportCardBonus / 10000.0,
                // OpponentRating's score_rate is a constant, not the real rate. The rate that
                // reproduces the bonus is OpponentMultiplier. Integer bonuses on a small base
                // cannot stay inside 0.1% (1 point on a base of 100 is already 1%), so the
                // check is the same +/- 1 used for the other bonuses. That also keeps every
                // bucket in the run on one rate. For |base| >= 1000, +/- 1 is inside 0.1%.
                ScoreBonusType.OpponentRating => baseScore * (double)trial.OpponentMultiplier,
                _ => throw new InvalidOperationException(
                    $"{relativePath} unexpected bonus {bonus.ScoreBonusId}"),
            };

            double error = Math.Abs(bonus.BonusScore - predicted);
            Assert.True(error <= 1,
                $"{relativePath} round {round} raw {bucket.RawScoreId} {bonus.ScoreBonusId}: " +
                $"bonus {bonus.BonusScore} vs {predicted:F3} (base {baseScore})");
        }
    }
}
