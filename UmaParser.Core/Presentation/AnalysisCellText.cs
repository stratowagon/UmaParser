using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>
/// Display text for the Team Analysis grid and the status summary.
/// WinForms cell formatting and the WPF grid both call this.
/// </summary>
public static class AnalysisCellText
{
    /// <summary>
    /// Shown on the Team Analysis tab when the drop is not one uniform Team Trials roster.
    /// </summary>
    public const string Unavailable =
        "Team Analysis is only active for Team Trials files that all have the exact same roster of 15 umas.";

    /// <summary>
    /// Status text after a uniform roster loads. WinForms replaces the "files loaded" line with this.
    /// </summary>
    public static string Summary(UmaAnalysisReport report)
    {
        var lines = new List<string>
        {
            $"Analysis uses base scores (all bonuses removed) across {report.TrialCount} trial(s). Team avg (raw): {report.TeamAverage:N0}.",
        };

        foreach (TeamAceRecommendation ace in report.AceRecommendations)
        {
            if (!string.IsNullOrEmpty(ace.Note))
            {
                lines.Add($"{ace.Distance}: {ace.Note}");
            }
        }

        foreach (TeamStyleOverlapRecommendation overlap in report.StyleOverlapRecommendations)
        {
            if (!string.IsNullOrEmpty(overlap.Note))
            {
                lines.Add($"{overlap.Distance}: {overlap.Note}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Cell text for one analysis column. Avg Place is <c>0.00</c>. Other numbers use the
    /// general format WinForms uses when the column has no format string. CV, skill ratio,
    /// retrain, and ace delta are already strings on the column definition.
    /// </summary>
    public static string Display(GridColumnDefinition<UmaAnalysisRow> column, UmaAnalysisRow row)
    {
        object? value = column.GetValue(row);
        if (column.Id == GridColumnIds.Analysis.AvgPlace && value is double place)
        {
            return place.ToString("0.00");
        }

        return value switch
        {
            null => "",
            string text => text,
            double number => number.ToString(),
            _ => value.ToString() ?? "",
        };
    }
}
