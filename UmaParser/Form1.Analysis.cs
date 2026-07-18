using System.Drawing;
using UmaParser.Analysis;
using UmaParser.ObjectModel;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private UmaAnalysisReport? _lastAnalysisReport;

        private void PopulateAnalysis(Dictionary<string, TeamTrialResult> trialResults)
        {
            var layoutTrial = trialResults.Last().Value;
            var canonicalIds = layoutTrial.RosterTrainedCharaIds;
            var rosterNames = layoutTrial.RosterNames;
            var trialValues = trialResults.Values;
            var scoreMatrix = BuildCategoryScoreMatrix(trialValues, canonicalIds, (t, id) => t.UmaBaseScore(id));
            var skillMatrix = BuildCategoryScoreMatrix(trialValues, canonicalIds, (t, id) => t.UmaSkillBaseScore(id));
            var otherMatrix = BuildCategoryScoreMatrix(trialValues, canonicalIds, (t, id) => t.UmaOtherBaseScore(id));

            _lastAnalysisReport = UmaAnalysisEngine.Analyze(
                rosterNames,
                scoreMatrix,
                layoutTrial,
                skillMatrix,
                otherMatrix,
                canonicalIds,
                trialValues);
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

        private static List<IReadOnlyList<double>> BuildCategoryScoreMatrix(
            IEnumerable<TeamTrialResult> trials,
            IReadOnlyList<int> canonicalIds,
            Func<TeamTrialResult, int, int> getScore)
        {
            var matrix = new List<IReadOnlyList<double>>(canonicalIds.Count);
            var trialList = trials.ToList();

            var trialScoreMaps = new List<Dictionary<int, int>>(trialList.Count);
            foreach (var t in trialList)
            {
                var map = new Dictionary<int, int>();
                foreach (var id in t.RosterTrainedCharaIds)
                {
                    map[id] = getScore(t, id);
                }
                trialScoreMaps.Add(map);
            }

            for (int i = 0; i < canonicalIds.Count; i++)
            {
                int id = canonicalIds[i];
                var scores = new double[trialList.Count];
                for (int t = 0; t < trialList.Count; t++)
                {
                    scores[t] = trialScoreMaps[t].TryGetValue(id, out var b) ? b : 0;
                }
                matrix.Add(scores);
            }

            return matrix;
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

            if (columnId == GridColumnIds.Analysis.Style)
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

            if (columnId == GridColumnIds.Analysis.AvgPlace && e.Value is double avgPlace)
            {
                e.Value = avgPlace.ToString("0.00");
                e.FormattingApplied = true;
            }

            if (columnId == GridColumnIds.Analysis.AceDelta)
            {
                if (analysisRow.AceDelta > 0 && analysisRow.IsSuggestedAce)
                {
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
                return;
            }

            if (ShouldBoldAceName(dataGridViewAnalysis, e.RowIndex, e.ColumnIndex))
            {
                e.CellStyle.Font = _aceNameFontCache.GetBoldFont(dataGridViewAnalysis);
            }
        }
    }
}