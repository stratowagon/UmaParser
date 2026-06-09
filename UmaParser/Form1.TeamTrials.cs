using System.Text.Json;
using UmaBlobber.Import;
using UmaBlobber.ObjectModel;

namespace UmaBlobber
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

            _currentSingleRaces.Clear();
            _currentNonTtIdentities.Clear();

            // Quick sanity + data collection
            int totalHorsesAcrossRaces = 0;
            int successfullyParsedSims = 0;

            foreach (var sr in batch.SingleRaceImports)
            {
                if (!string.IsNullOrWhiteSpace(sr.SingleRaceSimDataBase64))
                {
                    try
                    {
                        var sim = UmaBlobber.DataModel.RaceScenario.RaceScenarioParser.Parse(sr.SingleRaceSimDataBase64);
                        totalHorsesAcrossRaces += sim.HorseNum;
                        successfullyParsedSims++;
                    }
                    catch { /* ignore bad blobs for status */ }
                }

                if (!string.IsNullOrWhiteSpace(sr.SingleRaceNormalizedJson))
                {
                    try
                    {
                        var parts = SingleRaceParticipantExtractor.ExtractLocalPlayerParticipants(
                            sr.SingleRaceNormalizedJson, sr.SingleRaceSimDataBase64);

                        _currentSingleRaces.Add(parts);

                        foreach (var (id, _) in parts.LocalPlayerHorses)
                        {
                            if (!_currentNonTtIdentities.Any(existing => existing.IsSameUma(id)))
                                _currentNonTtIdentities.Add(id);
                        }
                    }
                    catch { /* best effort */ }
                }
            }

            if (singleCount > 0)
                msg += $". Parsed {successfullyParsedSims}/{singleCount} sim blobs ({totalHorsesAcrossRaces} total horse slots). Found {_currentNonTtIdentities.Count} unique local player uma(s).";

            if (ttCount > 0 && singleCount > 0)
            {
                msg += "  (Note: Mixing Team Trials with CM/Room/Practice is not recommended — the two use cases have very different team structures and the app treats them separately.)";
            }

            SetStatus(msg + "  Results + Analysis are for uniform TT rosters only. Skills/Tracks support non-uniform data.");

            // Populate Skills (and Tracks) selector using the extracted identities with differentiation (rank score etc.)
            _skillsTrialResults = null; // not TT
            _skillsRosterEntries.Clear();
            comboBoxSkillsUma.Items.Clear();

            var ordered = _currentNonTtIdentities
                .OrderBy(i => i.ShortName)
                .ThenByDescending(i => i.RankScore)
                .ToList();

            _currentNonTtIdentities = ordered;

            foreach (var id in _currentNonTtIdentities)
            {
                _skillsRosterEntries.Add((id.TrainedCharaId, id.GetDisplayName()));
                comboBoxSkillsUma.Items.Add(id.GetDisplayName());
            }

            // Also prepare Tracks combo the same way (they share similar roster list in current design)
            _tracksTrialResults = null;
            _tracksRosterEntries.Clear();
            comboBoxTracksUma.Items.Clear();

            foreach (var id in _currentNonTtIdentities)
            {
                _tracksRosterEntries.Add((id.TrainedCharaId, id.GetDisplayName()));
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
            SetStatus(BuildImportStatusMessage(
                "Roster mismatch — compare columns to find outlier files. Analysis skipped.",
                batch.SkippedFileCount));
            mainTabControl.SelectedTab = tabPageResults;

            var fileEntries = trials.ToList(); // ordered list for column indices
            int n = fileEntries.Count;
            SetGridSize(n, 15);

            // Compute majority roster by trained_chara_id (more accurate than names for distinguishing veterans)
            var rosterGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < n; i++)
            {
                var ids = fileEntries[i].Value.RosterTrainedCharaIds;
                string key = string.Join(",", ids);
                if (!rosterGroups.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    rosterGroups[key] = list;
                }
                list.Add(i);
            }

            // Largest group is the "majority" roster
            var majorityEntry = rosterGroups.OrderByDescending(g => g.Value.Count).First();
            string majKey = majorityEntry.Key;
            var majFileIndices = majorityEntry.Value;

            var majTrial = fileEntries[majFileIndices[0]].Value;
            var majIds = majTrial.RosterTrainedCharaIds;
            var refStyles = majTrial.RosterRunningStyles; // ints from capture

            // Mark outlier cells ONLY in minority files (files whose id roster differs from majority)
            // A cell is an outlier if:
            // - different veteran (trained_chara_id differs), or
            // - same veteran but different running style than the reference style from the majority group.
            //
            // Note: A highlighted cell may still display the same short name as the majority in these cases:
            // 1. Change in running style for this veteran
            // 2. A different veteran of the same base character (different trained_chara_id, same chara_id)
            // 3. A different variant of the character (different card_id, still different trained_chara_id)
            _mismatchOutliers = new bool[n, 15];
            _mismatchFileCount = n;

            for (int fi = 0; fi < n; fi++)
            {
                var trial = fileEntries[fi].Value;
                var fIds = trial.RosterTrainedCharaIds;
                var fStyles = trial.RosterRunningStyles;

                string thisKey = string.Join(",", fIds);
                if (thisKey == majKey)
                    continue; // no highlights for files in the majority group

                for (int p = 0; p < 15; p++)
                {
                    bool idDiff = fIds[p] != majIds[p];
                    bool styleDiff = !idDiff && fStyles[p] != refStyles[p];
                    _mismatchOutliers[fi, p] = idDiff || styleDiff;
                }
            }

            // Compute stable core for Skills/Tracks in this mismatch TT batch:
            // only umas that are present in EVERY file, in the exact same position (team),
            // with the exact same running style.
            // This allows useful analysis of long-term consistent umas without full roster uniformity.
            var stableCore = new List<(int TrainedCharaId, string DisplayName)>();
            if (n > 0)
            {
                var firstIds = fileEntries[0].Value.RosterTrainedCharaIds;
                var firstNames = fileEntries[0].Value.RosterNames;
                var firstStyles = fileEntries[0].Value.RosterRunningStyles;
                for (int p = 0; p < 15; p++)
                {
                    int refId = firstIds[p];
                    int refStyle = firstStyles[p];
                    bool consistent = true;
                    for (int fi = 1; fi < n && consistent; fi++)
                    {
                        var ids = fileEntries[fi].Value.RosterTrainedCharaIds;
                        var styles = fileEntries[fi].Value.RosterRunningStyles;
                        if (ids[p] != refId || styles[p] != refStyle)
                            consistent = false;
                    }
                    if (consistent)
                    {
                        string team = GetTeamNameForPosition(p);
                        string styleStr = FormatRunningStyle(refStyle);
                        string display = $"{team}: {firstNames[p]} ({styleStr})";
                        stableCore.Add((refId, display));
                    }
                }
            }

            if (stableCore.Count > 0)
            {
                // Enable Skills/Tracks for the stable subset, using the full TT trials for the analyzers.
                _skillsTrialResults = batch.Trials;
                _skillsRosterEntries = new List<(int, string)>(stableCore);
                comboBoxSkillsUma.Items.Clear();
                foreach (var (_, display) in stableCore)
                    comboBoxSkillsUma.Items.Add(display);
                if (comboBoxSkillsUma.Items.Count > 0)
                    comboBoxSkillsUma.SelectedIndex = 0;

                _tracksTrialResults = batch.Trials;
                _tracksRosterEntries = new List<(int, string)>(stableCore);
                comboBoxTracksUma.Items.Clear();
                foreach (var (_, display) in stableCore)
                    comboBoxTracksUma.Items.Add(display);
                if (comboBoxTracksUma.Items.Count > 0)
                    comboBoxTracksUma.SelectedIndex = 0;

                labelSkillsSummary.Text = $"{stableCore.Count} consistent uma(s) across all {n} files (same position + style).";
                labelTracksSummary.Text = $"{stableCore.Count} consistent uma(s) for track performance.";
            }
            else
            {
                labelSkillsSummary.Text = "No umas are consistent (same team position + style) across all files.";
                labelTracksSummary.Text = "No consistent umas for Tracks.";
            }

            // Populate the grid (names only; highlighting applied in CellFormatting)
            for (int c = 0; c < n; c++)
            {
                var (fileName, trial) = fileEntries[c];
                dataGridView1.Columns[c].HeaderText = fileName;
                for (int row = 0; row < trial.RosterNames.Count; row++)
                {
                    SetCellValue(c, row, trial.RosterNames[row]);
                }
            }

            // Explicit non-grid message on the Team Analysis tab (like Results empty state)
            ClearAnalysis("Team Analysis is only active for Team Trials files that all have the exact same roster of 15 umas.");
        }

        private string GetTeamNameForPosition(int position)
        {
            // 0-2 Sprint, 3-5 Mile, 6-8 Medium, 9-11 Long, 12-14 Dirt
            return (position / 3) switch
            {
                0 => "Sprint",
                1 => "Mile",
                2 => "Medium",
                3 => "Long",
                4 => "Dirt",
                _ => "?"
            };
        }

        private string FormatRunningStyle(int style)
        {
            return style switch
            {
                1 => "Front",
                2 => "Pace",
                3 => "Late",
                4 => "End",
                _ => style.ToString()
            };
        }

        private void BindRosterStats(TeamTrialBatch batch)
        {
            var trials = batch.Trials;
            _gridDisplayMode = GridDisplayMode.RosterStats;
            _mismatchOutliers = null;
            _mismatchFileCount = 0;
            SetStatus(BuildImportStatusMessage(
                $"{trials.Count} team trial file(s) loaded.",
                batch.SkippedFileCount));

            var columnNames = new List<string> { "Name" };
            for (int i = 0; i < trials.Count; i++)
            {
                columnNames.Add((i + 1).ToString());
            }

            SetGridSize(columnNames, 16);

            var names = trials.First().Value.RosterNames;
            for (int i = 0; i < names.Count; i++)
            {
                SetCellValue(0, i, names[i]);
            }

            SetCellValue(0, 15, "Total");

            int scoreColumn = 1;
            foreach (var trial in trials.Values)
            {
                var scores = trial.RosterScores;
                for (int i = 0; i < scores.Count; i++)
                {
                    SetCellValue(scoreColumn, i, scores[i]);
                }

                SetCellValue(scoreColumn, 15, trial.TotalScore);
                scoreColumn++;
            }

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