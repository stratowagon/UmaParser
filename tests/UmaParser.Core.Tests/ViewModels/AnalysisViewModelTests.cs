using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using UmaParser.Settings;
using UmaParser.ViewModels;
using Xunit;

namespace UmaParser.Core.Tests.ViewModels;

/// <summary>
/// <see cref="AnalysisViewModel"/> is compiled into this project from the WPF sources.
/// It does not touch a dispatcher or a window, so these tests run with the Core suite.
/// </summary>
public sealed class AnalysisViewModelTests
{
    private static readonly GridColumnGridPreferences AllColumns = new();

    [Fact]
    public void Apply_fills_the_grid_and_the_status_summary_for_a_uniform_roster()
    {
        var viewModel = new AnalysisViewModel();
        Assert.True(viewModel.ShowGrid);
        Assert.Equal("", viewModel.EmptyMessage);
        Assert.Equal("", viewModel.Summary);
        Assert.Equal(0, viewModel.GridRevision);

        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        AnalysisScreen screen = AnalysisScreen.Load(batch, AllColumns);
        viewModel.Apply(batch, AllColumns);

        Assert.Equal(1, viewModel.GridRevision);
        Assert.True(viewModel.ShowGrid);
        Assert.Equal("", viewModel.EmptyMessage);
        Assert.Equal(screen.Summary, viewModel.Summary);
        Assert.Contains("base scores", viewModel.Summary);
        Assert.Equal(screen.Rows.Select(row => (row.Uma, row.Avg, row.RetrainBand, row.IsAce)), viewModel.Rows.Select(row => (row.Uma, row.Avg, row.RetrainBand, row.IsAce)));
        Assert.Equal(screen.Columns.Select(column => column.Id), viewModel.VisibleColumns.Select(column => column.Id));
    }

    [Fact]
    public void A_hidden_column_stays_hidden_and_a_mismatch_clears_the_grid()
    {
        var hidden = new GridColumnGridPreferences { Hidden = [GridColumnIds.Analysis.Gap] };
        var viewModel = new AnalysisViewModel();
        viewModel.Apply(TrialSets.Build("TT-K0"), hidden);

        Assert.DoesNotContain(viewModel.VisibleColumns, column => column.Id == GridColumnIds.Analysis.Gap);
        Assert.Equal(GridColumnIds.Analysis.Uma, viewModel.VisibleColumns[0].Id);
        Assert.Contains(viewModel.VisibleColumns, column => column.Id == GridColumnIds.Analysis.AceDelta);

        viewModel.Apply(TrialSets.Build("TT-Mismatch"), AllColumns);
        Assert.False(viewModel.ShowGrid);
        Assert.Equal(AnalysisCellText.Unavailable, viewModel.EmptyMessage);
        Assert.Equal("", viewModel.Summary);
        Assert.Empty(viewModel.Rows);
        Assert.Empty(viewModel.VisibleColumns);

        viewModel.Apply(TrialSets.Build("Single-RoomMatch"), AllColumns);
        Assert.False(viewModel.ShowGrid);
        Assert.Equal(AnalysisCellText.Unavailable, viewModel.EmptyMessage);
    }
}
