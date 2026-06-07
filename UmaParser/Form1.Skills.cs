using System.ComponentModel;
using System.Drawing;
using UmaBlobber.Analysis;
using UmaBlobber.MasterData;
using UmaBlobber.ObjectModel;

namespace UmaBlobber
{
    public partial class Form1
    {
        private static readonly Dictionary<string, string> SkillsColumnTooltips = new(StringComparer.Ordinal)
        {
            ["Skill"] = "Skill name from master data.",
            ["Pts/race"] = "Observed team-trial skill points per race (total skill score events ÷ races). Good-start bonus is excluded.",
            ["Activations"] = "Total times this skill activated across all races in the sample.",
            ["≥1/race %"] = "Percent of races where the skill activated at least once.",
            ["Procs/race %"] = "Total activations ÷ races. Can exceed 100% when a skill procs more than once in a race.",
            ["Δ wit"] = "≥1/race % minus expected wit activation rate for this uma. — = skill is not wit-gated.",
        };

        private Dictionary<string, TeamTrialResult>? _skillsTrialResults;
        private List<(int TrainedCharaId, string Name)> _skillsRosterEntries = new();
        private SkillActivationReport? _lastSkillReport;
        private int _skillsWitDeltaColumnIndex = -1;

        private void PopulateSkills(Dictionary<string, TeamTrialResult> trialResults)
        {
            _skillsTrialResults = trialResults;
            var first = trialResults.First().Value;

            _skillsRosterEntries.Clear();
            var names = first.RosterNames;
            int i = 0;
            foreach (var uma in first.RaceRoster.Values)
            {
                _skillsRosterEntries.Add((uma.TrainedCharaId, names[i]));
                i++;
            }

            comboBoxSkillsUma.Items.Clear();
            foreach (var entry in _skillsRosterEntries)
            {
                comboBoxSkillsUma.Items.Add(entry.Name);
            }

            if (comboBoxSkillsUma.Items.Count > 0)
            {
                comboBoxSkillsUma.SelectedIndex = 0;
            }
        }

        private void ClearSkills()
        {
            _skillsTrialResults = null;
            _skillsRosterEntries.Clear();
            _lastSkillReport = null;
            _skillsWitDeltaColumnIndex = -1;
            comboBoxSkillsUma.Items.Clear();
            dataGridViewSkills.Rows.Clear();
            dataGridViewSkills.Columns.Clear();
            labelSkillsSummary.Text = string.Empty;
        }

        private void ComboBoxSkillsUma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_skillsTrialResults == null || comboBoxSkillsUma.SelectedIndex < 0)
            {
                return;
            }

            if (comboBoxSkillsUma.SelectedIndex >= _skillsRosterEntries.Count)
            {
                return;
            }

            var (trainedCharaId, name) = _skillsRosterEntries[comboBoxSkillsUma.SelectedIndex];
            _lastSkillReport = SkillActivationAnalyzer.Analyze(
                _skillsTrialResults.Values,
                trainedCharaId,
                name);
            BindSkillsGrid(_lastSkillReport);
        }

        private void BindSkillsGrid(SkillActivationReport report)
        {
            dataGridViewSkills.Columns.Clear();
            dataGridViewSkills.Rows.Clear();
            _skillsWitDeltaColumnIndex = -1;

            AddSkillsColumn("Skill", typeof(string));
            AddSkillsColumn("Pts/race", typeof(double));
            AddSkillsColumn("Activations", typeof(int));
            AddSkillsColumn("≥1/race %", typeof(double));
            AddSkillsColumn("Procs/race %", typeof(double));

            if (report.HasSkillLotMetadata)
            {
                _skillsWitDeltaColumnIndex = AddSkillsColumn("Δ wit", typeof(double));
            }

            foreach (var row in report.Rows)
            {
                var values = new List<object?>
                {
                    row.SkillName,
                    row.PointsPerRace,
                    row.ActivationCount,
                    row.PerRaceActivationRatePercent,
                    row.ActivationRatePercent,
                };

                if (report.HasSkillLotMetadata)
                {
                    values.Add(row.WitDeltaPercent.HasValue ? row.WitDeltaPercent.Value : DBNull.Value);
                }

                int rowIndex = dataGridViewSkills.Rows.Add(values.Cast<object>().ToArray());
                dataGridViewSkills.Rows[rowIndex].Tag = row;
            }

            string lotNote = report.HasSkillLotMetadata
                ? "Δ wit compares ≥1/race % to expected wit activation (— = not wit-gated)."
                : "Regenerate embedded master data to enable wit delta column.";

            labelSkillsSummary.Text =
                $"{report.UmaName}: {report.Rows.Count} skills, {report.RaceCount} race(s). " +
                $"Click column headers to sort. " +
                $"Avg wit {report.AverageWit} → {report.ExpectedWitPercentForUma:0.#}% activation rate when wit-gated. " +
                $"Procs/race can exceed 100%. Good-start bonus (Focus/Concentration) is excluded. {lotNote}";

            var ptsColumn = dataGridViewSkills.Columns["Pts/race"];
            if (ptsColumn != null)
            {
                dataGridViewSkills.Sort(ptsColumn, ListSortDirection.Descending);
            }

            FinalizeSkillsGrid();
        }

        private int AddSkillsColumn(string header, Type valueType)
        {
            int col = dataGridViewSkills.Columns.Add(header, header);
            var column = dataGridViewSkills.Columns[col];
            column.ValueType = valueType;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            if (SkillsColumnTooltips.TryGetValue(header, out string? tip))
            {
                column.ToolTipText = tip;
            }

            return col;
        }

        private void DataGridViewSkills_SortCompare(object? sender, DataGridViewSortCompareEventArgs e)
        {
            if (_skillsWitDeltaColumnIndex < 0 || e.Column.Index != _skillsWitDeltaColumnIndex)
            {
                return;
            }

            var rowA = dataGridViewSkills.Rows[e.RowIndex1].Tag as SkillActivationRow;
            var rowB = dataGridViewSkills.Rows[e.RowIndex2].Tag as SkillActivationRow;
            if (rowA == null || rowB == null)
            {
                return;
            }

            double? a = rowA.WitDeltaPercent;
            double? b = rowB.WitDeltaPercent;
            if (!a.HasValue && !b.HasValue)
            {
                e.SortResult = 0;
            }
            else if (!a.HasValue)
            {
                e.SortResult = 1;
            }
            else if (!b.HasValue)
            {
                e.SortResult = -1;
            }
            else
            {
                e.SortResult = a.Value.CompareTo(b.Value);
            }

            e.Handled = true;
        }

        private void DataGridViewSkills_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0
                || dataGridViewSkills.Rows[e.RowIndex].Tag is not SkillActivationRow row)
            {
                return;
            }

            var defaultStyle = dataGridViewSkills.DefaultCellStyle;
            e.CellStyle.BackColor = defaultStyle.BackColor;
            e.CellStyle.ForeColor = defaultStyle.ForeColor;
            e.CellStyle.SelectionBackColor = defaultStyle.SelectionBackColor;
            e.CellStyle.SelectionForeColor = defaultStyle.SelectionForeColor;

            string? header = e.ColumnIndex >= 0 && e.ColumnIndex < dataGridViewSkills.Columns.Count
                ? dataGridViewSkills.Columns[e.ColumnIndex].HeaderText
                : null;

            if (header == "Pts/race" && e.Value is double pts)
            {
                e.Value = pts.ToString("0");
                e.FormattingApplied = true;
            }
            else if (header == "≥1/race %" && e.Value is double perRace)
            {
                e.Value = perRace.ToString("0.#");
                e.FormattingApplied = true;
            }
            else if (header == "Procs/race %" && e.Value is double procRate)
            {
                e.Value = procRate.ToString("0.#");
                e.FormattingApplied = true;
            }
            else if (header == "Δ wit")
            {
                e.Value = FormatWitDelta(row);
                e.FormattingApplied = true;
            }

            if (row.IsUnderperformingVsWit)
            {
                ApplySeverityStyle(e, SeverityNeedsWork);
                return;
            }

            if (row.IsLowActivation && row.ActivateLot != SkillActivateLotKind.Wit)
            {
                ApplySeverityStyle(e, SeverityNeedsWork);
            }
        }

        private static string FormatWitDelta(SkillActivationRow row)
        {
            if (!row.WitDeltaPercent.HasValue)
            {
                return "—";
            }

            double delta = row.WitDeltaPercent.Value;
            return delta >= 0 ? $"+{delta:0.#}" : delta.ToString("0.#");
        }

        private void FinalizeSkillsGrid()
        {
            dataGridViewSkills.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            foreach (DataGridViewColumn column in dataGridViewSkills.Columns)
            {
                column.Width = column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true) + 8;
            }

            ClearGridSelection(dataGridViewSkills);
        }

        private static void ApplySeverityStyle(DataGridViewCellFormattingEventArgs e, Color backColor)
        {
            e.CellStyle.BackColor = backColor;
            e.CellStyle.ForeColor = SeverityForeground;
            e.CellStyle.SelectionBackColor = ControlPaint.Light(backColor, 0.28f);
            e.CellStyle.SelectionForeColor = SeverityForeground;
        }
    }
}