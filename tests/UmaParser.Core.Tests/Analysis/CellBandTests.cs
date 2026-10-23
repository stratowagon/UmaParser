using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.MasterData;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class CellBandTests
{
    [Fact]
    public void Analysis_bands_follow_retrain_overlap_and_ace_delta()
    {
        Assert.Equal(CellBand.Critical, CellBands.ForRetrain(AnalysisRow(RetrainPriorityLevel.Critical)));
        Assert.Equal(CellBand.NeedsWork, CellBands.ForRetrain(AnalysisRow(RetrainPriorityLevel.NeedsWork)));
        Assert.Equal(CellBand.Stable, CellBands.ForRetrain(AnalysisRow(RetrainPriorityLevel.Stable)));

        Assert.Equal(CellBand.Critical, CellBands.ForStyleOverlap(AnalysisRow(overlap: TeamStyleOverlap.FullTeam)));
        Assert.Equal(CellBand.NeedsWork, CellBands.ForStyleOverlap(AnalysisRow(overlap: TeamStyleOverlap.Pair)));
        Assert.Equal(CellBand.None, CellBands.ForStyleOverlap(AnalysisRow()));

        UmaAnalysisReport noMatch = Report();
        Assert.Equal(CellBand.None, CellBands.ForAceDelta(noMatch, AnalysisRow(aceDelta: 0, suggested: true)));
        Assert.Equal(CellBand.None, CellBands.ForAceDelta(noMatch, AnalysisRow(aceDelta: 10, suggested: false)));
        Assert.Equal(CellBand.NeedsWork, CellBands.ForAceDelta(noMatch, AnalysisRow(aceDelta: 0.1, suggested: true)));

        UmaAnalysisReport weak = Report(Ace("Sprint", "Uma", weak: true));
        Assert.Equal(CellBand.NeedsWork, CellBands.ForAceDelta(weak, AnalysisRow(aceDelta: 12, suggested: true)));

        UmaAnalysisReport strong = Report(Ace("Mile", "Other", weak: false), Ace("Sprint", "Uma", weak: false));
        Assert.Equal(CellBand.Critical, CellBands.ForAceDelta(strong, AnalysisRow(aceDelta: 12, suggested: true)));
        Assert.Equal(CellBand.NeedsWork, CellBands.ForAceDelta(strong, AnalysisRow(name: "Uma", distance: "Long", aceDelta: 12, suggested: true)));

        UmaAnalysisReport fromFixture = UmaAnalysisEngine.Analyze(TrialSets.Load("TT-K0"));
        Assert.Contains(fromFixture.Rows, row => row.PriorityLevel == RetrainPriorityLevel.Stable);
        foreach (UmaAnalysisRow row in fromFixture.Rows)
        {
            CellBand retrain = row.PriorityLevel switch
            {
                RetrainPriorityLevel.Critical => CellBand.Critical,
                RetrainPriorityLevel.NeedsWork => CellBand.NeedsWork,
                _ => CellBand.Stable,
            };
            Assert.Equal(retrain, CellBands.ForRetrain(row));

            CellBand overlap = row.StyleOverlap switch
            {
                TeamStyleOverlap.FullTeam => CellBand.Critical,
                TeamStyleOverlap.Pair => CellBand.NeedsWork,
                _ => CellBand.None,
            };
            Assert.Equal(overlap, CellBands.ForStyleOverlap(row));

            CellBand ace = CellBand.None;
            if (row.AceDelta > 0 && row.IsSuggestedAce)
            {
                TeamAceRecommendation? recommendation = fromFixture.AceRecommendations
                    .FirstOrDefault(candidate => candidate.Distance == row.Distance && candidate.BestSupport == row.Name);
                ace = recommendation != null && !recommendation.IsWeakSuggestion
                    ? CellBand.Critical
                    : CellBand.NeedsWork;
            }

            Assert.Equal(ace, CellBands.ForAceDelta(fromFixture, row));
        }
    }

    [Theory]
    [InlineData(0, 0, CellBand.None)]
    [InlineData(1200, 0, CellBand.None)]
    [InlineData(1149, 1, CellBand.None)]
    [InlineData(1150, 1, CellBand.Gold)]
    [InlineData(1200, 1, CellBand.Gold)]
    [InlineData(1250, 1, CellBand.Gold)]
    [InlineData(1251, 1, CellBand.Unique)]
    [InlineData(2300, 2, CellBand.Gold)]
    [InlineData(449, 1, CellBand.Unique)]
    [InlineData(450, 1, CellBand.None)]
    [InlineData(1, 1, CellBand.Unique)]
    public void Skill_name_band_uses_points_per_activation(double points, int activations, CellBand expected)
    {
        var row = new SkillActivationRow
        {
            TotalPointsEarned = points,
            ActivationCount = activations,
        };
        Assert.Equal(expected, CellBands.ForSkillName(row));
    }

    [Fact]
    public void Skill_metric_band_flags_wit_underperformance_then_non_wit_low_activation()
    {
        Assert.Equal(CellBand.NeedsWork, CellBands.ForSkillMetric(Skill(under: true, low: false, SkillActivateLotKind.Wit)));
        Assert.Equal(CellBand.NeedsWork, CellBands.ForSkillMetric(Skill(under: false, low: true, SkillActivateLotKind.Unconditional)));
        Assert.Equal(CellBand.NeedsWork, CellBands.ForSkillMetric(Skill(under: true, low: true, SkillActivateLotKind.Unconditional)));
        Assert.Equal(CellBand.None, CellBands.ForSkillMetric(Skill(under: false, low: true, SkillActivateLotKind.Wit)));
        Assert.Equal(CellBand.None, CellBands.ForSkillMetric(Skill(under: false, low: false, SkillActivateLotKind.Unknown)));

        var trials = TrialSets.Load("TT-K0");
        var layout = trials[^1];
        SkillActivationReport report = SkillActivationAnalyzer.Analyze(
            trials,
            layout.RosterTrainedCharaIds[0],
            layout.RosterNames[0]);
        Assert.NotEmpty(report.Rows);
        foreach (SkillActivationRow row in report.Rows)
        {
            double average = row.ActivationCount > 0 ? row.TotalPointsEarned / row.ActivationCount : 0;
            CellBand name = CellBand.None;
            if (average >= 1150 && average <= 1250)
            {
                name = CellBand.Gold;
            }
            else if (average > 1250 || (average > 0 && average < 450))
            {
                name = CellBand.Unique;
            }

            Assert.Equal(name, CellBands.ForSkillName(row));

            CellBand metric = CellBand.None;
            if (row.IsUnderperformingVsWit || (row.IsLowActivation && row.ActivateLot != SkillActivateLotKind.Wit))
            {
                metric = CellBand.NeedsWork;
            }

            Assert.Equal(metric, CellBands.ForSkillMetric(row));
        }
    }

    [Theory]
    [InlineData(94.9, CellBand.Critical)]
    [InlineData(95, CellBand.NeedsWork)]
    [InlineData(99.9, CellBand.NeedsWork)]
    [InlineData(100, CellBand.None)]
    [InlineData(100.1, CellBand.None)]
    public void Track_rate_band_is_shared_by_spurt_and_survival(double percent, CellBand expected)
    {
        Assert.Equal(95, CellBands.TrackRateCriticalBelow);
        Assert.Equal(100, CellBands.TrackRateNeedsWorkBelow);
        Assert.Equal(expected, CellBands.ForTrackRate(percent));
    }

    [Fact]
    public void Real_track_rows_use_the_same_rate_thresholds()
    {
        var trials = TrialSets.Load("TT-K0");
        var layout = trials[^1];
        TracksReport report = TracksAnalyzer.Analyze(trials, layout.RosterTrainedCharaIds[0], layout.RosterNames[0]);
        Assert.NotEmpty(report.Rows);
        foreach (TracksRow row in report.Rows)
        {
            Assert.Equal(ExpectedRate(row.SpurtRate), CellBands.ForTrackRate(row.SpurtRate));
            Assert.Equal(ExpectedRate(row.Survival), CellBands.ForTrackRate(row.Survival));
        }
    }

    static CellBand ExpectedRate(double percent)
    {
        if (percent < 95)
        {
            return CellBand.Critical;
        }

        if (percent < 100)
        {
            return CellBand.NeedsWork;
        }

        return CellBand.None;
    }

    static SkillActivationRow Skill(bool under, bool low, SkillActivateLotKind lot) => new()
    {
        IsUnderperformingVsWit = under,
        IsLowActivation = low,
        ActivateLot = lot,
    };

    static UmaAnalysisRow AnalysisRow(
        RetrainPriorityLevel priority = RetrainPriorityLevel.Stable,
        TeamStyleOverlap overlap = TeamStyleOverlap.None,
        double aceDelta = 0,
        bool suggested = false,
        string name = "Uma",
        string distance = "Sprint") => new()
    {
        Name = name,
        RunningStyleLabel = "Front",
        StyleOverlap = overlap,
        Distance = distance,
        Role = "Support",
        PriorityLevel = priority,
        RetrainPriorityLabel = priority.ToString(),
        AceDelta = aceDelta,
        IsSuggestedAce = suggested,
    };

    static UmaAnalysisReport Report(params TeamAceRecommendation[] aces) => new()
    {
        Rows = Array.Empty<UmaAnalysisRow>(),
        AceRecommendations = aces,
        StyleOverlapRecommendations = Array.Empty<TeamStyleOverlapRecommendation>(),
        TeamAverage = 0,
        TrialCount = 1,
    };

    static TeamAceRecommendation Ace(string distance, string support, bool weak) => new()
    {
        Distance = distance,
        CurrentAce = "Ace",
        BestSupport = support,
        IsWeakSuggestion = weak,
        Note = string.Empty,
    };
}
