using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Presentation;
using UmaParser.Settings;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private IReadOnlyDictionary<string, TeamTrialResult>? _skillsTrialResults;
        private SkillActivationReport? _lastSkillReport;
        private SkillsScreen? _skillsScreen;
        private bool _suppressSkillsSelection;

        private void PopulateSkills(Dictionary<string, TeamTrialResult> trialResults)
        {
            _skillsTrialResults = trialResults;
            var batch = new TeamTrialBatch
            {
                Kind = TeamTrialBatchKind.RosterStats,
                Trials = trialResults,
            };
            ShowSkills(SkillsScreen.Load(batch, SkillsColumnPreferences()));
        }

        private GridColumnGridPreferences SkillsColumnPreferences() =>
            LoadGridColumnPreferences<SkillActivationRow>(GridKeys.Skills);

        private void ShowSkills(SkillsScreen screen)
        {
            _skillsScreen = screen;
            _lastSkillReport = screen.Report;

            _suppressSkillsSelection = true;
            try
            {
                comboBoxSkillsUma.Items.Clear();
                foreach (SkillsUmaOption uma in screen.Umas)
                {
                    comboBoxSkillsUma.Items.Add(uma.Name);
                }

                comboBoxSkillsUma.SelectedIndex = screen.SelectedIndex;
            }
            finally
            {
                _suppressSkillsSelection = false;
            }

            if (screen.Report != null)
            {
                BindSkillsGrid(screen.Report);
            }
            else
            {
                dataGridViewSkills.Rows.Clear();
                dataGridViewSkills.Columns.Clear();
            }

            // BindSkillsGrid writes the per-uma report line. A fresh mismatch or single-race
            // load replaces that with the screen's load summary, matching the old order:
            // the combo event bound the grid, then the caller set the summary.
            labelSkillsSummary.Text = screen.Summary;
        }

        private void ClearSkills()
        {
            _skillsTrialResults = null;
            _skillsScreen = null;
            _lastSkillReport = null;
            _suppressSkillsSelection = true;
            comboBoxSkillsUma.Items.Clear();
            _suppressSkillsSelection = false;
            dataGridViewSkills.Rows.Clear();
            dataGridViewSkills.Columns.Clear();
            labelSkillsSummary.Text = string.Empty;

            _currentSingleRaces?.Clear();
            _currentNonTtIdentities?.Clear();
        }

        private void ComboBoxSkillsUma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressSkillsSelection || _skillsScreen == null)
                return;

            if (comboBoxSkillsUma.SelectedIndex < 0 || comboBoxSkillsUma.SelectedIndex == _skillsScreen.SelectedIndex)
                return;

            ShowSkills(_skillsScreen.Select(comboBoxSkillsUma.SelectedIndex));
            _openOptionsDialog?.RefreshAvailability();
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

            labelSkillsSummary.Text = SkillsScreen.FormatReport(report);

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

            e.SortResult = SkillWitDeltaSort.Compare(rowA.WitDeltaPercent, rowB.WitDeltaPercent);
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
                e.Value = SkillCellText.PointsPerRace(pts);
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Skills.PerRacePct && e.Value is double perRace)
            {
                e.Value = SkillCellText.OneDecimal(perRace);
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Skills.ProcsPerRacePct && e.Value is double procRate)
            {
                e.Value = SkillCellText.OneDecimal(procRate);
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Skills.WitDelta)
            {
                e.Value = SkillCellText.FormatWitDelta(row);
                e.FormattingApplied = true;
            }

            if (columnId == GridColumnIds.Skills.Skill)
            {
                CellBandStyling.Apply(e, CellBands.ForSkillName(row));
                return;
            }

            CellBandStyling.Apply(e, CellBands.ForSkillMetric(row));
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
    }
}
