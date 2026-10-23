using UmaParser.Analysis;
using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class TracksGridSortTests
{
    [Fact]
    public void Cell_text_matches_the_winforms_format_strings()
    {
        Assert.Equal("100%", TrackCellText.Percent(100));
        Assert.Equal("33.3%", TrackCellText.Percent(33.34));
        Assert.Equal("0%", TrackCellText.Percent(0));
        Assert.Equal("1.20", TrackCellText.AveragePlace(1.2));
        Assert.Equal("3.00", TrackCellText.AveragePlace(3));
        Assert.Equal("10", TrackCellText.SkillPoints(10.4));
        Assert.Equal("18", TrackCellText.SkillPoints(17.5));
        Assert.Equal("Alice: No appearances found.", TrackCellText.NoAppearances("Alice"));
        Assert.Equal("Alice: tracks analysis not available.", TrackCellText.Unavailable("Alice"));

        var report = new TracksReport
        {
            UmaName = "Oguri",
            TotalRaces = 3,
            TeamType = "Mile",
            Rows = [new TracksRow(), new TracksRow()],
        };
        Assert.Equal("Oguri: 2 tracks, 3 race(s) total. Team type: Mile. ", TrackCellText.Report(report));
    }

    [Fact]
    public void Footer_values_match_the_winforms_total_row()
    {
        TracksRow[] rows =
        [
            Row("Tokyo", races: 1, place: 2, first: 0, top3: 100, top5: 100, spurt: 94, survival: 100, skill: 10),
            Row("Kyoto", races: 3, place: 4, first: 33.3, top3: 66.7, top5: 100, spurt: 100, survival: 96.5, skill: 20),
        ];
        TracksTotals totals = TracksTotals.Compute(rows, includeSkillPoints: true);

        Assert.Equal(TrackCellText.TotalLabel, TracksFooterValues.Get(GridColumnIds.Tracks.Track, totals, true));
        Assert.Null(TracksFooterValues.Get(GridColumnIds.Tracks.Distance, totals, true));
        Assert.Equal(4, Assert.IsType<int>(TracksFooterValues.Get(GridColumnIds.Tracks.Races, totals, true)));
        Assert.Equal(totals.AvgPlace, Assert.IsType<double>(TracksFooterValues.Get(GridColumnIds.Tracks.AvgPlace, totals, true)));
        Assert.Equal(totals.PctFirst, Assert.IsType<double>(TracksFooterValues.Get(GridColumnIds.Tracks.PctFirst, totals, true)));
        Assert.Equal(totals.PctTop3, Assert.IsType<double>(TracksFooterValues.Get(GridColumnIds.Tracks.PctTop3, totals, true)));
        Assert.Equal(totals.PctTop5, Assert.IsType<double>(TracksFooterValues.Get(GridColumnIds.Tracks.PctTop5, totals, true)));
        Assert.Equal(totals.SpurtRate, Assert.IsType<double>(TracksFooterValues.Get(GridColumnIds.Tracks.SpurtRate, totals, true)));
        Assert.Equal(totals.Survival, Assert.IsType<double>(TracksFooterValues.Get(GridColumnIds.Tracks.Survival, totals, true)));
        Assert.Equal(totals.AvgSkillPointsPerRace, Assert.IsType<double>(TracksFooterValues.Get(GridColumnIds.Tracks.AvgSkillPts, totals, true)));
        Assert.Null(TracksFooterValues.Get(GridColumnIds.Tracks.AvgSkillPts, totals, false));
        Assert.Null(TracksFooterValues.Get("tracks.unknown", totals, true));

        IReadOnlyList<TracksGridRow> grid = TracksGridRows.Build(rows, includeSkillPoints: true);
        Assert.Equal(3, grid.Count);
        Assert.Equal("Tokyo", grid[0].Track);
        Assert.Equal("1600", grid[0].DistanceText);
        Assert.Equal(CellBand.Critical, grid[0].SpurtBand);
        Assert.Equal(CellBand.None, grid[0].SurvivalBand);
        Assert.Equal(CellBand.NeedsWork, grid[1].SurvivalBand);
        Assert.Equal("94%", grid[0].SpurtRateText);
        Assert.Equal("2.00", grid[0].AvgPlaceText);

        TracksGridRow footer = grid[^1];
        Assert.True(footer.IsFooter);
        Assert.Equal("", footer.DistanceText);
        Assert.Equal(TrackCellText.AveragePlace(totals.AvgPlace), footer.AvgPlaceText);
        Assert.Equal(TrackCellText.Percent(totals.PctFirst), footer.PctFirstText);
        Assert.Equal(TrackCellText.Percent(totals.SpurtRate), footer.SpurtRateText);
        Assert.Equal(TrackCellText.SkillPoints(totals.AvgSkillPointsPerRace), footer.AvgSkillPointsText);
        Assert.Equal(CellBand.None, footer.SpurtBand);
        Assert.Equal(CellBand.None, footer.SurvivalBand);

        TracksGridRow outsideTeamTrials = TracksGridRows.Build(rows, includeSkillPoints: false)[^1];
        Assert.Equal("", outsideTeamTrials.AvgSkillPointsText);
        Assert.Equal(footer.AvgPlaceText, outsideTeamTrials.AvgPlaceText);
        Assert.Empty(TracksGridRows.Build(Array.Empty<TracksRow>(), includeSkillPoints: true));
    }

    [Fact]
    public void The_total_row_stays_last_when_the_sort_direction_flips()
    {
        TracksGridRow tokyo = TracksGridRow.FromData(Row("Tokyo", races: 2, place: 5, first: 0, top3: 0, top5: 50, spurt: 100, survival: 100, skill: 1));
        TracksGridRow kyoto = TracksGridRow.FromData(Row("Kyoto", races: 4, place: 1, first: 100, top3: 100, top5: 100, spurt: 80, survival: 90, skill: 9));
        TracksGridRow footer = TracksGridRow.FromFooter(TracksTotals.Compute(
            [Row("Tokyo", 2, 5, 0, 0, 50, 100, 100, 1), Row("Kyoto", 4, 1, 100, 100, 100, 80, 90, 9)],
            includeSkillPoints: true), includeSkillPoints: true);

        Assert.True(TracksGridSort.Compare(kyoto, tokyo, nameof(TracksGridRow.Track), descending: false) < 0);
        Assert.True(TracksGridSort.Compare(tokyo, kyoto, nameof(TracksGridRow.Track), descending: true) < 0);
        Assert.True(TracksGridSort.Compare(kyoto, tokyo, nameof(TracksGridRow.AvgPlace), descending: false) < 0);
        Assert.True(TracksGridSort.Compare(kyoto, tokyo, nameof(TracksGridRow.Races), descending: true) < 0);

        Assert.True(TracksGridSort.Compare(footer, tokyo, nameof(TracksGridRow.AvgPlace), descending: false) > 0);
        Assert.True(TracksGridSort.Compare(footer, kyoto, nameof(TracksGridRow.AvgPlace), descending: true) > 0);
        Assert.True(TracksGridSort.Compare(tokyo, footer, nameof(TracksGridRow.Track), descending: true) < 0);
        Assert.Equal(0, TracksGridSort.Compare(footer, footer, nameof(TracksGridRow.Races), descending: true));
        Assert.Equal(0, TracksGridSort.Compare(tokyo, kyoto, "not-a-column", descending: false));
    }

    private static TracksRow Row(
        string track,
        int races,
        double place,
        double first,
        double top3,
        double top5,
        double spurt,
        double survival,
        double skill) => new()
    {
        Track = track,
        Distance = track == "Tokyo" ? 1600 : 2000,
        Races = races,
        AvgPlace = place,
        PctFirst = first,
        PctTop3 = top3,
        PctTop5 = top5,
        SpurtRate = spurt,
        Survival = survival,
        AvgSkillPointsPerRace = skill,
    };
}
