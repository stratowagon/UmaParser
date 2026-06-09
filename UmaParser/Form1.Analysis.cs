using System.Drawing;
using UmaBlobber.Analysis;
using UmaBlobber.ObjectModel;
using UmaBlobber.Ui;

namespace UmaBlobber
{
    public partial class Form1
    {
        private static readonly Dictionary<string, string> AnalysisColumnTooltips = new(StringComparer.Ordinal)
        {
            ["Uma"] = "Uma name in roster order: five teams (Sprint → Dirt), ace first in each trio (shown bold).",
            ["Style"] = "Running style used in that team's race. Amber = two share a style; red = all three share a style.",
            ["Avg"] = "Mean base score per trial (all score bonuses removed).",
            ["Trimmed"] = "Average after dropping one outlier trial if the lowest score is below 80% of the team mean.",
            ["CV"] = "Coefficient of variation (stdev ÷ mean), shown as percent. Lower is more consistent.",
            ["Adj CV"] = "CV after removing one outlier low trial when the trim rule applies, shown as percent.",
            ["Gap"] = "Points below the best trimmed base score on the roster.",
            ["Ceiling"] = "90th percentile base score across trials.",
            ["Floor"] = "10th percentile base score across trials.",
            ["Spread"] = "Ceiling minus floor — scoring range across trials.",
            ["Retrain"] = "Combined weakness score (85% gap, 15% adjusted CV). Higher = stronger rebuild candidate.",
            ["Ace delta"] = "Trimmed base score difference vs this team's current ace (+ = better than ace). Colored when positive and large enough to suggest a swap (strong = red, weak hint = amber).",
        };

        private UmaAnalysisReport? _lastAnalysisReport;
        private int _analysisRetrainColumnIndex = -1;
        private int _analysisStyleColumnIndex = -1;
        private int _analysisAceDeltaColumnIndex = -1;

        private void PopulateAnalysis(Dictionary<string, TeamTrialResult> trialResults)
        {
            var first = trialResults.First().Value;
            var rosterNames = first.RosterNames;
            var scoreMatrix = BuildBaseScoreMatrix(trialResults.Values, rosterNames.Count);

            _lastAnalysisReport = UmaAnalysisEngine.Analyze(rosterNames, scoreMatrix, first);
            BindAnalysisGrid(_lastAnalysisReport);
            SetStatus(FormatAnalysisSummary(_lastAnalysisReport));
            mainTabControl.SelectedTab = tabPageAnalysis;
        }

        private void ClearAnalysis(string? message = null)
        {
            _lastAnalysisReport = null;
            _analysisRetrainColumnIndex = -1;
            _analysisStyleColumnIndex = -1;
            _analysisAceDeltaColumnIndex = -1;

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

        private static List<IReadOnlyList<double>> BuildBaseScoreMatrix(
            IEnumerable<TeamTrialResult> trials,
            int umaCount)
        {
            var matrix = new List<IReadOnlyList<double>>(umaCount);
            var trialList = trials.ToList();

            for (int uma = 0; uma < umaCount; uma++)
            {
                var scores = new double[trialList.Count];
                for (int t = 0; t < trialList.Count; t++)
                {
                    scores[t] = trialList[t].RosterBaseScores[uma];
                }
                matrix.Add(scores);
            }

            return matrix;
        }

        private void BindAnalysisGrid(UmaAnalysisReport report)
        {
            dataGridViewAnalysis.Columns.Clear();
            dataGridViewAnalysis.Rows.Clear();

            string[] headers =
            [
                "Uma", "Style", "Avg", "Trimmed", "CV", "Adj CV", "Gap", "Ceiling", "Floor",
                "Spread", "Retrain", "Ace delta"
            ];

            foreach (var header in headers)
            {
                int col = dataGridViewAnalysis.Columns.Add(header, header);
                var column = dataGridViewAnalysis.Columns[col];
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
                if (AnalysisColumnTooltips.TryGetValue(header, out string? tip))
                {
                    column.ToolTipText = tip;
                }
            }

            foreach (var row in report.Rows)
            {
                dataGridViewAnalysis.Rows.Add(
                    row.Name,
                    row.RunningStyleLabel,
                    row.Average,
                    row.TrimmedAverage,
                    FormatCoefficientOfVariation(row.CoefficientOfVariation),
                    FormatCoefficientOfVariation(row.AdjustedCoefficientOfVariation),
                    row.GapToTop,
                    row.Ceiling,
                    row.Floor,
                    row.CeilingFloorSpread,
                    row.RetrainPriorityLabel,
                    row.AceDeltaLabel);
            }

            _analysisRetrainColumnIndex = dataGridViewAnalysis.Columns["Retrain"]?.Index ?? -1;
            _analysisStyleColumnIndex = dataGridViewAnalysis.Columns["Style"]?.Index ?? -1;
            _analysisAceDeltaColumnIndex = dataGridViewAnalysis.Columns["Ace delta"]?.Index ?? -1;
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

        /// <summary>CV is stored as a ratio (0–1 for typical scores); display as percent.</summary>
        private static string FormatCoefficientOfVariation(double ratio) => $"{ratio * 100:F2}%";

        private void DataGridViewAnalysis_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_lastAnalysisReport == null || e.RowIndex < 0 || e.ColumnIndex < 0
                || e.RowIndex >= _lastAnalysisReport.Rows.Count)
            {
                return;
            }

            var analysisRow = _lastAnalysisReport.Rows[e.RowIndex];

            if (e.ColumnIndex == _analysisRetrainColumnIndex)
            {
                Color backColor = analysisRow.PriorityLevel switch
                {
                    RetrainPriorityLevel.Critical => SeverityCritical,
                    RetrainPriorityLevel.NeedsWork => SeverityNeedsWork,
                    _ => SeverityStable
                };

                e.CellStyle.BackColor = backColor;
                var fore = AppColors.SeverityForeFor(backColor);
                e.CellStyle.ForeColor = fore;
                e.CellStyle.SelectionBackColor = ControlPaint.Light(backColor, 0.28f);
                e.CellStyle.SelectionForeColor = fore;
                return;
            }

            if (e.ColumnIndex == _analysisStyleColumnIndex)
            {
                Color? overlapColor = analysisRow.StyleOverlap switch
                {
                    TeamStyleOverlap.FullTeam => SeverityCritical,
                    TeamStyleOverlap.Pair => SeverityNeedsWork,
                    _ => null
                };

                if (overlapColor.HasValue)
                {
                    e.CellStyle.BackColor = overlapColor.Value;
                    var fore = AppColors.SeverityForeFor(overlapColor.Value);
                    e.CellStyle.ForeColor = fore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(overlapColor.Value, 0.28f);
                    e.CellStyle.SelectionForeColor = fore;
                    return;
                }
            }

            if (e.ColumnIndex == _analysisAceDeltaColumnIndex)
            {
                // Only color when this support beats the ace (positive delta) and it qualifies as a hint/suggestion.
                if (analysisRow.AceDelta > 0 && analysisRow.IsSuggestedAce)
                {
                    // Look up the recommendation to know if it was a strong or weak hint.
                    var rec = _lastAnalysisReport.AceRecommendations
                        .FirstOrDefault(r => r.Distance == analysisRow.Distance && r.BestSupport == analysisRow.Name);

                    Color backColor = (rec != null && !rec.IsWeakSuggestion)
                        ? SeverityCritical
                        : SeverityNeedsWork;

                    e.CellStyle.BackColor = backColor;
                    var fore = AppColors.SeverityForeFor(backColor);
                    e.CellStyle.ForeColor = fore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(backColor, 0.28f);
                    e.CellStyle.SelectionForeColor = fore;
                }
                // Aces show "+0 (+0%)" with no special color. Negative deltas (worse than ace) also get no color.
                return;
            }

            if (ShouldBoldAceName(dataGridViewAnalysis, e.RowIndex, e.ColumnIndex))
            {
                e.CellStyle.Font = _aceNameFontCache.GetBoldFont(dataGridViewAnalysis);
            }
        }
    }
}