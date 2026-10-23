using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>
/// One Team Analysis row, already formatted the way WinForms formats the cell.
/// Sort is off in WinForms, so these are display strings in roster order.
/// Ace rows are the first uma of each team. A team line sits above rows 3, 6, 9, and 12.
/// </summary>
public sealed class AnalysisGridRow
{
    public bool IsAce { get; init; }
    public bool IsTeamBoundary { get; init; }
    public string Uma { get; init; } = "";
    public string Style { get; init; } = "";
    public string Avg { get; init; } = "";
    public string Skills { get; init; } = "";
    public string SkillCv { get; init; } = "";
    public string SkillRatio { get; init; } = "";
    public string Other { get; init; } = "";
    public string OtherCv { get; init; } = "";
    public string AvgPlace { get; init; } = "";
    public string Trimmed { get; init; } = "";
    public string Cv { get; init; } = "";
    public string AdjCv { get; init; } = "";
    public string Gap { get; init; } = "";
    public string Ceiling { get; init; } = "";
    public string Floor { get; init; } = "";
    public string Spread { get; init; } = "";
    public string Retrain { get; init; } = "";
    public string AceDelta { get; init; } = "";
    public CellBand StyleBand { get; init; }
    public CellBand RetrainBand { get; init; }
    public CellBand AceDeltaBand { get; init; }

    public static AnalysisGridRow From(UmaAnalysisReport report, UmaAnalysisRow row, int rowIndex)
    {
        return new AnalysisGridRow
        {
            IsAce = RosterGridLayout.IsAceRow(rowIndex),
            IsTeamBoundary = RosterGridLayout.IsDefaultTeamBoundary(rowIndex),
            Uma = Text(GridColumnIds.Analysis.Uma, row),
            Style = Text(GridColumnIds.Analysis.Style, row),
            Avg = Text(GridColumnIds.Analysis.Avg, row),
            Skills = Text(GridColumnIds.Analysis.Skills, row),
            SkillCv = Text(GridColumnIds.Analysis.SkillCv, row),
            SkillRatio = Text(GridColumnIds.Analysis.SkillRatio, row),
            Other = Text(GridColumnIds.Analysis.Other, row),
            OtherCv = Text(GridColumnIds.Analysis.OtherCv, row),
            AvgPlace = Text(GridColumnIds.Analysis.AvgPlace, row),
            Trimmed = Text(GridColumnIds.Analysis.Trimmed, row),
            Cv = Text(GridColumnIds.Analysis.Cv, row),
            AdjCv = Text(GridColumnIds.Analysis.AdjCv, row),
            Gap = Text(GridColumnIds.Analysis.Gap, row),
            Ceiling = Text(GridColumnIds.Analysis.Ceiling, row),
            Floor = Text(GridColumnIds.Analysis.Floor, row),
            Spread = Text(GridColumnIds.Analysis.Spread, row),
            Retrain = Text(GridColumnIds.Analysis.Retrain, row),
            AceDelta = Text(GridColumnIds.Analysis.AceDelta, row),
            StyleBand = CellBands.ForStyleOverlap(row),
            RetrainBand = CellBands.ForRetrain(row),
            AceDeltaBand = CellBands.ForAceDelta(report, row),
        };
    }

    private static string Text(string columnId, UmaAnalysisRow row)
    {
        foreach (GridColumnDefinition<UmaAnalysisRow> column in AnalysisColumnSet.All)
        {
            if (column.Id == columnId)
            {
                return AnalysisCellText.Display(column, row);
            }
        }

        return "";
    }
}
