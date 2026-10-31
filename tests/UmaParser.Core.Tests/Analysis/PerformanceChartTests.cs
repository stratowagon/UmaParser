using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class PerformanceChartTests
{
    [Fact]
    public void Linear_percentile_interpolates_like_team_analysis()
    {
        double[] values = [10, 20, 30, 40];
        Assert.Equal(17.5, ScorePercentile.Linear(values, 0.25));
        Assert.Equal(25, ScorePercentile.Linear(values, 0.5));
        Assert.Equal(32.5, ScorePercentile.Linear(values, 0.75));
        Assert.Equal(37, ScorePercentile.Linear(values, 0.9));
        Assert.Equal(10, values.Min());
        Assert.Equal(40, values.Max());

        Assert.Equal(3, ScorePercentile.Linear([1, 2, 3, 4, 5], 0.5));
        Assert.Equal(4.6, ScorePercentile.Linear([1, 2, 3, 4, 5], 0.9));
        Assert.Equal(7, ScorePercentile.Linear([7], 0.25));
        Assert.Equal(7, ScorePercentile.Linear([7], 0.9));
        Assert.Equal(0, ScorePercentile.Linear([], 0.9));
    }

    [Fact]
    public void Master_labels_choose_the_segment_without_a_hard_coded_id_list()
    {
        GameMasterCatalog catalog = GameMasterService.Current.Catalog;
        Assert.Equal(PerformanceScoreSource.FinishAndMargin, Source(catalog, "1st place"));
        Assert.Equal(PerformanceScoreSource.FinishAndMargin, Source(catalog, "12th place"));
        Assert.Equal(PerformanceScoreSource.FinishAndMargin, Source(catalog, "Nose"));
        Assert.Equal(PerformanceScoreSource.FinishAndMargin, Source(catalog, "10 Lengths"));
        Assert.Equal(PerformanceScoreSource.SkillWhite, Source(catalog, "Skill Activated"));
        Assert.Equal(PerformanceScoreSource.SkillGold, Source(catalog, "Rare Skill Activated"));
        Assert.Equal(PerformanceScoreSource.SkillUnique, Source(catalog, "Unique Skill Lvl 1 Activated (1-2★)"));
        Assert.Equal(PerformanceScoreSource.SkillUnique, Source(catalog, "Unique Skill Lvl 10 Activated (3★+)"));
        Assert.Equal(PerformanceScoreSource.StrongStart, Source(catalog, "Strong Start"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "Good Positioning (Mid-Race)"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "Good Positioning (Late-Race)"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "Rushed"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "Rushed (1 sec)"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "Beat Target Time (0.1 secs)"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "Long Shot"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "1st, 2nd & 3rd Place"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, Source(catalog, "Victory"));
        Assert.Equal(PerformanceScoreSource.StyleAndExtras, PerformanceScoreSources.Classify(999_999, catalog));

        foreach (TeamTrialsScoreEntry entry in catalog.TeamTrialsScores.Values)
        {
            PerformanceScoreSource source = PerformanceScoreSources.Classify(entry);
            if (entry.Name.Contains("Unique Skill", StringComparison.Ordinal))
            {
                Assert.Equal(PerformanceScoreSource.SkillUnique, source);
            }
            else if (entry.Name.Contains("Rare Skill", StringComparison.Ordinal))
            {
                Assert.Equal(PerformanceScoreSource.SkillGold, source);
            }
            else if (entry.Name == "Skill Activated")
            {
                Assert.Equal(PerformanceScoreSource.SkillWhite, source);
            }
            else if (entry.Name == "Strong Start")
            {
                Assert.Equal(PerformanceScoreSource.StrongStart, source);
            }
            else if (IsOrdinalPlace(entry.Name) || entry.Description.Contains("winning by", StringComparison.Ordinal))
            {
                Assert.Equal(PerformanceScoreSource.FinishAndMargin, source);
            }
            else
            {
                Assert.Equal(PerformanceScoreSource.StyleAndExtras, source);
            }
        }
    }

    [Fact]
    public void Segments_use_base_scores_and_sum_to_the_average()
    {
        TeamTrialResult trial = Trial(
            (1, 1001, 1, 1,
            [
                Bucket(60, 10_000),
                Bucket(19, 5_000, bonus: 500),
                Bucket(57, 500),
                Bucket(56, 1_200),
                Bucket(35, 1_500),
                Bucket(25, 1_000),
                Bucket(58, 1_000),
                Bucket(23, -500),
            ]));

        PerformanceChart chart = PerformanceChartEngine.Build([trial]);
        PerformanceUmaRow row = Assert.Single(chart.Rows);

        Assert.True(chart.Available);
        Assert.Equal(1, chart.TrialCount);
        Assert.Equal("Special Week", row.Name);
        Assert.Equal("Sprint · Ace · Front", row.Subtitle);
        Assert.True(row.IsAce);
        Assert.Equal(19_700, row.Average);
        Assert.Equal(15_000, Segment(row, PerformanceScoreSource.FinishAndMargin));
        Assert.Equal(500, Segment(row, PerformanceScoreSource.SkillWhite));
        Assert.Equal(1_200, Segment(row, PerformanceScoreSource.SkillGold));
        Assert.Equal(1_500, Segment(row, PerformanceScoreSource.SkillUnique));
        Assert.Equal(1_000, Segment(row, PerformanceScoreSource.StrongStart));
        Assert.Equal(500, Segment(row, PerformanceScoreSource.StyleAndExtras));
        Assert.Equal(3_200, row.SkillAverage);
        Assert.Equal(row.Average, row.Segments.Sum(segment => segment.Average), 6);
        Assert.Equal(19_700, row.Min);
        Assert.Equal(19_700, row.Max);
        Assert.Equal(19_700, row.Median);
        Assert.Equal(19_700, row.P90);
        Assert.Contains("Finish + margin    15,000", row.Tooltip);
        Assert.Contains("Skill total    3,200", row.Tooltip);
        Assert.DoesNotContain("20,200", row.Tooltip);
        Assert.Equal(19_700, chart.TeamAverage);
    }

    [Fact]
    public void Sort_keys_reorder_rows_and_keep_roster_order_on_ties()
    {
        // A is spiky: low average and median, high max and P90.
        // B is flat and higher on average, median, and P75.
        int[] a = [0, 0, 0, 10_000];
        int[] b = [3_000, 3_000, 3_000, 3_000];
        var trials = new List<TeamTrialResult>();
        for (int i = 0; i < 4; i++)
        {
            trials.Add(Trial(
                (1, 1001, 1, 1, [Bucket(60, a[i])]),
                (2, 1002, 2, 4, [Bucket(60, b[i])])));
        }

        PerformanceChart chart = PerformanceChartEngine.Build(trials);
        Assert.Equal([1, 2], chart.Rows.Select(row => row.TrainedCharaId));

        PerformanceUmaRow spike = chart.Rows[0];
        PerformanceUmaRow flat = chart.Rows[1];
        Assert.Equal(2_500, spike.Average);
        Assert.Equal(0, spike.Median);
        Assert.Equal(10_000, spike.Max);
        Assert.Equal(2_500, spike.P75);
        Assert.Equal(7_000, spike.P90, 6);
        Assert.Equal(3_000, flat.Average);
        Assert.Equal(3_000, flat.Median);
        Assert.Equal(3_000, flat.Max);
        Assert.Equal(3_000, flat.P75);
        Assert.Equal(3_000, flat.P90);
        Assert.Equal("End", flat.Style);
        Assert.False(flat.IsAce);

        Assert.Equal([1, 2], Ids(chart, PerformanceSortKey.Average, ascending: true));
        Assert.Equal([2, 1], Ids(chart, PerformanceSortKey.Average, ascending: false));
        Assert.Equal([1, 2], Ids(chart, PerformanceSortKey.Median, ascending: true));
        Assert.Equal([2, 1], Ids(chart, PerformanceSortKey.Max, ascending: true));
        Assert.Equal([1, 2], Ids(chart, PerformanceSortKey.Max, ascending: false));
        Assert.Equal([1, 2], Ids(chart, PerformanceSortKey.P75, ascending: true));
        Assert.Equal([2, 1], Ids(chart, PerformanceSortKey.P90, ascending: true));
        Assert.Equal([1, 2], Ids(chart, PerformanceSortKey.P90, ascending: false));

        PerformanceChart tied = PerformanceChartEngine.Build(
        [
            Trial(
                (1, 1001, 1, 1, [Bucket(60, 4_000)]),
                (2, 1002, 2, 2, [Bucket(60, 4_000)])),
        ]);
        Assert.Equal([1, 2], Ids(tied, PerformanceSortKey.Average, ascending: true));
        Assert.Equal([1, 2], Ids(tied, PerformanceSortKey.P90, ascending: false));
        Assert.Equal(4_000, tied.Sorted(PerformanceSortKey.Max, ascending: false).TeamAverage);
    }

    [Fact]
    public void Two_trials_interpolate_the_spread_and_strip_bonuses()
    {
        PerformanceChart chart = PerformanceChartEngine.Build(
        [
            Trial((1, 1001, 1, 1, [Bucket(60, 10_000, bonus: 1_000)])),
            Trial((1, 1001, 1, 1, [Bucket(60, 20_000)])),
        ]);
        PerformanceUmaRow row = Assert.Single(chart.Rows);

        Assert.Equal(15_000, row.Average);
        Assert.Equal(15_000, row.Median);
        Assert.Equal(12_500, row.P25);
        Assert.Equal(17_500, row.P75);
        Assert.Equal(19_000, row.P90);
        Assert.Equal(10_000, row.Min);
        Assert.Equal(20_000, row.Max);
        Assert.Contains("bonuses removed", PerformanceChartEngine.Describe(chart));
        Assert.Contains("15,000", PerformanceChartEngine.Describe(chart));
        Assert.Contains("0.02s", PerformanceChartEngine.Describe(chart));
    }

    [Fact]
    public void A_consistent_roster_matches_team_analysis_base_scores()
    {
        IReadOnlyList<TeamTrialResult> trials = TrialSets.Load("TT-K0");
        PerformanceChart chart = PerformanceChartEngine.Build(trials);
        UmaAnalysisReport analysis = UmaAnalysisEngine.Analyze(trials.ToList());

        Assert.True(chart.Available);
        Assert.Equal(trials.Count, chart.TrialCount);
        Assert.Equal(15, chart.Rows.Count);
        Assert.Equal(analysis.TeamAverage, Math.Round(chart.TeamAverage, 0, MidpointRounding.AwayFromZero));

        GameMasterCatalog catalog = GameMasterService.Current.Catalog;
        foreach (PerformanceUmaRow row in chart.Rows)
        {
            double[] bases = trials.Select(trial => (double)trial.UmaBaseScore(row.TrainedCharaId)).ToArray();
            double[] skills = trials.Select(trial => (double)trial.UmaSkillBaseScore(row.TrainedCharaId)).ToArray();
            Assert.Equal(bases.Average(), row.Average, 6);
            Assert.Equal(skills.Average(), row.SkillAverage, 6);
            Assert.Equal(row.Average, row.Segments.Sum(segment => segment.Average), 6);
            Assert.Equal(ScorePercentile.Linear(bases, 0.25), row.P25);
            Assert.Equal(ScorePercentile.Linear(bases, 0.5), row.Median);
            Assert.Equal(ScorePercentile.Linear(bases, 0.75), row.P75);
            Assert.Equal(ScorePercentile.Linear(bases, 0.9), row.P90);
            Assert.Equal(bases.Min(), row.Min);
            Assert.Equal(bases.Max(), row.Max);
            Assert.Contains(row.Name, row.Tooltip);
            Assert.Contains("Skill total", row.Tooltip);

            SkillActivationReport report = SkillActivationAnalyzer.Analyze(trials, row.TrainedCharaId, row.Name);
            Assert.Equal(report.Rows.Select(skill => skill.SkillId), row.Skills.Select(skill => skill.SkillId));
            // Scenario score events and the score-array buckets are the same skill points,
            // aside from a point or two of 1:1 pairing at a shared frame.
            double attributed = report.Rows.Sum(skill => skill.PointsPerRace);
            Assert.InRange(Math.Abs(attributed - row.SkillAverage), 0, Math.Max(50, row.SkillAverage * 0.05));
            if (report.Rows.Count > 0)
            {
                Assert.Contains(report.Rows[0].SkillName, row.Tooltip);
                Assert.Contains("%", row.Tooltip);
            }

            foreach (TeamTrialResult trial in trials)
            {
                foreach (TeamTrialsScore bucket in trial.Results[row.TrainedCharaId].ScoreArray)
                {
                    Assert.True(catalog.TryGetTeamTrialsScore(bucket.RawScoreId, out _), $"raw score {bucket.RawScoreId}");
                }
            }
        }

        PerformanceChart weakestFirst = chart.Sorted(PerformanceSortKey.Average, ascending: true);
        for (int i = 1; i < weakestFirst.Rows.Count; i++)
        {
            Assert.True(weakestFirst.Rows[i].Average >= weakestFirst.Rows[i - 1].Average);
        }

        PerformanceChart peakFirst = chart.Sorted(PerformanceSortKey.P90, ascending: false);
        Assert.Equal(chart.Rows.Max(row => row.P90), peakFirst.Rows[0].P90);
        Assert.Equal(chart.TeamAverage, peakFirst.TeamAverage);
    }

    [Fact]
    public void A_roster_that_is_not_consistent_has_no_chart()
    {
        PerformanceChart mismatch = PerformanceChartEngine.Build(TrialSets.Build("TT-Mismatch"));
        PerformanceChart styleChange = PerformanceChartEngine.Build(TrialSets.Build("TT-StyleChange"));
        PerformanceChart empty = PerformanceChartEngine.Build(Array.Empty<TeamTrialResult>());

        Assert.False(mismatch.Available);
        Assert.Empty(mismatch.Rows);
        Assert.Equal(PerformanceChartEngine.UnavailableMessage, mismatch.EmptyMessage);
        Assert.False(styleChange.Available);
        Assert.False(empty.Available);
        Assert.Equal(PerformanceChartEngine.DropMessage, empty.EmptyMessage);
        Assert.Same(empty, empty.Sorted(PerformanceSortKey.Max, ascending: false));
    }

    private static PerformanceScoreSource Source(GameMasterCatalog catalog, string name) =>
        PerformanceScoreSources.Classify(catalog.TeamTrialsScores.Values.First(entry => entry.Name == name));

    private static bool IsOrdinalPlace(string name)
    {
        int space = name.IndexOf(' ');
        if (space < 3)
        {
            return false;
        }

        ReadOnlySpan<char> suffix = name.AsSpan(0, space)[^2..];
        return name.AsSpan(space + 1).Equals("place", StringComparison.OrdinalIgnoreCase)
            && (suffix.Equals("st", StringComparison.OrdinalIgnoreCase)
                || suffix.Equals("nd", StringComparison.OrdinalIgnoreCase)
                || suffix.Equals("rd", StringComparison.OrdinalIgnoreCase)
                || suffix.Equals("th", StringComparison.OrdinalIgnoreCase));
    }

    private static double Segment(PerformanceUmaRow row, PerformanceScoreSource source) =>
        row.Segments.Single(segment => segment.Source == source).Average;

    private static IReadOnlyList<int> Ids(PerformanceChart chart, PerformanceSortKey key, bool ascending) =>
        chart.Sorted(key, ascending).Rows.Select(row => row.TrainedCharaId).ToList();

    private static TeamTrialsScore Bucket(int rawScoreId, int baseScore, int bonus = 0)
    {
        var score = new TeamTrialsScore
        {
            RawScoreId = rawScoreId,
            Score = baseScore + bonus,
        };
        if (bonus != 0)
        {
            score.BonusArray.Add(new ScoreBonus
            {
                ScoreBonusId = ScoreBonusType.Ace,
                BonusScore = bonus,
            });
        }

        return score;
    }

    private static TeamTrialResult Trial(
        params (int TrainedId, int CharaId, int Member, int Style, TeamTrialsScore[] Scores)[] umas)
    {
        var horses = new List<RaceHorseData>(umas.Length);
        var results = new List<CharaResult>(umas.Length);
        foreach ((int trainedId, int charaId, int member, int style, TeamTrialsScore[] scores) in umas)
        {
            horses.Add(new RaceHorseData
            {
                TrainedCharaId = trainedId,
                CharaId = charaId,
                TeamId = 1,
                TeamMemberId = member,
                RunningStyle = style,
                FrameOrder = member,
            });
            results.Add(new CharaResult
            {
                TrainedCharaId = trainedId,
                TeamId = 1,
                ScoreArray = scores.ToList(),
            });
        }

        return new TeamTrialResult(new TeamRaceData
        {
            RaceStartParamsArray =
            [
                new RaceStartParams
                {
                    Round = 1,
                    RaceHorseDataArray = horses,
                },
            ],
            RaceResultArray =
            [
                new RaceResult
                {
                    Round = 1,
                    DistanceType = 1,
                    CharaResultArray = results,
                },
            ],
        });
    }
}
