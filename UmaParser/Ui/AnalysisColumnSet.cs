using UmaParser.Analysis;
using UmaParser.Settings;

namespace UmaParser.Ui;

public static class AnalysisColumnSet
{
    public static IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> All { get; } = Build();

    private static IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> Build()
    {
        return
        [
            Col(GridColumnIds.Analysis.Uma, "Uma",
                "Uma name in roster order.",
                "The identity column for each roster row. Always in team order (Sprint -> Dirt). Ace umas are shown in bold. This column is always first.",
                typeof(string), true, r => r.Name),
            Col(GridColumnIds.Analysis.Style, "Style",
                "Running style used.",
                "Running style for this uma. Amber shading means two teammates share a style; red means all three share a style.",
                typeof(string), false, r => r.RunningStyleLabel),
            Col(GridColumnIds.Analysis.Avg, "Avg",
                "Mean base score per trial (bonuses removed).",
                "Mean base score per trial with all score bonuses removed. This is the primary consistency metric for Team Trials analysis.",
                typeof(double), false, r => r.Average),
            Col(GridColumnIds.Analysis.Skills, "Skills",
                "Mean skill-activation base points per trial.",
                "Mean skill-activation base points per trial (raw_score 26–57, bonuses removed).",
                typeof(double), false, r => r.SkillPointsAverage),
            Col(GridColumnIds.Analysis.SkillCv, "Skill CV",
                "Coefficient of variation of skill base points across trials (stdev ÷ mean).",
                "Coefficient of variation of skill base points across trials (stdev ÷ mean), shown as percent. Lower is more consistent.",
                typeof(string), false, r => FormatCv(r.SkillPointsCoefficientOfVariation)),
            Col(GridColumnIds.Analysis.SkillRatio, "Skill Ratio",
                "Share of mean base score from skill activations (Skills ÷ Avg), shown as percent.",
                "Percentage of mean base score attributed to skill activations. Computed as Skills ÷ Avg base score.",
                typeof(string), false, FormatSkillRatio),
            Col(GridColumnIds.Analysis.Other, "Other",
                "Mean non-skill base points per trial.",
                "Mean non-skill base points per trial (positioning, placement, target time, strong start, etc.; bonuses removed).",
                typeof(double), false, r => r.OtherPointsAverage),
            Col(GridColumnIds.Analysis.OtherCv, "Other CV",
                "Coefficient of variation of non-skill base points across trials (stdev ÷ mean).",
                "Coefficient of variation of non-skill base points across trials (stdev ÷ mean), shown as percent. Lower is more consistent.",
                typeof(string), false, r => FormatCv(r.OtherPointsCoefficientOfVariation)),
            Col(GridColumnIds.Analysis.AvgPlace, "Avg Place",
                "Average finish position across all races.",
                "Average finish position across every race this uma ran in the loaded trials.",
                typeof(double), false, r => r.AveragePlace),
            Col(GridColumnIds.Analysis.Trimmed, "Trimmed",
                "Average after dropping one outlier low trial.",
                "Average after dropping one outlier low trial when the trim rule applies (lowest score below 80% of roster mean and at least 5 trials).",
                typeof(double), false, r => r.TrimmedAverage),
            Col(GridColumnIds.Analysis.Cv, "CV",
                "Coefficient of variation for base score (stdev ÷ mean).",
                "Coefficient of variation for base scores (stdev ÷ mean). Lower values indicate more consistent scoring.",
                typeof(string), false, r => FormatCv(r.CoefficientOfVariation)),
            Col(GridColumnIds.Analysis.AdjCv, "Adj CV",
                "Coefficient of variation for Trimmed score.",
                "Coefficient of variation for Trimmed score (requires at least 5 trials), shown as percent.",
                typeof(string), false, r => FormatCv(r.AdjustedCoefficientOfVariation)),
            Col(GridColumnIds.Analysis.Gap, "Gap",
                "Points below the best trimmed base score on the roster.",
                "Points below the best trimmed base score on the roster. Used as part of the retrain priority score.",
                typeof(double), false, r => r.GapToTop),
            Col(GridColumnIds.Analysis.Ceiling, "Ceiling",
                "90th percentile base score across trials.",
                "90th percentile base score across trials.",
                typeof(double), false, r => r.Ceiling),
            Col(GridColumnIds.Analysis.Floor, "Floor",
                "10th percentile base score across trials.",
                "10th percentile base score across trials.",
                typeof(double), false, r => r.Floor),
            Col(GridColumnIds.Analysis.Spread, "Spread",
                "Ceiling minus floor.",
                "Ceiling minus floor — the scoring range across trials.",
                typeof(double), false, r => r.CeilingFloorSpread),
            Col(GridColumnIds.Analysis.Retrain, "Retrain",
                "Combined weakness score (85% gap, 15% adjusted CV).",
                "Combined weakness score (85% gap, 15% adjusted CV). Higher values indicate stronger rebuild candidates. Cells are shaded by priority level.",
                typeof(string), false, r => r.RetrainPriorityLabel),
            Col(GridColumnIds.Analysis.AceDelta, "Ace delta",
                "Trimmed base score difference vs this team's current ace (+ = better than ace).",
                "Trimmed base score difference vs this team's current ace (+ = better than ace). Colored when positive and large enough to suggest an ace swap. Requires a decent sample size to be reliable.",
                typeof(string), false, r => r.AceDeltaLabel),
        ];
    }

    private static GridColumnDefinition<UmaAnalysisRow> Col(
        string id,
        string header,
        string tooltip,
        string description,
        Type valueType,
        bool isLocked,
        Func<UmaAnalysisRow, object?> getValue) =>
        new()
        {
            Id = id,
            Header = header,
            Tooltip = tooltip,
            Description = description,
            ValueType = valueType,
            IsLocked = isLocked,
            GetValue = getValue,
        };

    private static string FormatCv(double ratio) => $"{ratio * 100:F2}%";

    private static string FormatSkillRatio(UmaAnalysisRow row)
    {
        if (row.Average <= 0)
        {
            return "—";
        }

        double ratio = row.SkillPointsAverage / row.Average * 100;
        return $"{ratio:F1}%";
    }
}