using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.ObjectModel;
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

            // Populate Skills (and Tracks) selector using the extracted identities with differentiation (rank score etc.)
            _skillsTrialResults = null; // not TT
            _skillsRosterEntries.Clear();
            comboBoxSkillsUma.Items.Clear();

            foreach (var id in _currentNonTtIdentities)
            {
                _skillsRosterEntries.Add(new NamedRosterUma(id.TrainedCharaId, id.GetDisplayName()));
                comboBoxSkillsUma.Items.Add(id.GetDisplayName());
            }

            // Also prepare Tracks combo the same way (they share similar roster list in current design)
            _tracksTrialResults = null;
            _tracksRosterEntries.Clear();
            comboBoxTracksUma.Items.Clear();

            foreach (var id in _currentNonTtIdentities)
            {
                _tracksRosterEntries.Add(new NamedRosterUma(id.TrainedCharaId, id.GetDisplayName()));
                comboBoxTracksUma.Items.Add(id.GetDisplayName());
            }

            if (comboBoxSkillsUma.Items.Count > 0)
            {
                comboBoxSkillsUma.SelectedIndex = 0;
                if (comboBoxTracksUma.Items.Count > 0)
                    comboBoxTracksUma.SelectedIndex = 0;

                labelSkillsSummary.Text = $"{_currentNonTtIdentities.Count} local player uma(s) across {singleCount} single-race file(s). Select one to see skill performance.";
            }
            else
            {
                labelSkillsSummary.Text = "No local player umas detected in the loaded non-TT captures.";
            }

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

            if (consistency.StableCore.Count > 0)
            {
                // Enable Skills/Tracks for the stable subset, using the full TT trials for the analyzers.
                _skillsTrialResults = batch.Trials;
                _skillsRosterEntries = consistency.StableCore
                    .Select(uma => new NamedRosterUma(uma.TrainedCharaId, uma.DisplayName))
                    .ToList();
                comboBoxSkillsUma.Items.Clear();
                foreach (NamedRosterUma entry in _skillsRosterEntries)
                    comboBoxSkillsUma.Items.Add(entry.Name);
                if (comboBoxSkillsUma.Items.Count > 0)
                    comboBoxSkillsUma.SelectedIndex = 0;

                _tracksTrialResults = batch.Trials;
                _tracksRosterEntries = new List<NamedRosterUma>(_skillsRosterEntries);
                comboBoxTracksUma.Items.Clear();
                foreach (NamedRosterUma entry in _tracksRosterEntries)
                    comboBoxTracksUma.Items.Add(entry.Name);
                if (comboBoxTracksUma.Items.Count > 0)
                    comboBoxTracksUma.SelectedIndex = 0;

                labelSkillsSummary.Text = $"{consistency.StableCore.Count} consistent uma(s) across all {n} files (same team + style; position within team may vary).";
                labelTracksSummary.Text = $"{consistency.StableCore.Count} consistent uma(s) for track performance.";
            }
            else
            {
                labelSkillsSummary.Text = "No umas are consistent (same team position + style) across all files.";
                labelTracksSummary.Text = "No consistent umas for Tracks.";
            }

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
