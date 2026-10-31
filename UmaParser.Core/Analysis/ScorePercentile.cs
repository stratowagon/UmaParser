namespace UmaParser.Analysis;

/// <summary>
/// Linear percentile used by Team Analysis for ceiling (P90) and floor (P10):
/// rank = p × (n − 1), then interpolate between the surrounding sorted values.
/// </summary>
internal static class ScorePercentile
{
    public static double Linear(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        double[] sorted = values.OrderBy(value => value).ToArray();
        double rank = p * (sorted.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi)
        {
            return sorted[lo];
        }

        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }
}
