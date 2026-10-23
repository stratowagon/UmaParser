using System.Diagnostics;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Presentation;
using UmaParser.ViewModels;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

/// <summary>
/// The Results grid is one column per file. This builds that screen for a few hundred
/// copies of one scrubbed trial. It does not import 400 JSON files and it does not
/// create a WPF visual tree (that needs Windows). The budget is the screen model only.
/// </summary>
public sealed class ResultsScreenScaleTests
{
    private const int FileCount = 400;

    private readonly ITestOutputHelper _output;

    public ResultsScreenScaleTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// A Release run of this model on the 400-file copy was about 20 ms for
    /// <see cref="ResultsScreen.Load"/> and about 20 ms for <see cref="ResultsViewModel.Apply"/>.
    /// The budget is headroom for a cold JIT. A multi-second result means the screen build regressed.
    /// The WPF grid itself is timed with Trace in ResultsView, which this project cannot host.
    /// </summary>
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(400);

    [Fact]
    public void A_roster_of_400_files_builds_the_results_screen_inside_the_budget()
    {
        FixtureEntry entry = FixtureCatalog.InGroup("TT-K0").First();
        TeamTrialResult trial = Assert.IsType<TeamTrialResult>(FixtureCatalog.Import(entry).Response);

        var trials = new Dictionary<string, TeamTrialResult>(FileCount);
        for (int i = 0; i < FileCount; i++)
        {
            trials.Add($"TT-20261019_{i:000000}_000.json", trial);
        }

        var batch = new TeamTrialBatch
        {
            Kind = TeamTrialBatchKind.RosterStats,
            Trials = trials,
        };

        long started = Stopwatch.GetTimestamp();
        ResultsScreen screen = ResultsScreen.Load(batch);
        TimeSpan screenTime = Stopwatch.GetElapsedTime(started);

        Assert.Equal(ResultsGridKind.RosterStats, screen.Kind);
        Assert.Equal(FileCount + 1, screen.Columns.Count);
        Assert.Equal(RosterGridLayout.TotalRowIndex + 1, screen.Rows.Count);
        Assert.Equal(FileCount + 1, screen.Rows[0].Cells.Count);
        Assert.Null(screen.Rows[0].Cells[0].Heat);
        Assert.NotNull(screen.Rows[0].Cells[1].Heat);
        Assert.Null(screen.Rows[^1].Cells[1].Heat);
        Assert.True(screen.Rows[^1].IsFooter);
        _output.WriteLine($"ResultsScreen.Load {FileCount} files: {screenTime.TotalMilliseconds:0.0} ms");
        Assert.True(
            screenTime < Budget,
            $"ResultsScreen.Load for {FileCount} files took {screenTime.TotalMilliseconds:0.0} ms (budget {Budget.TotalMilliseconds:0} ms).");

        var viewModel = new ResultsViewModel();
        started = Stopwatch.GetTimestamp();
        viewModel.Apply(batch);
        TimeSpan viewModelTime = Stopwatch.GetElapsedTime(started);

        Assert.Equal(FileCount + 1, viewModel.Columns.Count);
        Assert.Equal(screen.Rows.Count, viewModel.Rows.Count);
        Assert.Equal(1, viewModel.GridRevision);
        _output.WriteLine($"ResultsViewModel.Apply {FileCount} files: {viewModelTime.TotalMilliseconds:0.0} ms");
        Assert.True(
            viewModelTime < Budget,
            $"ResultsViewModel.Apply for {FileCount} files took {viewModelTime.TotalMilliseconds:0.0} ms (budget {Budget.TotalMilliseconds:0} ms).");
    }
}
