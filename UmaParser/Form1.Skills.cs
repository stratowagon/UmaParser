using System.ComponentModel;
using System.Drawing;
using UmaParser.Analysis;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.ObjectModel;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private static readonly Dictionary<string, string> SkillsColumnTooltips = new(StringComparer.Ordinal)
        {
            ["Skill"] = "Skill name from master data.",
            ["Pts/race"] = "Observed team-trial skill points per race (attributed score points ÷ races).",
            ["Activations"] = "Total times this skill activated across all races.",
            ["≥1/race %"] = "Percent of races where the skill activated at least once.",
            ["Procs/race %"] = "Total activations ÷ races. Can exceed 100% when a skill procs more than once in a race.",
            ["Δ wit"] = "≥1/race % minus expected wit rate for this uma. — = skill is not wit-gated.",
        };

        private IReadOnlyDictionary<string, TeamTrialResult>? _skillsTrialResults;
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

            // Reset non-TT state so old data doesn't leak into TT loads
            _currentSingleRaces?.Clear();
            _currentNonTtIdentities?.Clear();
        }

        private void ComboBoxSkillsUma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (comboBoxSkillsUma.SelectedIndex < 0)
                return;

            if (_skillsTrialResults != null && comboBoxSkillsUma.SelectedIndex < _skillsRosterEntries.Count)
            {
                // Classic TT path
                var (trainedCharaId, name) = _skillsRosterEntries[comboBoxSkillsUma.SelectedIndex];
                _lastSkillReport = SkillActivationAnalyzer.Analyze(
                    _skillsTrialResults.Values,
                    trainedCharaId,
                    name);
                BindSkillsGrid(_lastSkillReport);
                return;
            }

            // Non-TT path
            if (_currentNonTtIdentities != null && comboBoxSkillsUma.SelectedIndex < _currentNonTtIdentities.Count && _currentSingleRaces != null)
            {
                var selectedId = _currentNonTtIdentities[comboBoxSkillsUma.SelectedIndex];
                var displayName = selectedId.GetDisplayName();

                var apps = new List<RaceAppearance>();
                foreach (var sr in _currentSingleRaces)
                {
                    foreach (var (id, horse) in sr.LocalPlayerHorses)
                    {
                        if (selectedId.IsSameUma(id))
                        {
                            // horse is already a fully materialized RaceHorseData (no JsonDocument dependency)
                            // Minimal result is sufficient for Skills (only simulation events + horse stats are used)
                            var dummyResult = new RaceResult();
                            apps.Add(new RaceAppearance(dummyResult, sr.Simulation, horse));
                        }
                    }
                }

                if (apps.Count > 0)
                {
                    _lastSkillReport = SkillActivationAnalyzer.Analyze(apps, displayName);
                    BindSkillsGrid(_lastSkillReport);
                }
                else
                {
                    labelSkillsSummary.Text = $"{displayName}: No appearances found.";
                    dataGridViewSkills.Rows.Clear();
                    dataGridViewSkills.Columns.Clear();
                }
                return;
            }

            // Fallback placeholder
            if (comboBoxSkillsUma.SelectedIndex < _skillsRosterEntries.Count)
            {
                var (_, displayName) = _skillsRosterEntries[comboBoxSkillsUma.SelectedIndex];
                labelSkillsSummary.Text = $"{displayName}: analysis not available for this selection.";
            }
        }

        private void BindSkillsGrid(SkillActivationReport report)
        {
            dataGridViewSkills.Columns.Clear();
            dataGridViewSkills.Rows.Clear();
            _skillsWitDeltaColumnIndex = -1;

            bool isTt = _skillsTrialResults != null;
            bool showPoints = isTt;  // suppress Pts/race for non-TT (no equivalent scoring)

            AddSkillsColumn("Skill", typeof(string));
            if (showPoints)
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
                };
                if (showPoints)
                    values.Add(row.PointsPerRace);
                values.Add(row.ActivationCount);
                values.Add(row.PerRaceActivationRatePercent);
                values.Add(row.ActivationRatePercent);

                if (report.HasSkillLotMetadata)
                {
                    values.Add(row.WitDeltaPercent.HasValue ? row.WitDeltaPercent.Value : DBNull.Value);
                }

                int rowIndex = dataGridViewSkills.Rows.Add(values.Cast<object>().ToArray());
                dataGridViewSkills.Rows[rowIndex].Tag = row;
            }

            labelSkillsSummary.Text =
                $"{report.UmaName}: {report.Rows.Count} skills, {report.RaceCount} race(s). " +
                $"Click column headers to sort. " +
                $"Avg wit {report.AverageWit} → {report.ExpectedWitPercentForUma:0.#}% activation rate. ";

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
                || e.CellStyle == null
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

            if (header == "Skill")
            {
                // Color code the skill name column by its inherent type (white/gold/unique).
                // This takes precedence over any low-activation warning coloring for the name column.
                double avg = row.ActivationCount > 0
                    ? row.TotalPointsEarned / (double)row.ActivationCount
                    : 0.0;

                if (avg >= 1150 && avg <= 1250) // gold skill (~1200 pts)
                {
                    e.CellStyle.BackColor = AppColors.SkillGoldBack;
                    e.CellStyle.ForeColor = AppColors.SkillGoldFore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(e.CellStyle.BackColor, 0.25f);
                    e.CellStyle.SelectionForeColor = AppColors.SkillGoldFore;
                }
                else if (avg > 1250 || (avg > 0 && avg < 450)) // unique (variable, often higher or specific)
                {
                    e.CellStyle.BackColor = AppColors.SkillUniqueBack;
                    e.CellStyle.ForeColor = AppColors.SkillUniqueFore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(e.CellStyle.BackColor, 0.25f);
                    e.CellStyle.SelectionForeColor = AppColors.SkillUniqueFore;
                }
                // white/normal (~500) or zero observed procs: leave the default background we reset to above

                return; // do not apply warning yellow to the name column
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
            if (e.CellStyle == null)
            {
                return;
            }

            e.CellStyle.BackColor = backColor;
            var fore = AppColors.SeverityForeFor(backColor);
            e.CellStyle.ForeColor = fore;
            e.CellStyle.SelectionBackColor = ControlPaint.Light(backColor, 0.28f);
            e.CellStyle.SelectionForeColor = fore;
        }
    }
}