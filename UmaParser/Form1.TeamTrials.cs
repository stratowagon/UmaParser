using UmaBlobber.Import;
using UmaBlobber.ObjectModel;

namespace UmaBlobber
{
    public partial class Form1
    {
        private void BindTeamTrialBatch(TeamTrialBatch batch)
        {
            if (batch.Kind == TeamTrialBatchKind.Empty)
            {
                return;
            }

            ClearGrid();
            ClearAnalysis();
            ClearSkills();

            switch (batch.Kind)
            {
                case TeamTrialBatchKind.RosterMismatch:
                    BindRosterMismatch(batch);
                    break;
                case TeamTrialBatchKind.RosterStats:
                    BindRosterStats(batch);
                    break;
            }

            FinalizeGridDisplay();
        }

        private void BindRosterMismatch(TeamTrialBatch batch)
        {
            var trials = batch.Trials;
            _gridDisplayMode = GridDisplayMode.RosterMismatch;
            SetStatus(BuildImportStatusMessage(
                "Roster mismatch — compare columns to find outlier files. Analysis skipped.",
                batch.SkippedFileCount));
            mainTabControl.SelectedTab = tabPageResults;
            SetGridSize(trials.Count, 15);

            int column = 0;
            foreach (var (fileName, trial) in trials)
            {
                dataGridView1.Columns[column].HeaderText = fileName;
                for (int row = 0; row < trial.RosterNames.Count; row++)
                {
                    SetCellValue(column, row, trial.RosterNames[row]);
                }

                column++;
            }
        }

        private void BindRosterStats(TeamTrialBatch batch)
        {
            var trials = batch.Trials;
            _gridDisplayMode = GridDisplayMode.RosterStats;
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
        }

        private static string BuildImportStatusMessage(string primary, int skipped)
        {
            return skipped <= 0 ? primary : $"{primary} ({skipped} other file(s) skipped.)";
        }
    }
}