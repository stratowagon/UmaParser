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
        private static readonly object TracksFooterRowTag = new();

        private IReadOnlyDictionary<string, TeamTrialResult>? _tracksTrialResults;
        private TracksReport? _lastTracksReport;
        private TracksScreen? _tracksScreen;
        private bool _suppressTracksSelection;

        private void PopulateTracks(Dictionary<string, TeamTrialResult> trialResults)
        {
            _tracksTrialResults = trialResults;
            var batch = new TeamTrialBatch
            {
                Kind = TeamTrialBatchKind.RosterStats,
                Trials = trialResults,
            };
            ShowTracks(TracksScreen.Load(batch, TracksColumnPreferences()));
        }

        private GridColumnGridPreferences TracksColumnPreferences() =>
            LoadGridColumnPreferences<TracksRow>(GridKeys.Tracks);

        private void ShowTracks(TracksScreen screen)
        {
            _tracksScreen = screen;

            _suppressTracksSelection = true;
            try
            {
                comboBoxTracksUma.Items.Clear();
                foreach (TracksUmaOption uma in screen.Umas)
                {
                    comboBoxTracksUma.Items.Add(uma.Name);
                }

                comboBoxTracksUma.SelectedIndex = screen.SelectedIndex;
            }
            finally
            {
                _suppressTracksSelection = false;
            }

            if (screen.Report != null)
            {
                // No appearances leaves the previous report in place. The old handler only
                // assigned this when it had appearances to analyze.
                _lastTracksReport = screen.Report;
                BindTracksGrid(screen.Report);
            }
            else
            {
                dataGridViewTracks.Rows.Clear();
                dataGridViewTracks.Columns.Clear();
            }

            // BindTracksGrid writes the per-uma report line. A fresh mismatch load replaces
            // that with the screen's load summary, matching the old order: the combo event
            // bound the grid, then the caller set the summary.
            labelTracksSummary.Text = screen.Summary;
        }

        private void ClearTracks()
        {
            _tracksTrialResults = null;
            _lastTracksReport = null;
            _tracksScreen = null;
            _suppressTracksSelection = true;
            comboBoxTracksUma.Items.Clear();
            _suppressTracksSelection = false;
            dataGridViewTracks.Rows.Clear();
            dataGridViewTracks.Columns.Clear();
            labelTracksSummary.Text = string.Empty;

            _currentSingleRaces?.Clear();
            _currentNonTtIdentities?.Clear();
        }

        private void ComboBoxTracksUma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_suppressTracksSelection || _tracksScreen == null)
                return;

            if (comboBoxTracksUma.SelectedIndex < 0 || comboBoxTracksUma.SelectedIndex == _tracksScreen.SelectedIndex)
                return;

            ShowTracks(_tracksScreen.Select(comboBoxTracksUma.SelectedIndex));
            _openOptionsDialog?.RefreshAvailability();
        }

        private void BindTracksGrid(TracksReport report)
        {
            var prefs = LoadGridColumnPreferences<TracksRow>(GridKeys.Tracks);
            var context = new GridColumnContext
            {
                IsTeamTrial = _tracksTrialResults != null,
            };

            GridColumnBinder.Bind(
                dataGridViewTracks,
                TracksColumnSet.All,
                prefs,
                context,
                report.Rows,
                DataGridViewColumnSortMode.Automatic,
                (row, data) => row.Tag = data);

            if (report.Rows.Count > 0)
            {
                AddTracksFooterRow(TracksTotals.Compute(report.Rows, context.IsTeamTrial), prefs, context);
            }

            FinalizeTracksGrid();

            labelTracksSummary.Text = TrackCellText.Report(report);

            if (report.Rows.Count > 0)
            {
                GridColumnBinder.ApplyDefaultSortAscending(dataGridViewTracks, GridColumnIds.Tracks.Track);
            }
        }

        private void AddTracksFooterRow(TracksTotals totals, GridColumnGridPreferences prefs, GridColumnContext context)
        {
            var effective = GridColumnBinder.ResolveEffectiveColumns(TracksColumnSet.All, prefs, context);
            var values = GridColumnBinder.BuildRowValues(
                effective,
                id => TracksFooterValues.Get(id, totals, context.IsTeamTrial));

            int rowIndex = dataGridViewTracks.Rows.Add(values);
            var footerRow = dataGridViewTracks.Rows[rowIndex];
            footerRow.Tag = TracksFooterRowTag;
            ApplyTracksFooterRowStyle(footerRow);
        }

        private static void ApplyTracksFooterRowStyle(DataGridViewRow row)
        {
            var headerStyle = row.DataGridView?.ColumnHeadersDefaultCellStyle;
            if (headerStyle == null)
            {
                return;
            }

            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.Style.BackColor = headerStyle.BackColor;
                cell.Style.ForeColor = headerStyle.ForeColor;
                cell.Style.Font = headerStyle.Font;
                cell.Style.SelectionBackColor = ControlPaint.Dark(headerStyle.BackColor);
                cell.Style.SelectionForeColor = headerStyle.ForeColor;
            }
        }

        private static bool IsTracksFooterRow(DataGridView grid, int rowIndex) =>
            rowIndex >= 0
            && rowIndex < grid.Rows.Count
            && ReferenceEquals(grid.Rows[rowIndex].Tag, TracksFooterRowTag);

        private void DataGridViewTracks_SortCompare(object? sender, DataGridViewSortCompareEventArgs e)
        {
            bool aIsFooter = IsTracksFooterRow(dataGridViewTracks, e.RowIndex1);
            bool bIsFooter = IsTracksFooterRow(dataGridViewTracks, e.RowIndex2);
            if (!aIsFooter && !bIsFooter)
            {
                return;
            }

            if (aIsFooter && bIsFooter)
            {
                e.SortResult = 0;
            }
            else if (aIsFooter)
            {
                e.SortResult = 1;
            }
            else
            {
                e.SortResult = -1;
            }

            e.Handled = true;
        }

        private void DataGridViewTracks_Sorted(object? sender, EventArgs e)
        {
            EnsureTracksFooterAtBottom();
        }

        private void EnsureTracksFooterAtBottom()
        {
            int footerIdx = -1;
            for (int i = 0; i < dataGridViewTracks.Rows.Count; i++)
            {
                if (IsTracksFooterRow(dataGridViewTracks, i))
                {
                    footerIdx = i;
                    break;
                }
            }

            if (footerIdx < 0 || footerIdx == dataGridViewTracks.Rows.Count - 1)
            {
                return;
            }

            var footer = dataGridViewTracks.Rows[footerIdx];
            var values = new object[dataGridViewTracks.Columns.Count];
            for (int c = 0; c < values.Length; c++)
            {
                values[c] = footer.Cells[c].Value ?? DBNull.Value;
            }

            dataGridViewTracks.Rows.RemoveAt(footerIdx);
            int newIdx = dataGridViewTracks.Rows.Add(values);
            var movedFooter = dataGridViewTracks.Rows[newIdx];
            movedFooter.Tag = TracksFooterRowTag;
            ApplyTracksFooterRowStyle(movedFooter);
        }

        private void DataGridViewTracks_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.CellStyle == null)
            {
                return;
            }

            bool isFooter = IsTracksFooterRow(dataGridViewTracks, e.RowIndex);
            if (!isFooter && dataGridViewTracks.Rows[e.RowIndex].Tag is not TracksRow)
            {
                return;
            }

            if (!isFooter)
            {
                var defaultStyle = dataGridViewTracks.DefaultCellStyle;
                e.CellStyle.BackColor = defaultStyle.BackColor;
                e.CellStyle.ForeColor = defaultStyle.ForeColor;
                e.CellStyle.SelectionBackColor = defaultStyle.SelectionBackColor;
                e.CellStyle.SelectionForeColor = defaultStyle.SelectionForeColor;
            }

            string? columnId = e.ColumnIndex >= 0 && e.ColumnIndex < dataGridViewTracks.Columns.Count
                ? dataGridViewTracks.Columns[e.ColumnIndex].Name
                : null;

            if (columnId == GridColumnIds.Tracks.Distance && isFooter)
            {
                e.Value = string.Empty;
                e.FormattingApplied = true;
                return;
            }

            if ((columnId == GridColumnIds.Tracks.PctFirst
                || columnId == GridColumnIds.Tracks.PctTop3
                || columnId == GridColumnIds.Tracks.PctTop5) && e.Value is double pct)
            {
                e.Value = TrackCellText.Percent(pct);
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Tracks.AvgPlace && e.Value is double ap)
            {
                e.Value = TrackCellText.AveragePlace(ap);
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Tracks.SpurtRate && e.Value is double rate)
            {
                e.Value = TrackCellText.Percent(rate);
                e.FormattingApplied = true;

                if (isFooter)
                {
                    return;
                }

                CellBandStyling.Apply(e, CellBands.ForTrackRate(rate));
            }
            else if (columnId == GridColumnIds.Tracks.Survival && e.Value is double surv)
            {
                e.Value = TrackCellText.Percent(surv);
                e.FormattingApplied = true;

                if (isFooter)
                {
                    return;
                }

                CellBandStyling.Apply(e, CellBands.ForTrackRate(surv));
            }
            else if (columnId == GridColumnIds.Tracks.AvgSkillPts && e.Value is double sp)
            {
                e.Value = TrackCellText.SkillPoints(sp);
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
