using UmaParser.Analysis;
using UmaParser.ObjectModel;
using UmaParser.Presentation;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private UmaAnalysisReport? _lastAnalysisReport;

        private void PopulateAnalysis(Dictionary<string, TeamTrialResult> trialResults)
        {
            _lastAnalysisReport = UmaAnalysisEngine.Analyze(trialResults.Values.ToList());
            BindAnalysisGrid(_lastAnalysisReport);
            SetStatus(FormatAnalysisSummary(_lastAnalysisReport));
            mainTabControl.SelectedTab = tabPageAnalysis;
        }

        private void ClearAnalysis(string? message = null)
        {
            _lastAnalysisReport = null;

            if (message != null && panelAnalysisEmpty != null && labelAnalysisMessage != null)
            {
                dataGridViewAnalysis.Visible = false;
                panelAnalysisEmpty.Visible = true;
                labelAnalysisMessage.Text = message;
            }
            else
            {
                if (panelAnalysisEmpty != null) panelAnalysisEmpty.Visible = false;
                dataGridViewAnalysis.Visible = true;
                dataGridViewAnalysis.Rows.Clear();
                dataGridViewAnalysis.Columns.Clear();
            }
        }

        private void BindAnalysisGrid(UmaAnalysisReport report)
        {
            var prefs = LoadGridColumnPreferences<UmaAnalysisRow>(GridKeys.Analysis);
            GridColumnBinder.Bind(
                dataGridViewAnalysis,
                AnalysisColumnSet.All,
                prefs,
                BuildGridColumnContext(),
                report.Rows,
                DataGridViewColumnSortMode.NotSortable);

            FinalizeAnalysisGrid();
        }

        private void FinalizeAnalysisGrid()
        {
            foreach (DataGridViewColumn column in dataGridViewAnalysis.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            dataGridViewAnalysis.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            TrimAnalysisColumnWidths();

            ClearGridSelection(dataGridViewAnalysis);
        }

        private void TrimAnalysisColumnWidths()
        {
            const int padding = 8;
            foreach (DataGridViewColumn column in dataGridViewAnalysis.Columns)
            {
                int width = column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true) + padding;
                column.Width = Math.Max(width, column.MinimumWidth);
            }
        }

        private static string FormatAnalysisSummary(UmaAnalysisReport report)
        {
            var lines = new List<string>
            {
                $"Analysis uses base scores (all bonuses removed) across {report.TrialCount} trial(s). Team avg (raw): {report.TeamAverage:N0}."
            };

            foreach (var ace in report.AceRecommendations)
            {
                if (!String.IsNullOrEmpty(ace.Note))
                {
                    lines.Add($"{ace.Distance}: {ace.Note}");
                }
            }

            foreach (var overlap in report.StyleOverlapRecommendations)
            {
                if (!String.IsNullOrEmpty(overlap.Note))
                {
                    lines.Add($"{overlap.Distance}: {overlap.Note}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void DataGridViewAnalysis_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_lastAnalysisReport == null || e.RowIndex < 0 || e.ColumnIndex < 0
                || e.RowIndex >= _lastAnalysisReport.Rows.Count
                || e.CellStyle == null)
            {
                return;
            }

            var analysisRow = _lastAnalysisReport.Rows[e.RowIndex];
            string? columnId = e.ColumnIndex < dataGridViewAnalysis.Columns.Count
                ? dataGridViewAnalysis.Columns[e.ColumnIndex].Name
                : null;

            if (columnId == GridColumnIds.Analysis.Retrain)
            {
                CellBandStyling.Apply(e, CellBands.ForRetrain(analysisRow));
                return;
            }

            if (columnId == GridColumnIds.Analysis.Style)
            {
                CellBand overlapBand = CellBands.ForStyleOverlap(analysisRow);
                if (overlapBand != CellBand.None)
                {
                    CellBandStyling.Apply(e, overlapBand);
                    return;
                }
            }

            if (columnId == GridColumnIds.Analysis.AvgPlace && e.Value is double avgPlace)
            {
                e.Value = avgPlace.ToString("0.00");
                e.FormattingApplied = true;
            }

            if (columnId == GridColumnIds.Analysis.AceDelta)
            {
                CellBandStyling.Apply(e, CellBands.ForAceDelta(_lastAnalysisReport, analysisRow));
                return;
            }

            if (ShouldBoldAceName(dataGridViewAnalysis, e.RowIndex, e.ColumnIndex))
            {
                e.CellStyle.Font = _aceNameFontCache.GetBoldFont(dataGridViewAnalysis);
            }
        }
    }
}