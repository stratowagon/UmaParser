using System.ComponentModel;
using System.Drawing;
using UmaParser.Analysis;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.ObjectModel;
using UmaParser.Settings;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private IReadOnlyDictionary<string, TeamTrialResult>? _skillsTrialResults;
        private List<(int TrainedCharaId, string Name)> _skillsRosterEntries = new();
        private SkillActivationReport? _lastSkillReport;

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
            comboBoxSkillsUma.Items.Clear();
            dataGridViewSkills.Rows.Clear();
            dataGridViewSkills.Columns.Clear();
            labelSkillsSummary.Text = string.Empty;

            _currentSingleRaces?.Clear();
            _currentNonTtIdentities?.Clear();
        }

        private void ComboBoxSkillsUma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (comboBoxSkillsUma.SelectedIndex < 0)
                return;

            if (_skillsTrialResults != null && comboBoxSkillsUma.SelectedIndex < _skillsRosterEntries.Count)
            {
                var (trainedCharaId, name) = _skillsRosterEntries[comboBoxSkillsUma.SelectedIndex];
                _lastSkillReport = SkillActivationAnalyzer.Analyze(
                    _skillsTrialResults.Values,
                    trainedCharaId,
                    name);
                BindSkillsGrid(_lastSkillReport);
                _openOptionsDialog?.RefreshAvailability();
                return;
            }

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
                            var dummyResult = new RaceResult();
                            apps.Add(new RaceAppearance(dummyResult, sr.Simulation, horse));
                        }
                    }
                }

                if (apps.Count > 0)
                {
                    _lastSkillReport = SkillActivationAnalyzer.Analyze(apps, displayName);
                    BindSkillsGrid(_lastSkillReport);
                    _openOptionsDialog?.RefreshAvailability();
                }
                else
                {
                    labelSkillsSummary.Text = $"{displayName}: No appearances found.";
                    dataGridViewSkills.Rows.Clear();
                    dataGridViewSkills.Columns.Clear();
                }
                return;
            }

            if (comboBoxSkillsUma.SelectedIndex < _skillsRosterEntries.Count)
            {
                var (_, displayName) = _skillsRosterEntries[comboBoxSkillsUma.SelectedIndex];
                labelSkillsSummary.Text = $"{displayName}: analysis not available for this selection.";
            }
        }

        private void BindSkillsGrid(SkillActivationReport report)
        {
            var prefs = LoadGridColumnPreferences<SkillActivationRow>(GridKeys.Skills);
            var context = new GridColumnContext
            {
                IsTeamTrial = _skillsTrialResults != null,
                HasSkillLotMetadata = report.HasSkillLotMetadata,
            };

            GridColumnBinder.Bind(
                dataGridViewSkills,
                SkillsColumnSet.All,
                prefs,
                context,
                report.Rows,
                DataGridViewColumnSortMode.Automatic,
                (row, data) => row.Tag = data);

            labelSkillsSummary.Text =
                $"{report.UmaName}: {report.Rows.Count} skills, {report.RaceCount} race(s). " +
                $"Click column headers to sort. " +
                $"Avg wit {report.AverageWit} → {report.ExpectedWitPercentForUma:0.#}% activation rate. ";

            GridColumnBinder.ApplyDefaultSort(
                dataGridViewSkills,
                GridColumnIds.Skills.PtsRace,
                GridColumnIds.Skills.Activations);

            FinalizeSkillsGrid();
        }

        private void DataGridViewSkills_SortCompare(object? sender, DataGridViewSortCompareEventArgs e)
        {
            if (e.Column.Name != GridColumnIds.Skills.WitDelta)
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

            string? columnId = e.ColumnIndex >= 0 && e.ColumnIndex < dataGridViewSkills.Columns.Count
                ? dataGridViewSkills.Columns[e.ColumnIndex].Name
                : null;

            if (columnId == GridColumnIds.Skills.PtsRace && e.Value is double pts)
            {
                e.Value = pts.ToString("0");
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Skills.PerRacePct && e.Value is double perRace)
            {
                e.Value = perRace.ToString("0.#");
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Skills.ProcsPerRacePct && e.Value is double procRate)
            {
                e.Value = procRate.ToString("0.#");
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Skills.WitDelta)
            {
                e.Value = FormatWitDelta(row);
                e.FormattingApplied = true;
            }

            if (columnId == GridColumnIds.Skills.Skill)
            {
                double avg = row.ActivationCount > 0
                    ? row.TotalPointsEarned / (double)row.ActivationCount
                    : 0.0;

                if (avg >= 1150 && avg <= 1250)
                {
                    e.CellStyle.BackColor = AppColors.SkillGoldBack;
                    e.CellStyle.ForeColor = AppColors.SkillGoldFore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(e.CellStyle.BackColor, 0.25f);
                    e.CellStyle.SelectionForeColor = AppColors.SkillGoldFore;
                }
                else if (avg > 1250 || (avg > 0 && avg < 450))
                {
                    e.CellStyle.BackColor = AppColors.SkillUniqueBack;
                    e.CellStyle.ForeColor = AppColors.SkillUniqueFore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(e.CellStyle.BackColor, 0.25f);
                    e.CellStyle.SelectionForeColor = AppColors.SkillUniqueFore;
                }

                return;
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