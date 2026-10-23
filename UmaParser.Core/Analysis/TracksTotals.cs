namespace UmaParser.Analysis;

/// <summary>
/// Race-weighted footer for the Tracks grid. Each track contributes in proportion to its race count.
/// </summary>
public readonly struct TracksTotals
{
    public static TracksTotals Empty => new(0, 0, 0, 0, 0, 0, 0, 0);

    public TracksTotals(
        int races,
        double avgPlace,
        double pctFirst,
        double pctTop3,
        double pctTop5,
        double spurtRate,
        double survival,
        double avgSkillPointsPerRace)
    {
        Races = races;
        AvgPlace = avgPlace;
        PctFirst = pctFirst;
        PctTop3 = pctTop3;
        PctTop5 = pctTop5;
        SpurtRate = spurtRate;
        Survival = survival;
        AvgSkillPointsPerRace = avgSkillPointsPerRace;
    }

    public int Races { get; }
    public double AvgPlace { get; }
    public double PctFirst { get; }
    public double PctTop3 { get; }
    public double PctTop5 { get; }
    public double SpurtRate { get; }
    public double Survival { get; }
    public double AvgSkillPointsPerRace { get; }

    /// <summary>
    /// Same totals the Tracks tab footer shows.
    /// Pass <paramref name="includeSkillPoints"/> false outside Team Trials; the average is then 0.
    /// </summary>
    public static TracksTotals Compute(IReadOnlyList<TracksRow> rows, bool includeSkillPoints)
    {
        int totalRaces = rows.Sum(row => row.Races);
        if (totalRaces <= 0)
        {
            return Empty;
        }

        double Weighted(Func<TracksRow, double> selector) =>
            rows.Sum(row => selector(row) * row.Races) / totalRaces;

        return new TracksTotals(
            totalRaces,
            Weighted(row => row.AvgPlace),
            Weighted(row => row.PctFirst),
            Weighted(row => row.PctTop3),
            Weighted(row => row.PctTop5),
            Weighted(row => row.SpurtRate),
            Weighted(row => row.Survival),
            includeSkillPoints ? Weighted(row => row.AvgSkillPointsPerRace) : 0);
    }
}
