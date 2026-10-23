using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using UmaParser.Presentation;
using UmaParser.Settings;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

public sealed class AnalysisScreenTests
{
    private static readonly GridColumnGridPreferences AllColumns = new();

    [Fact]
    public void A_uniform_roster_keeps_roster_order_formats_and_cell_bands()
    {
        TeamTrialBatch batch = TrialSets.Build("TT-K0");
        AnalysisScreen screen = AnalysisScreen.Load(batch, AllColumns);
        UmaAnalysisReport report = UmaAnalysisEngine.Analyze(batch.Trials.Values.ToList());

        Assert.True(screen.ShowGrid);
        Assert.Equal("", screen.EmptyMessage);
        Assert.Equal(AnalysisCellText.Summary(report), screen.Summary);
        Assert.StartsWith(
            $"Analysis uses base scores (all bonuses removed) across {report.TrialCount} trial(s). Team avg (raw): {report.TeamAverage:N0}.",
            screen.Summary);
        Assert.Equal(
            [
                "Uma", "Style", "Avg", "Skills", "Skill CV", "Skill Ratio", "Other", "Other CV",
                "Avg Place", "Trimmed", "CV", "Adj CV", "Gap", "Ceiling", "Floor", "Spread",
                "Retrain", "Ace delta",
            ],
            screen.Columns.Select(column => column.Header));
        Assert.Equal(report.Rows.Count, screen.Rows.Count);

        for (int i = 0; i < report.Rows.Count; i++)
        {
            UmaAnalysisRow source = report.Rows[i];
            AnalysisGridRow row = screen.Rows[i];
            Assert.Equal(RosterGridLayout.IsAceRow(i), row.IsAce);
            Assert.Equal(source.IsCurrentAce, row.IsAce);
            Assert.Equal(RosterGridLayout.IsDefaultTeamBoundary(i), row.IsTeamBoundary);
            Assert.Equal(source.Name, row.Uma);
            Assert.Equal(source.RunningStyleLabel, row.Style);
            Assert.Equal(source.Average.ToString(), row.Avg);
            Assert.Equal(source.AveragePlace.ToString("0.00"), row.AvgPlace);
            Assert.Equal(source.RetrainPriorityLabel, row.Retrain);
            Assert.Equal(source.AceDeltaLabel, row.AceDelta);
            Assert.Equal($"{source.CoefficientOfVariation * 100:F2}%", row.Cv);
            Assert.Equal(CellBands.ForRetrain(source), row.RetrainBand);
            Assert.Equal(CellBands.ForStyleOverlap(source), row.StyleBand);
            Assert.Equal(CellBands.ForAceDelta(report, source), row.AceDeltaBand);
        }
    }

    [Fact]
    public void Saved_column_order_hides_a_column_and_keeps_Uma_first()
    {
        var prefs = new GridColumnGridPreferences
        {
            Order = [GridColumnIds.Analysis.Ceiling, GridColumnIds.Analysis.Gap],
            Hidden = [GridColumnIds.Analysis.Spread, GridColumnIds.Analysis.Uma],
        };
        AnalysisScreen screen = AnalysisScreen.Load(TrialSets.Build("TT-K0"), prefs);

        Assert.Equal(GridColumnIds.Analysis.Uma, screen.Columns[0].Id);
        Assert.Equal(GridColumnIds.Analysis.Ceiling, screen.Columns[1].Id);
        Assert.Equal(GridColumnIds.Analysis.Gap, screen.Columns[2].Id);
        Assert.DoesNotContain(screen.Columns, column => column.Id == GridColumnIds.Analysis.Spread);
        Assert.Contains(screen.Columns, column => column.Id == GridColumnIds.Analysis.AceDelta);
    }

    [Fact]
    public void Mismatch_and_single_race_drops_explain_that_analysis_is_unavailable()
    {
        AnalysisScreen mismatch = AnalysisScreen.Load(TrialSets.Build("TT-Mismatch"), AllColumns);
        Assert.False(mismatch.ShowGrid);
        Assert.Equal(AnalysisCellText.Unavailable, mismatch.EmptyMessage);
        Assert.Equal("", mismatch.Summary);
        Assert.Empty(mismatch.Rows);
        Assert.Null(mismatch.Report);

        AnalysisScreen single = AnalysisScreen.Load(TrialSets.Build("Single-RoomMatch"), AllColumns);
        Assert.False(single.ShowGrid);
        Assert.Equal(AnalysisCellText.Unavailable, single.EmptyMessage);

        TeamTrialBatch team = TrialSets.Build("TT-K0");
        TeamTrialBatch races = TrialSets.Build("Single-CM12");
        AnalysisScreen mixed = AnalysisScreen.Load(new TeamTrialBatch
        {
            Kind = TeamTrialBatchKind.MixedOrNonTT,
            Trials = team.Trials,
            SingleRaceImports = races.SingleRaceImports,
        }, AllColumns);
        Assert.False(mixed.ShowGrid);
        Assert.Equal(AnalysisCellText.Unavailable, mixed.EmptyMessage);
    }
}
