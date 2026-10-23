using System.ComponentModel;
using System.Drawing;
using UmaParser.Analysis;
using UmaParser.ObjectModel;
using UmaParser.Settings;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private static readonly object TracksFooterRowTag = new();

        private IReadOnlyDictionary<string, TeamTrialResult>? _tracksTrialResults;
        private List<NamedRosterUma> _tracksRosterEntries = new();
        private TracksReport? _lastTracksReport;

        private void PopulateTracks(Dictionary<string, TeamTrialResult> trialResults)
        {
            _tracksTrialResults = trialResults;
            var first = trialResults.First().Value;
            _tracksRosterEntries = RosterListing.FromTrial(first);

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

            _currentSingleRaces?.Clear();
            _currentNonTtIdentities?.Clear();
        }

        private void ComboBoxTracksUma_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (comboBoxTracksUma.SelectedIndex < 0)
                return;

            if (_tracksTrialResults != null && comboBoxTracksUma.SelectedIndex < _tracksRosterEntries.Count)
            {
                var (trainedCharaId, name) = _tracksRosterEntries[comboBoxTracksUma.SelectedIndex];
                _lastTracksReport = TracksAnalyzer.Analyze(
                    _tracksTrialResults.Values,
                    trainedCharaId,
                    name);
                BindTracksGrid(_lastTracksReport!);
                _openOptionsDialog?.RefreshAvailability();
                return;
            }

            if (_currentNonTtIdentities != null && comboBoxTracksUma.SelectedIndex < _currentNonTtIdentities.Count && _currentSingleRaces != null)
            {
                var selectedId = _currentNonTtIdentities[comboBoxTracksUma.SelectedIndex];
                var displayName = selectedId.GetDisplayName();
                var nonTtItems = RaceAppearanceBuilder.ForTracks(_currentSingleRaces, selectedId);

                if (nonTtItems.Count > 0)
                {
                    _lastTracksReport = TracksAnalyzer.Analyze(nonTtItems, displayName);
                    BindTracksGrid(_lastTracksReport!);
                    _openOptionsDialog?.RefreshAvailability();
                }
                else
                {
                    labelTracksSummary.Text = $"{displayName}: No appearances found.";
                    dataGridViewTracks.Rows.Clear();
                    dataGridViewTracks.Columns.Clear();
                }
                return;
            }

            if (comboBoxTracksUma.SelectedIndex < _tracksRosterEntries.Count)
            {
                var (_, displayName) = _tracksRosterEntries[comboBoxTracksUma.SelectedIndex];
                labelTracksSummary.Text = $"{displayName}: tracks analysis not available.";
            }
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

            labelTracksSummary.Text =
                $"{report.UmaName}: {report.Rows.Count} tracks, {report.TotalRaces} race(s) total. Team type: {report.TeamType}. ";

            if (report.Rows.Count > 0)
            {
                GridColumnBinder.ApplyDefaultSortAscending(dataGridViewTracks, GridColumnIds.Tracks.Track);
            }
        }

        private void AddTracksFooterRow(TracksTotals totals, GridColumnGridPreferences prefs, GridColumnContext context)
        {
            var effective = GridColumnBinder.ResolveEffectiveColumns(TracksColumnSet.All, prefs, context);
            var values = GridColumnBinder.BuildRowValues(effective, id => id switch
            {
                GridColumnIds.Tracks.Track => "Total",
                GridColumnIds.Tracks.Distance => null,
                GridColumnIds.Tracks.Races => totals.Races,
                GridColumnIds.Tracks.AvgPlace => totals.AvgPlace,
                GridColumnIds.Tracks.PctFirst => totals.PctFirst,
                GridColumnIds.Tracks.PctTop3 => totals.PctTop3,
                GridColumnIds.Tracks.PctTop5 => totals.PctTop5,
                GridColumnIds.Tracks.SpurtRate => totals.SpurtRate,
                GridColumnIds.Tracks.Survival => totals.Survival,
                GridColumnIds.Tracks.AvgSkillPts => context.IsTeamTrial ? totals.AvgSkillPointsPerRace : null,
                _ => null,
            });

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
                e.Value = pct.ToString("0.#") + "%";
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Tracks.AvgPlace && e.Value is double ap)
            {
                e.Value = ap.ToString("0.00");
                e.FormattingApplied = true;
            }
            else if (columnId == GridColumnIds.Tracks.SpurtRate && e.Value is double rate)
            {
                e.Value = rate.ToString("0.#") + "%";
                e.FormattingApplied = true;

                if (isFooter)
                {
                    return;
                }

                CellBandStyling.Apply(e, CellBands.ForTrackRate(rate));
            }
            else if (columnId == GridColumnIds.Tracks.Survival && e.Value is double surv)
            {
                e.Value = surv.ToString("0.#") + "%";
                e.FormattingApplied = true;

                if (isFooter)
                {
                    return;
                }

                CellBandStyling.Apply(e, CellBands.ForTrackRate(surv));
            }
            else if (columnId == GridColumnIds.Tracks.AvgSkillPts && e.Value is double sp)
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