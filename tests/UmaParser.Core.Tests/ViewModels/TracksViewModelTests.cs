using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using UmaParser.Settings;
using UmaParser.ViewModels;
using Xunit;

namespace UmaParser.Core.Tests.ViewModels;

/// <summary>
/// <see cref="TracksViewModel"/> is compiled into this project from the WPF sources.
/// The class does not touch a dispatcher or a window, so these tests run with the Core suite.
/// The WPF project targets the Windows desktop runtime, which does not start here.
/// </summary>
public sealed class TracksViewModelTests
{
    private static readonly GridColumnGridPreferences AllColumns = new();

    [Fact]
    public void Apply_fills_the_uma_list_and_keeps_the_total_row_last()
    {
        var viewModel = new TracksViewModel();
        Assert.Equal(TracksViewModel.DropPrompt, viewModel.Summary);
        Assert.Equal(0, viewModel.GridRevision);

        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        TracksScreen screen = TracksScreen.Load(batch, AllColumns);
        viewModel.Apply(batch, AllColumns);

        Assert.Equal(1, viewModel.GridRevision);
        Assert.Equal(screen.Umas.Select(uma => uma.Name), viewModel.Umas.Select(uma => uma.Name));
        Assert.Same(viewModel.Umas[0], viewModel.SelectedUma);
        Assert.Equal(screen.Summary, viewModel.Summary);
        Assert.Equal(screen.GridRows.Count, viewModel.Rows.Count);
        Assert.Equal(screen.GridRows[^1].Track, viewModel.Rows[^1].Track);
        Assert.True(viewModel.Rows[^1].IsFooter);
        Assert.All(viewModel.Rows.Take(viewModel.Rows.Count - 1), row => Assert.False(row.IsFooter));
        Assert.Equal(screen.Columns.Select(column => column.Id), viewModel.VisibleColumns.Select(column => column.Id));
        Assert.Contains(viewModel.VisibleColumns, column => column.Id == GridColumnIds.Tracks.AvgSkillPts);
    }

    [Fact]
    public void Picking_another_uma_replaces_the_grid_and_ignores_a_null_or_repeat()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        var viewModel = new TracksViewModel();
        viewModel.Apply(batch, AllColumns);
        int revision = viewModel.GridRevision;
        string summary = viewModel.Summary;
        int rowCount = viewModel.Rows.Count;
        string firstTrack = viewModel.Rows[0].Track;

        viewModel.SelectedUma = viewModel.SelectedUma;
        viewModel.SelectedUma = null;
        Assert.Equal(revision, viewModel.GridRevision);
        Assert.Equal(summary, viewModel.Summary);
        Assert.Equal(rowCount, viewModel.Rows.Count);
        Assert.Equal(firstTrack, viewModel.Rows[0].Track);
        Assert.Null(viewModel.SelectedUma);

        viewModel.SelectedUma = viewModel.Umas[0];
        Assert.Equal(revision, viewModel.GridRevision);
        Assert.Same(viewModel.Umas[0], viewModel.SelectedUma);

        TracksScreen second = TracksScreen.Load(batch, AllColumns).Select(1);
        viewModel.SelectedUma = viewModel.Umas[1];

        Assert.Equal(revision + 1, viewModel.GridRevision);
        Assert.Equal(second.Summary, viewModel.Summary);
        Assert.Equal(second.GridRows.Select(row => (row.Track, row.Races, row.IsFooter)), viewModel.Rows.Select(row => (row.Track, row.Races, row.IsFooter)));
        Assert.True(viewModel.Rows[^1].IsFooter);
    }

    [Fact]
    public void Mismatch_summary_stays_pinned_until_a_different_uma_is_chosen()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-Mismatch");
        var viewModel = new TracksViewModel();
        viewModel.Apply(batch, AllColumns);

        Assert.Equal(TrackCellText.ConsistentUmas(viewModel.Umas.Count), viewModel.Summary);
        string pinned = viewModel.Summary;
        viewModel.SelectedUma = viewModel.Umas[0];
        Assert.Equal(pinned, viewModel.Summary);

        viewModel.SelectedUma = viewModel.Umas[1];
        Assert.Contains("Team type:", viewModel.Summary);
        Assert.Contains(viewModel.Umas[1].Name, viewModel.Summary);
        Assert.NotEqual(pinned, viewModel.Summary);
    }

    [Fact]
    public void Single_race_files_hide_skill_points_and_a_hidden_column_stays_hidden()
    {
        List<CaptureImportResult> imports = FixtureCatalog.InGroup("Single-CM12")
            .Concat(FixtureCatalog.InGroup("Single-RoomMatch"))
            .Select(FixtureCatalog.Import)
            .ToList();
        TeamTrialBatch batch = TeamTrialBatchBuilder.Build(imports);
        NonTeamTrialLoad load = NonTeamTrialLoad.Collect(batch.SingleRaceImports);
        var viewModel = new TracksViewModel();
        viewModel.Apply(batch, AllColumns, load);

        Assert.DoesNotContain(viewModel.VisibleColumns, column => column.Id == GridColumnIds.Tracks.AvgSkillPts);
        Assert.True(viewModel.Rows[^1].IsFooter);
        Assert.Equal("", viewModel.Rows[^1].AvgSkillPointsText);
        Assert.Equal(CellBand.None, viewModel.Rows[^1].SpurtBand);
        Assert.Contains(viewModel.Umas[0].Name, viewModel.Summary);

        var hidden = new GridColumnGridPreferences { Hidden = [GridColumnIds.Tracks.Distance] };
        viewModel.Apply(TrialSets.Build("TT-K0"), hidden);
        Assert.DoesNotContain(viewModel.VisibleColumns, column => column.Id == GridColumnIds.Tracks.Distance);
        Assert.Equal(GridColumnIds.Tracks.Track, viewModel.VisibleColumns[0].Id);
        Assert.Contains(viewModel.VisibleColumns, column => column.Id == GridColumnIds.Tracks.AvgSkillPts);
    }

    [Fact]
    public void An_empty_batch_clears_the_prompt()
    {
        var viewModel = new TracksViewModel();
        viewModel.Apply(new TeamTrialBatch { Kind = TeamTrialBatchKind.Empty }, AllColumns);

        Assert.Equal("", viewModel.Summary);
        Assert.Empty(viewModel.Umas);
        Assert.Null(viewModel.SelectedUma);
        Assert.Empty(viewModel.Rows);
        Assert.Equal(1, viewModel.GridRevision);
    }
}
