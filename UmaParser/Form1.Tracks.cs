using System.ComponentModel;
using System.Drawing;
using UmaBlobber.Analysis;
using UmaBlobber.ObjectModel;
using UmaBlobber.Ui;

namespace UmaBlobber
{
    public partial class Form1
    {
        private static readonly Dictionary<string, string> TracksColumnTooltips = new(StringComparer.Ordinal)
        {
            ["Track"] = "Race course + distance. Surface is implied by the track (internally differentiated by ground).",
            ["Races"] = "Number of times this uma ran this specific track+distance.",
            ["Avg Place"] = "Average finish position (1 = 1st) across runs on this track.",
            ["% 1st"] = "Percentage of races on this track where the uma finished 1st.",
            ["% Top 3"] = "Percentage of races finishing 3rd or better.",
            ["% Top 5"] = "Percentage of races finishing 5th or better.",
            ["Spurt Rate"] = "% of races on this track where the uma started their final spurt no more than ~3m after the ideal 2/3 distance point (based on LastSpurtStartDistance in replay data). A value of -1 means the uma never attempted a spurt (insufficient stamina) and counts against the rate.",
            ["Survival"] = "% of races on this track where the uma reached the goal with >0 HP remaining (never hit 0 HP before or at the goal line in the frame data).",
            ["Avg Skill Pts/Race"] = "Average skill points earned per race on this track (condition-8 scores only, no bonuses).",
        };

        private Dictionary<string, TeamTrialResult>? _tracksTrialResults;
        private List<(int TrainedCharaId, string Name)> _tracksRosterEntries = new();
        private TracksReport? _lastTracksReport;

        private void PopulateTracks(Dictionary<string, TeamTrialResult> trialResults)
        {
            _tracksTrialResults = trialResults;
            var first = trialResults.First().Value;

            _tracksRosterEntries.Clear();
            var names = first.RosterNames;
            int i = 0;
            foreach (var uma in first.RaceRoster.Values)
            {
                _tracksRosterEntries.Add((uma.TrainedCharaId, names[i]));
                i++;
            }

            comboBoxTracksUma.Items.Clear();
            foreach (var entry in _tracksRosterEntries)
            {
                comboBoxTracksUma.Items.Add(entry.Name);
            }

            if (comboBoxTracksUma.Items.Count > 0)
            {
                comboBoxTracksUma.SelectedIndex = 0;
            }
        }

        private void ClearTracks()
        {
            _tracksTrialResults = null;
            _tracksRosterEntries.Clear();
            _lastTracksReport = null;
            comboBoxTracksUma.Items.Clear();
            dataGridViewTracks.Rows.Clear();
            dataGridViewTracks.Columns.Clear();
            labelTracksSummary.Text = string.Empty;
        }

        private void ComboBoxTracksUma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_tracksTrialResults == null || comboBoxTracksUma.SelectedIndex < 0)
            {
                return;
            }

            if (comboBoxTracksUma.SelectedIndex >= _tracksRosterEntries.Count)
            {
                return;
            }

            var (trainedCharaId, name) = _tracksRosterEntries[comboBoxTracksUma.SelectedIndex];
            _lastTracksReport = TracksAnalyzer.Analyze(
                _tracksTrialResults.Values,
                trainedCharaId,
                name);
            BindTracksGrid(_lastTracksReport!);
        }

        private void BindTracksGrid(TracksReport report)
        {
            dataGridViewTracks.Columns.Clear();
            dataGridViewTracks.Rows.Clear();

            AddTracksColumn("Track", typeof(string));
            AddTracksColumn("Distance", typeof(int));
            AddTracksColumn("Races", typeof(int));
            AddTracksColumn("Avg Place", typeof(double));
            AddTracksColumn("% 1st", typeof(double));
            AddTracksColumn("% Top 3", typeof(double));
            AddTracksColumn("% Top 5", typeof(double));
            AddTracksColumn("Spurt Rate", typeof(double));
            AddTracksColumn("Survival", typeof(double));
            AddTracksColumn("Avg Skill Pts/Race", typeof(double));

            foreach (var row in report.Rows)
            {
                var values = new List<object?>
                {
                    row.Track,
                    row.Distance,
                    row.Races,
                    row.AvgPlace,
                    row.PctFirst,
                    row.PctTop3,
                    row.PctTop5,
                    row.SpurtRate,
                    row.Survival,
                    row.AvgSkillPointsPerRace,
                };

                int rowIndex = dataGridViewTracks.Rows.Add(values.Cast<object>().ToArray());
                dataGridViewTracks.Rows[rowIndex].Tag = row;
            }

            // Auto-width adjustment after populating the grid, like the other tabs (Skills, Analysis)
            FinalizeTracksGrid();

            labelTracksSummary.Text =
                $"{report.UmaName}: {report.Rows.Count} tracks, {report.TotalRaces} race(s) total. Team type: {report.TeamType}. ";

            if (dataGridViewTracks.Columns.Count > 0)
            {
                dataGridViewTracks.Sort(dataGridViewTracks.Columns[0], ListSortDirection.Ascending);
            }
        }

        private int AddTracksColumn(string header, Type valueType)
        {
            int col = dataGridViewTracks.Columns.Add(header, header);
            var column = dataGridViewTracks.Columns[col];
            column.ValueType = valueType;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            if (TracksColumnTooltips.TryGetValue(header, out string? tip))
            {
                column.ToolTipText = tip;
            }

            return col;
        }

        private void DataGridViewTracks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0
                || dataGridViewTracks.Rows[e.RowIndex].Tag is not TracksRow row)
            {
                return;
            }

            var defaultStyle = dataGridViewTracks.DefaultCellStyle;
            e.CellStyle.BackColor = defaultStyle.BackColor;
            e.CellStyle.ForeColor = defaultStyle.ForeColor;
            e.CellStyle.SelectionBackColor = defaultStyle.SelectionBackColor;
            e.CellStyle.SelectionForeColor = defaultStyle.SelectionForeColor;

            string? header = e.ColumnIndex >= 0 && e.ColumnIndex < dataGridViewTracks.Columns.Count
                ? dataGridViewTracks.Columns[e.ColumnIndex].HeaderText
                : null;

            if ((header == "% 1st" || header == "% Top 3" || header == "% Top 5") && e.Value is double pct)
            {
                e.Value = pct.ToString("0.#") + "%";
                e.FormattingApplied = true;
            }
            else if (header == "Avg Place" && e.Value is double ap)
            {
                e.Value = ap.ToString("0.00");
                e.FormattingApplied = true;
            }
            else if (header == "Spurt Rate" && e.Value is double rate)
            {
                e.Value = rate.ToString("0.#") + "%";
                e.FormattingApplied = true;

                if (rate < 95)
                {
                    var back = AppColors.SeverityCriticalBack;
                    e.CellStyle.BackColor = back;
                    var fore = AppColors.SeverityForeFor(back);
                    e.CellStyle.ForeColor = fore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(back, 0.28f);
                    e.CellStyle.SelectionForeColor = fore;
                }
                else if (rate < 100)
                {
                    var back = AppColors.SeverityNeedsWorkBack;
                    e.CellStyle.BackColor = back;
                    var fore = AppColors.SeverityForeFor(back);
                    e.CellStyle.ForeColor = fore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(back, 0.28f);
                    e.CellStyle.SelectionForeColor = fore;
                }
            }
            else if (header == "Survival" && e.Value is double surv)
            {
                e.Value = surv.ToString("0.#") + "%";
                e.FormattingApplied = true;

                if (surv < 95)
                {
                    var back = AppColors.SeverityCriticalBack;
                    e.CellStyle.BackColor = back;
                    var fore = AppColors.SeverityForeFor(back);
                    e.CellStyle.ForeColor = fore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(back, 0.28f);
                    e.CellStyle.SelectionForeColor = fore;
                }
                else if (surv < 100)
                {
                    var back = AppColors.SeverityNeedsWorkBack;
                    e.CellStyle.BackColor = back;
                    var fore = AppColors.SeverityForeFor(back);
                    e.CellStyle.ForeColor = fore;
                    e.CellStyle.SelectionBackColor = ControlPaint.Light(back, 0.28f);
                    e.CellStyle.SelectionForeColor = fore;
                }
            }
            else if (header == "Avg Skill Pts/Race" && e.Value is double sp)
            {
                e.Value = sp.ToString("0");
                e.FormattingApplied = true;
            }
        }

        private void FinalizeTracksGrid()
        {
            dataGridViewTracks.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            foreach (DataGridViewColumn column in dataGridViewTracks.Columns)
            {
                column.Width = column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true) + 8;
            }

            ClearGridSelection(dataGridViewTracks);
        }
    }
}
