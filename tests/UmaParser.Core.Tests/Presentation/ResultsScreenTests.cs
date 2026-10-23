using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class ResultsScreenTests
{
    [Fact]
    public void A_uniform_roster_is_names_then_file_scores_then_a_total_row()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        RosterStatsTable table = RosterStatsTableBuilder.Build(batch);
        ResultsScreen screen = ResultsScreen.Load(batch);

        Assert.Equal(ResultsGridKind.RosterStats, screen.Kind);
        Assert.True(screen.ShowGrid);
        Assert.Null(screen.Outliers);
        Assert.Equal("Name", screen.Columns[0].Header);
        Assert.Equal("", screen.Columns[0].ToolTip);
        Assert.Equal(table.FileNames.Select(ResultsFileHeader.FormatCompact), screen.Columns.Skip(1).Select(column => column.Header));
        Assert.Equal(table.FileNames, screen.Columns.Skip(1).Select(column => column.ToolTip));

        Assert.Equal(RosterGridLayout.TotalRowIndex + 1, screen.Rows.Count);
        Assert.All(screen.Rows.Take(RosterGridLayout.RosterRowCount), row => Assert.False(row.IsFooter));
        ResultsGridRow total = screen.Rows[^1];
        Assert.True(total.IsFooter);
        Assert.True(total.IsTeamBoundary);
        Assert.Equal("Total", total.Cells[0].Text);
        Assert.Equal("Total", total.Cells[0].Value);
        Assert.Equal(table.Totals.Select(score => score.ToString()), total.Cells.Skip(1).Select(cell => cell.Text));
        Assert.All(total.Cells, cell => Assert.Null(cell.Heat));

        bool heatOn = ScoreHeat.TryGetRange(
            Enumerable.Range(0, table.Names.Count).SelectMany(row =>
                Enumerable.Range(0, table.FileNames.Count).Select(file => table.Scores[row, file])),
            out double min,
            out double max);
        Assert.True(heatOn);

        for (int row = 0; row < table.Names.Count; row++)
        {
            ResultsGridRow gridRow = screen.Rows[row];
            bool ace = RosterGridLayout.IsAceRow(row);
            Assert.Equal(ace, gridRow.Cells[0].Bold);
            Assert.Equal(table.Names[row], gridRow.Cells[0].Text);
            Assert.Equal(table.Names[row], gridRow.Cells[0].Value);
            Assert.Null(gridRow.Cells[0].Heat);
            Assert.Equal(RosterGridLayout.IsDefaultTeamBoundary(row), gridRow.IsTeamBoundary);
            Assert.All(gridRow.Cells.Skip(1), cell => Assert.False(cell.Bold));

            for (int file = 0; file < table.FileNames.Count; file++)
            {
                int score = table.Scores[row, file];
                ResultsCell cell = gridRow.Cells[file + 1];
                Assert.Equal(score, cell.Value);
                Assert.Equal(score.ToString(), cell.Text);
                Assert.False(cell.Outlier);
                Assert.Equal(ScoreHeat.ColorFor(score, min, max), cell.Heat);
            }
        }
    }

    [Fact]
    public void A_mismatch_shows_each_file_roster_and_marks_outliers()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-Mismatch");
        var trials = batch.Trials.Select(file => file.Value).ToList();
        RosterConsistencyReport consistency = RosterConsistencyAnalyzer.Analyze(trials);
        ResultsScreen screen = ResultsScreen.Load(batch);

        Assert.Equal(ResultsGridKind.RosterMismatch, screen.Kind);
        Assert.Equal(consistency.StableCore, screen.StableCore);
        Assert.Equal(batch.Trials.Count, screen.Columns.Count);
        Assert.Equal(RosterGridLayout.RosterRowCount, screen.Rows.Count);
        Assert.DoesNotContain(screen.Rows, row => row.IsFooter);
        bool[,] outliers = Assert.IsType<bool[,]>(screen.Outliers);
        Assert.Equal(consistency.Outliers.GetLength(0), outliers.GetLength(0));
        Assert.Equal(consistency.Outliers.GetLength(1), outliers.GetLength(1));

        var files = batch.Trials.ToList();
        for (int file = 0; file < files.Count; file++)
        {
            Assert.Equal(ResultsFileHeader.FormatCompact(files[file].Key), screen.Columns[file].Header);
            Assert.Equal(files[file].Key, screen.Columns[file].ToolTip);
            for (int row = 0; row < RosterGridLayout.RosterRowCount; row++)
            {
                ResultsCell cell = screen.Rows[row].Cells[file];
                string name = files[file].Value.RosterNames[row];
                Assert.Equal(name, cell.Text);
                Assert.Equal(name, cell.Value);
                Assert.Equal(consistency.Outliers[file, row], cell.Outlier);
                Assert.Equal(RosterGridLayout.IsAceRow(row), cell.Bold);
                Assert.Null(cell.Heat);
            }
        }
    }

    [Fact]
    public void Single_race_and_mixed_drops_leave_the_welcome_text_and_no_grid()
    {
        ResultsScreen single = ResultsScreen.Load(TrialSets.Build("Single-CM12"));
        Assert.Equal(ResultsGridKind.None, single.Kind);
        Assert.False(single.ShowGrid);
        Assert.Empty(single.Rows);

        TeamTrialBatch team = TrialSets.Build("TT-K0");
        TeamTrialBatch races = TrialSets.Build("Single-CM12");
        ResultsScreen mixed = ResultsScreen.Load(new TeamTrialBatch
        {
            Kind = TeamTrialBatchKind.MixedOrNonTT,
            Trials = team.Trials,
            SingleRaceImports = races.SingleRaceImports,
        });
        Assert.False(mixed.ShowGrid);
        Assert.Contains("Team Analysis tab is only available", ResultsScreen.Welcome);
    }
}
