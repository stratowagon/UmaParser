using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>
/// One Tracks grid row, already formatted the way WinForms formats the cell.
/// The last row of a non-empty grid is the total: <see cref="IsFooter"/> is true,
/// the track cell says "Total", and the rate colors are off.
/// Sort properties stay numeric so a header click orders 10 after 9.
/// </summary>
public sealed class TracksGridRow
{
    public bool IsFooter { get; init; }
    public string Track { get; init; } = "";
    public string DistanceText { get; init; } = "";
    public int Distance { get; init; }
    public int Races { get; init; }
    public string AvgPlaceText { get; init; } = "";
    public double AvgPlace { get; init; }
    public string PctFirstText { get; init; } = "";
    public double PctFirst { get; init; }
    public string PctTop3Text { get; init; } = "";
    public double PctTop3 { get; init; }
    public string PctTop5Text { get; init; } = "";
    public double PctTop5 { get; init; }
    public string SpurtRateText { get; init; } = "";
    public double SpurtRate { get; init; }
    public CellBand SpurtBand { get; init; }
    public string SurvivalText { get; init; } = "";
    public double Survival { get; init; }
    public CellBand SurvivalBand { get; init; }
    public string AvgSkillPointsText { get; init; } = "";
    public double AvgSkillPoints { get; init; }

    public static TracksGridRow FromData(TracksRow row) => new()
    {
        Track = row.Track,
        DistanceText = row.Distance.ToString(),
        Distance = row.Distance,
        Races = row.Races,
        AvgPlaceText = TrackCellText.AveragePlace(row.AvgPlace),
        AvgPlace = row.AvgPlace,
        PctFirstText = TrackCellText.Percent(row.PctFirst),
        PctFirst = row.PctFirst,
        PctTop3Text = TrackCellText.Percent(row.PctTop3),
        PctTop3 = row.PctTop3,
        PctTop5Text = TrackCellText.Percent(row.PctTop5),
        PctTop5 = row.PctTop5,
        SpurtRateText = TrackCellText.Percent(row.SpurtRate),
        SpurtRate = row.SpurtRate,
        SpurtBand = CellBands.ForTrackRate(row.SpurtRate),
        SurvivalText = TrackCellText.Percent(row.Survival),
        Survival = row.Survival,
        SurvivalBand = CellBands.ForTrackRate(row.Survival),
        AvgSkillPointsText = TrackCellText.SkillPoints(row.AvgSkillPointsPerRace),
        AvgSkillPoints = row.AvgSkillPointsPerRace,
    };

    public static TracksGridRow FromFooter(TracksTotals totals, bool includeSkillPoints)
    {
        double? skillPoints = TracksFooterValues.Get(GridColumnIds.Tracks.AvgSkillPts, totals, includeSkillPoints) as double?;
        return new TracksGridRow
        {
            IsFooter = true,
            Track = TrackCellText.TotalLabel,
            DistanceText = "",
            Races = totals.Races,
            AvgPlaceText = TrackCellText.AveragePlace(totals.AvgPlace),
            AvgPlace = totals.AvgPlace,
            PctFirstText = TrackCellText.Percent(totals.PctFirst),
            PctFirst = totals.PctFirst,
            PctTop3Text = TrackCellText.Percent(totals.PctTop3),
            PctTop3 = totals.PctTop3,
            PctTop5Text = TrackCellText.Percent(totals.PctTop5),
            PctTop5 = totals.PctTop5,
            SpurtRateText = TrackCellText.Percent(totals.SpurtRate),
            SpurtRate = totals.SpurtRate,
            SpurtBand = CellBand.None,
            SurvivalText = TrackCellText.Percent(totals.Survival),
            Survival = totals.Survival,
            SurvivalBand = CellBand.None,
            AvgSkillPointsText = skillPoints.HasValue ? TrackCellText.SkillPoints(skillPoints.Value) : "",
            AvgSkillPoints = skillPoints ?? 0,
        };
    }
}

/// <summary>Data rows plus the total row the Tracks grid shows under them.</summary>
public static class TracksGridRows
{
    public static IReadOnlyList<TracksGridRow> Build(IReadOnlyList<TracksRow> rows, bool includeSkillPoints)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<TracksGridRow>();
        }

        var gridRows = new List<TracksGridRow>(rows.Count + 1);
        foreach (TracksRow row in rows)
        {
            gridRows.Add(TracksGridRow.FromData(row));
        }

        gridRows.Add(TracksGridRow.FromFooter(TracksTotals.Compute(rows, includeSkillPoints), includeSkillPoints));
        return gridRows;
    }
}

/// <summary>
/// Header-click order for the Tracks grid.
/// A total row stays at the bottom in both directions. WinForms gets there by
/// moving the footer after the sort. This comparison puts it there directly.
/// Data rows compare only the chosen column. The total row is not part of that comparison.
/// </summary>
public static class TracksGridSort
{
    public static int Compare(TracksGridRow left, TracksGridRow right, string sortMember, bool descending)
    {
        if (left.IsFooter || right.IsFooter)
        {
            if (left.IsFooter && right.IsFooter)
            {
                return 0;
            }

            return left.IsFooter ? 1 : -1;
        }

        int result = sortMember switch
        {
            nameof(TracksGridRow.Track) => string.Compare(left.Track, right.Track, StringComparison.CurrentCulture),
            nameof(TracksGridRow.Distance) => left.Distance.CompareTo(right.Distance),
            nameof(TracksGridRow.Races) => left.Races.CompareTo(right.Races),
            nameof(TracksGridRow.AvgPlace) => left.AvgPlace.CompareTo(right.AvgPlace),
            nameof(TracksGridRow.PctFirst) => left.PctFirst.CompareTo(right.PctFirst),
            nameof(TracksGridRow.PctTop3) => left.PctTop3.CompareTo(right.PctTop3),
            nameof(TracksGridRow.PctTop5) => left.PctTop5.CompareTo(right.PctTop5),
            nameof(TracksGridRow.SpurtRate) => left.SpurtRate.CompareTo(right.SpurtRate),
            nameof(TracksGridRow.Survival) => left.Survival.CompareTo(right.Survival),
            nameof(TracksGridRow.AvgSkillPoints) => left.AvgSkillPoints.CompareTo(right.AvgSkillPoints),
            _ => 0,
        };

        return descending ? -result : result;
    }
}
