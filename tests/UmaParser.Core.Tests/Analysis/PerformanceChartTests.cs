using System.Text.Json;
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
        Assert.Equal("Sprint · Front · 1 race", row.Subtitle);
        Assert.Equal(1, row.TrialCount);
        Assert.True(row.InCurrentLineup);
        Assert.Equal(PerformanceSampleWarning.Hard, row.SampleWarning);
        Assert.DoesNotContain("Ace", row.Subtitle);
        Assert.DoesNotContain("Support", row.Subtitle);
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
        Assert.Contains("Special Week · Sprint · Front", row.Tooltip);
        Assert.Contains("Average 19,700    Median 19,700    P90 19,700", row.Tooltip);
        Assert.Contains("Finish + margin    15,000", row.Tooltip);
        Assert.Contains("White skills    500", row.Tooltip);
        Assert.Equal(8, row.Tooltip.Split('\n').Length);
        Assert.DoesNotContain("Ace", row.Tooltip);
        Assert.DoesNotContain("Support", row.Tooltip);
        Assert.DoesNotContain("%", row.Tooltip);
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
        Assert.Equal("Sprint · End · 4 races", flat.Subtitle);
        Assert.Equal(PerformanceSampleWarning.Hard, flat.SampleWarning);
        Assert.DoesNotContain("Support", flat.Tooltip);

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
        Assert.Contains("current lineup", PerformanceChartEngine.Describe(chart));
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
            Assert.Contains("Average ", row.Tooltip);
            Assert.Contains("Median ", row.Tooltip);
            Assert.Contains("P90 ", row.Tooltip);
            Assert.Contains(row.Subtitle, row.Tooltip);
            Assert.Equal(trials.Count, row.TrialCount);
            Assert.True(row.InCurrentLineup);
            Assert.Equal(PerformanceSampleThresholds.Default.Classify(row.TrialCount), row.SampleWarning);
            Assert.Contains(PerformanceChartEngine.FormatRaceCount(row.TrialCount), row.Subtitle);
            Assert.DoesNotContain("Ace", row.Subtitle);
            Assert.DoesNotContain("Support", row.Subtitle);
            Assert.DoesNotContain("%", row.Tooltip);
            Assert.True(row.Tooltip.Split('\n').Length <= 8, row.Tooltip);

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
    public void A_roster_that_is_not_consistent_still_charts_every_trained_uma()
    {
        TeamTrialBatch mismatch = TrialSets.Build("TT-Mismatch");
        PerformanceChart chart = PerformanceChartEngine.Build(mismatch);
        TeamTrialResult newest = mismatch.MostRecentTrial!;

        Assert.True(chart.Available);
        Assert.True(chart.Rows.Count > 15);
        Assert.Equal(newest.RosterTrainedCharaIds, chart.Rows.Where(row => row.InCurrentLineup).Select(row => row.TrainedCharaId));
        Assert.Contains(chart.Rows, row => !row.InCurrentLineup);
        Assert.Equal(
            chart.Rows.Where(row => row.InCurrentLineup).Average(row => row.Average),
            chart.TeamAverage);
        Assert.Equal(chart.TeamAverage, chart.Sorted(PerformanceSortKey.Average, ascending: false).TeamAverage);

        foreach (PerformanceUmaRow row in chart.Rows)
        {
            List<TeamTrialResult> appearances = mismatch.Trials.Values
                .Where(trial => SameRow(trial, row))
                .ToList();
            Assert.Equal(appearances.Count, row.TrialCount);
            Assert.True(row.TrialCount < mismatch.Trials.Count || row.InCurrentLineup);
            double[] bases = appearances
                .Where(trial => trial.Results.ContainsKey(row.TrainedCharaId))
                .Select(trial => (double)trial.UmaBaseScore(row.TrainedCharaId))
                .ToArray();
            Assert.Equal(bases.Average(), row.Average, 6);
        }

        TeamTrialBatch styleChange = TrialSets.Build("TT-StyleChange");
        PerformanceChart styles = PerformanceChartEngine.Build(styleChange);
        int changedId = styleChange.Trials.Values
            .SelectMany(trial => trial.RaceRoster)
            .GroupBy(pair => pair.Key)
            .Where(group => group.Select(pair => pair.Value.RunningStyle).Distinct().Count() > 1)
            .Select(group => group.Key)
            .Single();
        List<PerformanceUmaRow> changedRows = styles.Rows.Where(row => row.TrainedCharaId == changedId).ToList();
        TeamTrialResult newestStyle = styleChange.MostRecentTrial!;
        int changedSlot = newestStyle.RosterTrainedCharaIds.ToList().IndexOf(changedId);
        RaceHorseData newestHorse = newestStyle.RaceRoster[changedId];
        int expectedRows = styleChange.Trials.Values
            .Select(trial => (
                trial.RaceRoster[changedId].RunningStyle,
                trial.FindRaceResultByUma(trial.RaceRoster[changedId])?.DistanceType))
            .Distinct()
            .Count();

        Assert.True(styles.Available);
        Assert.Equal(expectedRows, changedRows.Count);
        Assert.True(styles.Rows.Count > 15);
        Assert.Equal(2, changedRows.Select(row => row.Style).Distinct().Count());
        Assert.Equal(styleChange.Trials.Count, changedRows.Sum(row => row.TrialCount));
        Assert.Equal(
            newestStyle.RosterTrainedCharaIds,
            styles.Rows.Where(row => row.InCurrentLineup).Select(row => row.TrainedCharaId));
        PerformanceUmaRow currentStyle = Assert.Single(changedRows, row => row.InCurrentLineup);
        Assert.Equal(RunningStyleNames.FormatAnalysis(newestHorse.RunningStyle), currentStyle.Style);
        Assert.Equal(DistanceLabel(newestStyle, newestHorse, changedSlot), currentStyle.Distance);
        foreach (PerformanceUmaRow row in changedRows)
        {
            double[] bases = styleChange.Trials.Values
                .Where(trial => SameRow(trial, row))
                .Select(trial => (double)trial.UmaBaseScore(changedId))
                .ToArray();
            Assert.Equal(bases.Length, row.TrialCount);
            Assert.Equal(bases.Average(), row.Average, 6);
        }

        _ = expectedRows;

        PerformanceChart empty = PerformanceChartEngine.Build(Array.Empty<TeamTrialResult>());
        PerformanceChart otherModes = PerformanceChartEngine.Build(TrialSets.Build("Single-CM12"));
        Assert.False(empty.Available);
        Assert.Equal(PerformanceChartEngine.DropMessage, empty.EmptyMessage);
        Assert.False(otherModes.Available);
        Assert.Equal(PerformanceChartEngine.NonTeamTrialsMessage, otherModes.EmptyMessage);
        Assert.Same(empty, empty.Sorted(PerformanceSortKey.Max, ascending: false));
    }

    [Fact]
    public void Mixed_rows_average_only_the_trials_that_uma_ran_and_the_line_is_the_current_lineup()
    {
        PerformanceChart chart = PerformanceChartEngine.Build(
        [
            Trial(
                (1, 1001, 1, 1, [Bucket(60, 10_000)]),
                (2, 1002, 2, 2, [Bucket(60, 30_000)])),
            Trial(
                (1, 1001, 1, 4, [Bucket(60, 20_000)]),
                (3, 1001, 2, 3, [Bucket(60, 40_000)])),
        ]);

        Assert.Equal(4, chart.Rows.Count);
        Assert.Equal([1, 3, 1, 2], chart.Rows.Select(row => row.TrainedCharaId));

        PerformanceUmaRow kept = chart.Rows[0];
        PerformanceUmaRow added = chart.Rows[1];
        PerformanceUmaRow oldStyle = chart.Rows[2];
        PerformanceUmaRow replaced = chart.Rows[3];
        Assert.True(kept.InCurrentLineup);
        Assert.True(added.InCurrentLineup);
        Assert.False(oldStyle.InCurrentLineup);
        Assert.False(replaced.InCurrentLineup);
        Assert.Equal(20_000, kept.Average);
        Assert.Equal(1, kept.TrialCount);
        Assert.Equal("End", kept.Style);
        Assert.Equal("Sprint · End · 1 race", kept.Subtitle);
        Assert.Equal(40_000, added.Average);
        Assert.Equal(1, added.TrialCount);
        Assert.Equal("Late", added.Style);
        Assert.Equal(10_000, oldStyle.Average);
        Assert.Equal(1, oldStyle.TrialCount);
        Assert.Equal("Front", oldStyle.Style);
        Assert.Equal(kept.TrainedCharaId, oldStyle.TrainedCharaId);
        Assert.Equal(30_000, replaced.Average);
        Assert.Equal(1, replaced.TrialCount);
        Assert.Equal("Pace", replaced.Style);
        Assert.Equal(30_000, chart.TeamAverage);
        Assert.Equal("Special Week", kept.ShortName);
        Assert.Equal("Special Week", added.ShortName);
        Assert.NotEqual(kept.TrainedCharaId, added.TrainedCharaId);
    }

    [Fact]
    public void Rank_score_labels_the_row_and_trained_id_keeps_two_builds_apart()
    {
        TeamTrialResult older = Trial(
            (1, 1001, 1, 1, [Bucket(60, 1_000)]),
            (2, 1001, 2, 2, [Bucket(60, 2_000)]));
        older.RaceRoster[1].RankScore = 18_317;
        older.RaceRoster[2].RankScore = 12_000;
        TeamTrialResult newer = Trial((1, 1001, 1, 1, [Bucket(60, 3_000)]));
        newer.Data!.UserTrainedCharaArrayCopy.Add(new TrainedChara
        {
            TrainedCharaId = 1,
            RankScore = 18_400,
        });

        PerformanceChart chart = PerformanceChartEngine.Build([older, newer]);
        PerformanceUmaRow first = chart.Rows.Single(row => row.TrainedCharaId == 1);
        PerformanceUmaRow second = chart.Rows.Single(row => row.TrainedCharaId == 2);

        Assert.Equal("Special Week [18,400]", first.Name);
        Assert.Equal(18_400, first.RankScore);
        Assert.Equal(2_000, first.Average);
        Assert.True(first.InCurrentLineup);
        Assert.Equal("Special Week [12,000]", second.Name);
        Assert.Equal(12_000, second.RankScore);
        Assert.Equal(2_000, second.Average);
        Assert.False(second.InCurrentLineup);
        Assert.Equal(2_000, chart.TeamAverage);
    }

    [Fact]
    public void A_capture_rank_score_on_the_trained_chara_copy_beats_the_horse()
    {
        const string json = """
            {
              "race_start_params_array": [
                {
                  "round": 1,
                  "race_horse_data_array": [
                    {
                      "trained_chara_id": 7,
                      "chara_id": 1001,
                      "team_id": 1,
                      "team_member_id": 1,
                      "running_style": 1,
                      "rank_score": 9000
                    }
                  ]
                }
              ],
              "race_result_array": [
                {
                  "round": 1,
                  "distance_type": 1,
                  "race_scenario": "",
                  "chara_result_array": [
                    {
                      "trained_chara_id": 7,
                      "team_id": 1,
                      "score_array": [
                        { "raw_score_id": 60, "score": 1000, "bonus_array": [] }
                      ]
                    }
                  ]
                }
              ],
              "user_trained_chara_array_copy": [
                { "trained_chara_id": 7, "rank_score": 18317 }
              ]
            }
            """;

        TeamRaceData data = JsonSerializer.Deserialize<TeamRaceData>(json, UmaCaptureJson.Options)!;
        PerformanceChart chart = PerformanceChartEngine.Build([new TeamTrialResult(data)]);
        PerformanceUmaRow row = Assert.Single(chart.Rows);

        Assert.Equal(18_317, row.RankScore);
        Assert.Equal("Special Week [18,317]", row.Name);
        Assert.Equal(1_000, row.Average);
        Assert.True(row.InCurrentLineup);
    }

    [Fact]
    public void HorseACT_trained_chara_data_rankScore_labels_the_row()
    {
        const string json = """
            {
              "race_start_params_array": [
                {
                  "round": 1,
                  "race_horse_data_array": [
                    {
                      "trained_chara_id": 7,
                      "chara_id": 1001,
                      "team_id": 1,
                      "team_member_id": 1,
                      "running_style": 3,
                      "trained_chara_data": {
                        "id": 7,
                        "rankScore": 18317
                      }
                    }
                  ]
                }
              ],
              "race_result_array": [
                {
                  "round": 1,
                  "distance_type": 1,
                  "race_scenario": "",
                  "chara_result_array": [
                    {
                      "trained_chara_id": 7,
                      "team_id": 1,
                      "score_array": [
                        { "raw_score_id": 60, "score": 1000, "bonus_array": [] }
                      ]
                    }
                  ]
                }
              ],
              "user_trained_chara_array_copy": []
            }
            """;

        TeamRaceData data = JsonSerializer.Deserialize<TeamRaceData>(json, UmaCaptureJson.Options)!;
        PerformanceChart chart = PerformanceChartEngine.Build([new TeamTrialResult(data)]);
        PerformanceUmaRow row = Assert.Single(chart.Rows);

        Assert.Equal(0, data.RaceStartParamsArray[0].RaceHorseDataArray[0].RankScore);
        Assert.Equal(18_317, row.RankScore);
        Assert.Equal("Special Week [18,317]", row.Name);
        Assert.Equal("Late", row.Style);
    }

    [Fact]
    public void A_later_style_still_shares_the_rating_learned_for_that_trained_uma()
    {
        TeamTrialResult older = Trial((1, 1001, 1, 1, [Bucket(60, 4_000)]));
        TeamTrialResult newer = Trial((1, 1001, 1, 4, [Bucket(60, 8_000)]));
        newer.Data!.UserTrainedCharaArrayCopy.Add(new TrainedChara
        {
            TrainedCharaId = 1,
            RankScoreCamel = 18_317,
        });

        PerformanceChart chart = PerformanceChartEngine.Build([older, newer]);
        Assert.Equal(2, chart.Rows.Count);
        Assert.All(chart.Rows, row => Assert.Equal("Special Week [18,317]", row.Name));
        Assert.Equal(["End", "Front"], chart.Rows.Select(row => row.Style));
        Assert.True(chart.Rows[0].InCurrentLineup);
        Assert.False(chart.Rows[1].InCurrentLineup);
        Assert.Equal(8_000, chart.TeamAverage);
    }

    [Theory]
    [InlineData(0, PerformanceSampleWarning.Hard)]
    [InlineData(9, PerformanceSampleWarning.Hard)]
    [InlineData(10, PerformanceSampleWarning.Soft)]
    [InlineData(19, PerformanceSampleWarning.Soft)]
    [InlineData(20, PerformanceSampleWarning.None)]
    [InlineData(21, PerformanceSampleWarning.None)]
    public void Default_sample_thresholds_are_red_below_10_and_yellow_below_20(int races, PerformanceSampleWarning warning)
    {
        Assert.Equal(warning, PerformanceSampleThresholds.Default.Classify(races));
    }

    [Fact]
    public void Custom_thresholds_restamp_warnings_without_moving_the_scores()
    {
        var trials = new List<TeamTrialResult>();
        for (int i = 0; i < 12; i++)
        {
            trials.Add(Trial((1, 1001, 1, 1, [Bucket(60, 1_000)])));
        }

        PerformanceSampleThresholds custom = new(5, 2);
        PerformanceChart chart = PerformanceChartEngine.Build(trials, custom);
        PerformanceUmaRow row = Assert.Single(chart.Rows);
        Assert.Equal(12, row.TrialCount);
        Assert.Equal(PerformanceSampleWarning.None, row.SampleWarning);
        Assert.Equal(1_000, row.Average);

        chart.ApplySampleThresholds(PerformanceSampleThresholds.Default);
        Assert.Equal(PerformanceSampleWarning.Soft, row.SampleWarning);
        Assert.Equal(1_000, row.Average);

        Assert.Equal(PerformanceSampleWarning.Hard, custom.Classify(1));
        Assert.Equal(PerformanceSampleWarning.Soft, custom.Classify(2));
        Assert.Equal(PerformanceSampleWarning.Soft, custom.Classify(4));
        Assert.Equal(PerformanceSampleWarning.None, custom.Classify(5));
    }

    [Fact]
    public void Distance_splits_a_row_and_ace_versus_support_does_not()
    {
        TeamTrialResult sprintAce = Trial((1, 1001, 1, 3, [Bucket(60, 10_000, bonus: 1_000)]));
        TeamTrialResult mileAce = Trial((1, 1001, 1, 3, [Bucket(60, 30_000)]));
        mileAce.Data!.RaceResultArray[0].DistanceType = 2;

        PerformanceChart distances = PerformanceChartEngine.Build([sprintAce, mileAce]);
        Assert.Equal(2, distances.Rows.Count);
        Assert.Equal(["Mile", "Sprint"], distances.Rows.Select(row => row.Distance));
        Assert.All(distances.Rows, row => Assert.Equal("Late", row.Style));
        Assert.Equal(30_000, distances.Rows[0].Average);
        Assert.Equal(1, distances.Rows[0].TrialCount);
        Assert.True(distances.Rows[0].InCurrentLineup);
        Assert.Equal("Mile · Late · 1 race", distances.Rows[0].Subtitle);
        Assert.Equal(10_000, distances.Rows[1].Average);
        Assert.False(distances.Rows[1].InCurrentLineup);
        Assert.Equal(30_000, distances.TeamAverage);

        TeamTrialResult asAce = Trial((1, 1001, 1, 3, [Bucket(60, 10_000, bonus: 2_000)]));
        TeamTrialResult asSupport = Trial((1, 1001, 3, 3, [Bucket(60, 20_000)]));
        PerformanceChart slot = PerformanceChartEngine.Build([asAce, asSupport]);
        PerformanceUmaRow row = Assert.Single(slot.Rows);

        Assert.Equal(15_000, row.Average);
        Assert.Equal(2, row.TrialCount);
        Assert.Equal("Sprint · Late · 2 races", row.Subtitle);
        Assert.True(row.InCurrentLineup);
        Assert.Equal(15_000, slot.TeamAverage);
        Assert.DoesNotContain("Ace", row.Subtitle);
        Assert.DoesNotContain("Support", row.Subtitle);
    }

    private static bool SameRow(TeamTrialResult trial, PerformanceUmaRow row)
    {
        if (!trial.RaceRoster.TryGetValue(row.TrainedCharaId, out RaceHorseData? horse))
        {
            return false;
        }

        int rosterIndex = 0;
        foreach (int id in trial.RosterTrainedCharaIds)
        {
            if (id == row.TrainedCharaId)
            {
                break;
            }

            rosterIndex++;
        }

        return RunningStyleNames.FormatAnalysis(horse.RunningStyle) == row.Style
            && DistanceLabel(trial, horse, rosterIndex) == row.Distance;
    }

    private static string DistanceLabel(TeamTrialResult trial, RaceHorseData horse, int rosterIndex)
    {
        int distanceType = trial.FindRaceResultByUma(horse)?.DistanceType ?? (rosterIndex / 3 + 1);
        return DistanceTypeNames.FormatAnalysis(distanceType);
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
