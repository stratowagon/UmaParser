using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Settings;

namespace UmaParser.Presentation;

/// <summary>
/// The Team Analysis tab after a drop. A uniform roster shows the report grid.
/// A roster mismatch, a single race, or a mix of the two shows <see cref="AnalysisCellText.Unavailable"/>
/// and no grid. Column order and hidden columns come from the same preferences as WinForms.
/// The grid is not sortable: rows stay in roster order (Sprint through Dirt).
/// </summary>
public sealed class AnalysisScreen
{
    private AnalysisScreen(
        bool showGrid,
        string emptyMessage,
        string summary,
        IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> columns,
        IReadOnlyList<AnalysisGridRow> rows,
        UmaAnalysisReport? report)
    {
        ShowGrid = showGrid;
        EmptyMessage = emptyMessage;
        Summary = summary;
        Columns = columns;
        Rows = rows;
        Report = report;
    }

    public bool ShowGrid { get; }

    /// <summary>Centered message when <see cref="ShowGrid"/> is false. Empty when the grid is showing.</summary>
    public string EmptyMessage { get; }

    /// <summary>Status-bar summary. Empty unless this drop has a report.</summary>
    public string Summary { get; }

    public IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> Columns { get; }
    public IReadOnlyList<AnalysisGridRow> Rows { get; }
    public UmaAnalysisReport? Report { get; }

    /// <summary>Before any drop: an empty grid, no message. That is what WinForms shows at startup.</summary>
    public static AnalysisScreen Blank { get; } = new(
        showGrid: true,
        emptyMessage: "",
        summary: "",
        columns: Array.Empty<GridColumnDefinition<UmaAnalysisRow>>(),
        rows: Array.Empty<AnalysisGridRow>(),
        report: null);

    public static AnalysisScreen Load(TeamTrialBatch batch, GridColumnGridPreferences preferences)
    {
        if (batch.Kind != TeamTrialBatchKind.RosterStats)
        {
            return Unavailable();
        }

        var trials = new List<TeamTrialResult>(batch.Trials.Count);
        foreach (TeamTrialResult trial in batch.Trials.Values)
        {
            trials.Add(trial);
        }

        if (trials.Count == 0)
        {
            return Blank;
        }

        UmaAnalysisReport report = UmaAnalysisEngine.Analyze(trials);
        IReadOnlyList<GridColumnDefinition<UmaAnalysisRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(AnalysisColumnSet.All, preferences, GridColumnContext.Empty);
        var rows = new List<AnalysisGridRow>(report.Rows.Count);
        for (int i = 0; i < report.Rows.Count; i++)
        {
            rows.Add(AnalysisGridRow.From(report, report.Rows[i], i));
        }

        return new AnalysisScreen(
            showGrid: true,
            emptyMessage: "",
            summary: AnalysisCellText.Summary(report),
            columns: columns,
            rows: rows,
            report: report);
    }

    private static AnalysisScreen Unavailable() => new(
        showGrid: false,
        emptyMessage: AnalysisCellText.Unavailable,
        summary: "",
        columns: Array.Empty<GridColumnDefinition<UmaAnalysisRow>>(),
        rows: Array.Empty<AnalysisGridRow>(),
        report: null);
}
