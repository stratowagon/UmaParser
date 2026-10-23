using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class TracksTotalsTests
{
    [Fact]
    public void Footer_is_the_race_weighted_average_of_the_track_rows()
    {
        var trials = TrialSets.Load("TT-K0");
        var layout = trials[^1];
        TracksReport report = TracksAnalyzer.Analyze(trials, layout.RosterTrainedCharaIds[0], layout.RosterNames[0]);
        Assert.NotEmpty(report.Rows);

        TracksTotals totals = TracksTotals.Compute(report.Rows, includeSkillPoints: true);
        int races = report.Rows.Sum(row => row.Races);
        Assert.Equal(races, totals.Races);
        Assert.True(races > 0);
        Assert.Equal(Weighted(report.Rows, row => row.AvgPlace, races), totals.AvgPlace);
        Assert.Equal(Weighted(report.Rows, row => row.PctFirst, races), totals.PctFirst);
        Assert.Equal(Weighted(report.Rows, row => row.PctTop3, races), totals.PctTop3);
        Assert.Equal(Weighted(report.Rows, row => row.PctTop5, races), totals.PctTop5);
        Assert.Equal(Weighted(report.Rows, row => row.SpurtRate, races), totals.SpurtRate);
        Assert.Equal(Weighted(report.Rows, row => row.Survival, races), totals.Survival);
        Assert.Equal(Weighted(report.Rows, row => row.AvgSkillPointsPerRace, races), totals.AvgSkillPointsPerRace);

        TracksTotals withoutSkills = TracksTotals.Compute(report.Rows, includeSkillPoints: false);
        Assert.Equal(0, withoutSkills.AvgSkillPointsPerRace);
        Assert.Equal(totals.AvgPlace, withoutSkills.AvgPlace);
        Assert.Equal(totals.Races, withoutSkills.Races);
    }

    [Fact]
    public void No_races_returns_the_empty_total()
    {
        TracksTotals empty = TracksTotals.Compute(Array.Empty<TracksRow>(), includeSkillPoints: true);
        Assert.Equal(0, empty.Races);
        Assert.Equal(0, empty.AvgPlace);
        Assert.Equal(0, empty.AvgSkillPointsPerRace);

        var zeros = new[]
        {
            new TracksRow { Track = "Nowhere", Races = 0, AvgPlace = 4 },
        };
        Assert.Equal(0, TracksTotals.Compute(zeros, includeSkillPoints: true).AvgPlace);
    }

    static double Weighted(IReadOnlyList<TracksRow> rows, Func<TracksRow, double> selector, int races) =>
        rows.Sum(row => selector(row) * row.Races) / races;
}
