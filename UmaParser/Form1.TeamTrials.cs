using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Presentation;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1
    {
        private void BindTeamTrialBatch(TeamTrialBatch batch)
        {
            if (!batch.HasAnyData)
            {
                return;
            }

            ClearGrid();
            ClearAnalysis();
            ClearSkills();
            ClearTracks();

            switch (batch.Kind)
            {
                case TeamTrialBatchKind.RosterMismatch:
                    BindRosterMismatch(batch);
                    break;
                case TeamTrialBatchKind.RosterStats:
                    BindRosterStats(batch);
                    break;
                case TeamTrialBatchKind.MixedOrNonTT:
                    BindMixedOrNonTT(batch);
                    break;
            }

            FinalizeGridDisplay();
            _openOptionsDialog?.RefreshAvailability();
        }

        // Non-TT (single race) data for the current load
        private List<SingleRaceParticipants> _currentSingleRaces = new();
        private List<UmaIdentity> _currentNonTtIdentities = new();

        private void BindMixedOrNonTT(TeamTrialBatch batch)
        {
            _gridDisplayMode = GridDisplayMode.None; // no classic roster grid for mixed/non-TT

            NonTeamTrialLoad load = NonTeamTrialLoad.Collect(batch.SingleRaceImports);
            _currentSingleRaces = load.Races.ToList();
            _currentNonTtIdentities = load.Identities.ToList();
            SetStatus(DropStatusText.Mixed(batch, load));

            // Skills and Tracks share this load so the sim blobs are not parsed a second time.
            // Both screens are the same models the WPF window displays.
            _skillsTrialResults = null; // not TT
            ShowSkills(SkillsScreen.Load(batch, SkillsColumnPreferences(), load));

            _tracksTrialResults = null;
            ShowTracks(TracksScreen.Load(batch, TracksColumnPreferences(), load));

            // Explicit non-grid message on the Team Analysis tab (like Results empty state)
            ClearAnalysis(AnalysisCellText.Unavailable);

            mainTabControl.SelectedTab = tabPageSkills;
        }

        private void BindRosterMismatch(TeamTrialBatch batch)
        {
            _gridDisplayMode = GridDisplayMode.RosterMismatch;
            ResultsGridHeaders.ApplyCompactHeaderLayout(dataGridView1);
            dataGridView1.ShowCellToolTips = true;
            SetStatus(DropStatusText.RosterMismatch(batch.SkippedFileCount));
            mainTabControl.SelectedTab = tabPageResults;

            ResultsScreen results = ResultsScreen.Load(batch);
            FillResultsGrid(results);

            // Skills shows the stable core. The summary text comes from SkillsScreen.
            _skillsTrialResults = results.StableCore.Count > 0 ? batch.Trials : null;
            ShowSkills(SkillsScreen.Load(batch, SkillsColumnPreferences()));

            // Same stable core as Skills. The summary text comes from TracksScreen.
            _tracksTrialResults = results.StableCore.Count > 0 ? batch.Trials : null;
            ShowTracks(TracksScreen.Load(batch, TracksColumnPreferences()));

            if (results.Outliers != null)
            {
                ResultsGridStyler.ApplyRosterMismatch(
                    dataGridView1,
                    results.Outliers,
                    results.Columns.Count,
                    _aceNameFontCache);
            }

            // Explicit non-grid message on the Team Analysis tab (like Results empty state)
            ClearAnalysis(AnalysisCellText.Unavailable);
        }

        private void BindRosterStats(TeamTrialBatch batch)
        {
            var trials = batch.Trials;
            _gridDisplayMode = GridDisplayMode.RosterStats;
            SetStatus(DropStatusText.RosterLoaded(trials.Count, batch.SkippedFileCount));

            ResultsGridHeaders.ApplyCompactHeaderLayout(dataGridView1);
            dataGridView1.ShowCellToolTips = true;

            FillResultsGrid(ResultsScreen.Load(batch));
            ResultsGridStyler.ApplyRosterStats(dataGridView1, StatsTotalRowIndex, _aceNameFontCache);

            PopulateAnalysis(trials.ToDictionary(kv => kv.Key, kv => kv.Value));
            PopulateSkills(trials.ToDictionary(kv => kv.Key, kv => kv.Value));
            PopulateTracks(trials.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        private void FillResultsGrid(ResultsScreen screen)
        {
            var headers = new List<string>(screen.Columns.Count);
            foreach (ResultsColumn column in screen.Columns)
            {
                headers.Add(column.Header);
            }

            SetGridSize(headers, screen.Rows.Count);
            for (int column = 0; column < screen.Columns.Count; column++)
            {
                if (screen.Columns[column].ToolTip.Length > 0)
                {
                    dataGridView1.Columns[column].ToolTipText = screen.Columns[column].ToolTip;
                }
            }

            for (int row = 0; row < screen.Rows.Count; row++)
            {
                IList<ResultsCell> cells = screen.Rows[row].Cells;
                for (int column = 0; column < cells.Count; column++)
                {
                    object? value = cells[column].Value;
                    if (value != null)
                    {
                        SetCellValue(column, row, value);
                    }
                }
            }
        }
    }
}
