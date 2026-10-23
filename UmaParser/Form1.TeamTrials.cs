using UmaParser.Analysis;
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
            int ttCount = batch.Trials.Count;
            int singleCount = batch.SingleRaceImports.Count;
            string what = singleCount > 0 ? "single-race capture(s)" : "races";
            string msg = $"{ttCount + singleCount} {what} loaded";
            if (ttCount > 0) msg += $" (incl. {ttCount} TT)";
            if (singleCount > 0) msg += $" (incl. {singleCount} CM/Room/Practice)";
            if (batch.SkippedFileCount > 0) msg += $" ({batch.SkippedFileCount} other file(s) skipped)";

            NonTeamTrialLoad load = NonTeamTrialLoad.Collect(batch.SingleRaceImports);
            _currentSingleRaces = load.Races.ToList();
            _currentNonTtIdentities = load.Identities.ToList();

            if (singleCount > 0)
                msg += $". Parsed {load.ParsedSimulationCount}/{singleCount} sim blobs ({load.TotalHorseSlots} total horse slots). Found {_currentNonTtIdentities.Count} unique local player uma(s).";

            if (ttCount > 0 && singleCount > 0)
            {
                msg += "  (Note: Mixing Team Trials with CM/Room/Practice is not recommended — the two use cases have very different team structures and the app treats them separately.)";
            }

            SetStatus(msg + "  Results + Analysis are for uniform TT rosters only. Skills/Tracks support non-uniform data.");

            // Skills and Tracks share this load so the sim blobs are not parsed a second time.
            // Both screens are the same models the WPF window displays.
            _skillsTrialResults = null; // not TT
            ShowSkills(SkillsScreen.Load(batch, SkillsColumnPreferences(), load));

            _tracksTrialResults = null;
            ShowTracks(TracksScreen.Load(batch, TracksColumnPreferences(), load));

            // Explicit non-grid message on the Team Analysis tab (like Results empty state)
            ClearAnalysis("Team Analysis is only active for Team Trials files that all have the exact same roster of 15 umas.");

            mainTabControl.SelectedTab = tabPageSkills;
        }

        private void BindRosterMismatch(TeamTrialBatch batch)
        {
            var trials = batch.Trials;
            _gridDisplayMode = GridDisplayMode.RosterMismatch;
            ResultsGridHeaders.ApplyCompactHeaderLayout(dataGridView1);
            dataGridView1.ShowCellToolTips = true;
            SetStatus(BuildImportStatusMessage(
                "Roster mismatch — compare columns to find outlier files. Analysis skipped.",
                batch.SkippedFileCount));
            mainTabControl.SelectedTab = tabPageResults;

            var fileEntries = trials.ToList(); // ordered list for column indices
            int n = fileEntries.Count;
            SetGridSize(n, RosterConsistencyAnalyzer.SlotCount);

            // Same file order as the columns above. Outliers[file, slot] lines up with that.
            RosterConsistencyReport consistency = RosterConsistencyAnalyzer.Analyze(
                fileEntries.Select(entry => entry.Value).ToList());

            // Skills shows the stable core. The summary text comes from SkillsScreen.
            _skillsTrialResults = consistency.StableCore.Count > 0 ? batch.Trials : null;
            ShowSkills(SkillsScreen.Load(batch, SkillsColumnPreferences()));

            // Same stable core as Skills. The summary text comes from TracksScreen.
            _tracksTrialResults = consistency.StableCore.Count > 0 ? batch.Trials : null;
            ShowTracks(TracksScreen.Load(batch, TracksColumnPreferences()));

            for (int c = 0; c < n; c++)
            {
                var (fileName, trial) = fileEntries[c];
                dataGridView1.Columns[c].HeaderText = ResultsGridHeaders.FormatCompact(fileName);
                dataGridView1.Columns[c].ToolTipText = fileName;
                for (int row = 0; row < trial.RosterNames.Count; row++)
                {
                    SetCellValue(c, row, trial.RosterNames[row]);
                }
            }

            ResultsGridStyler.ApplyRosterMismatch(dataGridView1, consistency.Outliers, n, _aceNameFontCache);

            // Explicit non-grid message on the Team Analysis tab (like Results empty state)
            ClearAnalysis("Team Analysis is only active for Team Trials files that all have the exact same roster of 15 umas.");
        }

        private void BindRosterStats(TeamTrialBatch batch)
        {
            var trials = batch.Trials;
            var fileEntries = trials.ToList();
            _gridDisplayMode = GridDisplayMode.RosterStats;
            SetStatus(BuildImportStatusMessage(
                $"{trials.Count} team trial file(s) loaded.",
                batch.SkippedFileCount));

            ResultsGridHeaders.ApplyCompactHeaderLayout(dataGridView1);
            dataGridView1.ShowCellToolTips = true;

            RosterStatsTable table = RosterStatsTableBuilder.Build(fileEntries);

            var columnNames = new List<string> { "Name" };
            columnNames.AddRange(table.FileNames.Select(ResultsGridHeaders.FormatCompact));

            SetGridSize(columnNames, StatsTotalRowIndex + 1);

            for (int i = 0; i < table.FileNames.Count; i++)
            {
                dataGridView1.Columns[i + 1].ToolTipText = table.FileNames[i];
            }

            for (int i = 0; i < table.Names.Count; i++)
            {
                SetCellValue(0, i, table.Names[i]);
            }

            SetCellValue(0, StatsTotalRowIndex, "Total");

            for (int fileIndex = 0; fileIndex < table.FileNames.Count; fileIndex++)
            {
                int scoreColumn = fileIndex + 1;
                for (int row = 0; row < table.TrainedCharaIds.Count; row++)
                {
                    SetCellValue(scoreColumn, row, table.Scores[row, fileIndex]);
                }

                SetCellValue(scoreColumn, StatsTotalRowIndex, table.Totals[fileIndex]);
            }

            ResultsGridStyler.ApplyRosterStats(dataGridView1, StatsTotalRowIndex, _aceNameFontCache);

            PopulateAnalysis(trials.ToDictionary(kv => kv.Key, kv => kv.Value));
            PopulateSkills(trials.ToDictionary(kv => kv.Key, kv => kv.Value));
            PopulateTracks(trials.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        private static string BuildImportStatusMessage(string primary, int skipped)
        {
            return skipped <= 0 ? primary : $"{primary} ({skipped} other file(s) skipped.)";
        }
    }
}
