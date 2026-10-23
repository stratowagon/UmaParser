using System.Collections.ObjectModel;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using UmaParser.ViewModels;
using Xunit;

namespace UmaParser.Core.Tests.ViewModels;

/// <summary>
/// <see cref="ResultsViewModel"/> is compiled into this project from the WPF sources.
/// It does not touch a dispatcher or a window, so these tests run with the Core suite.
/// </summary>
public sealed class ResultsViewModelTests
{
    [Fact]
    public void Apply_fills_a_uniform_roster_and_a_later_mismatch_replaces_it()
    {
        var viewModel = new ResultsViewModel();
        Assert.False(viewModel.ShowGrid);
        Assert.Equal(ResultsScreen.Welcome, viewModel.Message);
        Assert.Equal(0, viewModel.GridRevision);

        TeamTrialBatch uniform = TrialSets.Build("TT-K0");
        ResultsScreen uniformScreen = ResultsScreen.Load(uniform);
        viewModel.Apply(uniform);

        Assert.Equal(1, viewModel.GridRevision);
        Assert.True(viewModel.ShowGrid);
        Assert.Equal(uniformScreen.Columns.Select(column => column.Header), viewModel.Columns.Select(column => column.Header));
        Assert.Equal(uniformScreen.Rows.Count, viewModel.Rows.Count);
        Assert.True(viewModel.Rows[^1].IsFooter);
        Assert.Equal("Total", viewModel.Rows[^1].Cells[0].Text);

        TeamTrialBatch mismatch = TrialSets.Build("TT-Mismatch");
        ResultsScreen mismatchScreen = ResultsScreen.Load(mismatch);
        viewModel.Apply(mismatch);

        Assert.Equal(2, viewModel.GridRevision);
        Assert.True(viewModel.ShowGrid);
        Assert.Equal(mismatchScreen.Rows.Count, viewModel.Rows.Count);
        Assert.DoesNotContain(viewModel.Rows, row => row.IsFooter);
        Assert.Equal(
            mismatchScreen.Rows[0].Cells.Select(cell => cell.Outlier),
            viewModel.Rows[0].Cells.Select(cell => cell.Outlier));
    }

    [Fact]
    public void A_single_race_drop_hides_the_grid_and_keeps_the_welcome_text()
    {
        var viewModel = new ResultsViewModel();
        viewModel.Apply(TrialSets.Build("TT-K0"));
        viewModel.Apply(TrialSets.Build("Single-CM12"));

        Assert.False(viewModel.ShowGrid);
        Assert.Empty(viewModel.Rows);
        Assert.Empty(viewModel.Columns);
        Assert.Equal(ResultsScreen.Welcome, viewModel.Message);
        Assert.Equal(2, viewModel.GridRevision);
    }

    [Fact]
    public void RequestRepaint_does_not_replace_the_loaded_grid()
    {
        var viewModel = new ResultsViewModel();
        viewModel.Apply(TrialSets.Build("TT-K0"));
        int revision = viewModel.GridRevision;
        ObservableCollection<ResultsGridRow> rows = viewModel.Rows;

        viewModel.RequestRepaint();

        Assert.Equal(revision, viewModel.GridRevision);
        Assert.Same(rows, viewModel.Rows);
        Assert.Equal(1, viewModel.RepaintRequest);
        Assert.True(viewModel.ShowGrid);
    }

    [Fact]
    public void ApplyTheme_recolors_heat_without_replacing_the_grid()
    {
        var viewModel = new ResultsViewModel();
        viewModel.Apply(TrialSets.Build("TT-K0"));
        int revision = viewModel.GridRevision;
        ObservableCollection<ResultsGridRow> rows = viewModel.Rows;
        var scores = rows
            .Where(row => !row.IsFooter)
            .SelectMany(row => row.Cells)
            .Select(cell => cell.Value)
            .OfType<int>();
        Assert.True(ScoreHeat.TryGetRange(scores, out double min, out double max));
        int score = Assert.IsType<int>(rows[0].Cells[1].Value);

        viewModel.ApplyTheme(ColorTheme.Dark);

        Assert.Equal(revision, viewModel.GridRevision);
        Assert.Same(rows, viewModel.Rows);
        Assert.Equal(ScoreHeat.ColorFor(score, min, max, ColorTheme.Dark), rows[0].Cells[1].Heat);
        Assert.Equal(1, viewModel.RepaintRequest);

        viewModel.ApplyTheme(ColorTheme.Dark);
        Assert.Equal(1, viewModel.RepaintRequest);
    }
}
