using System.Reflection;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.Presentation;
using UmaParser.Views;
using Xunit;

namespace UmaParser.Core.Tests.Presentation;

/// <summary>
/// The Results sheet is what the WPF view draws. Names and scores have to be on
/// <see cref="ResultsSheetCell.Text"/>, and every tone has to be one the painter draws.
/// A null foreground on the old cell style hid those strings while leaving the fill.
/// </summary>
public sealed class ResultsSheetTests
{
    [Fact]
    public void A_uniform_roster_puts_names_and_scores_on_the_drawn_text_property()
    {
        ResultsScreen screen = ResultsScreen.Load(TrialSets.Build("TT-K0"));
        ResultsSheetModel sheet = ResultsSheetBuilder.Build(screen.Columns, screen.Rows);

        PropertyInfo textProperty = typeof(ResultsSheetCell).GetProperty(ResultsSheetBuilder.DisplayTextProperty)!;
        Assert.NotNull(textProperty);
        Assert.Equal(typeof(string), textProperty.PropertyType);
        Assert.True(textProperty.CanRead);

        Assert.Equal(screen.Columns.Count, sheet.Columns.Count);
        Assert.Equal(screen.Rows.Count, sheet.Rows.Count);
        Assert.False(string.IsNullOrEmpty(sheet.Rows[0].Cells[0].Text));

        for (int row = 0; row < screen.Rows.Count; row++)
        {
            Assert.Equal(screen.Rows[row].IsFooter, sheet.Rows[row].Footer);
            Assert.Equal(screen.Rows[row].IsTeamBoundary, sheet.Rows[row].TeamBoundary);
            for (int column = 0; column < screen.Columns.Count; column++)
            {
                ResultsCell source = screen.Rows[row].Cells[column];
                ResultsSheetCell cell = sheet.Rows[row].Cells[column];
                Assert.Equal(source.Text, cell.Text);
                Assert.Equal(source.Text, textProperty.GetValue(cell));
                Assert.Equal(ResultsSheetBuilder.ToneFor(source, screen.Rows[row].IsFooter), cell.Tone);
                Assert.Equal(ResultsSheetBuilder.WeightFor(source, screen.Rows[row].IsFooter), cell.Weight);
                Assert.True(ResultsSheetBuilder.DrawsText(cell.Tone));
            }
        }

        ResultsSheetCell name = sheet.Rows[0].Cells[0];
        ResultsSheetCell score = sheet.Rows[0].Cells[1];
        ResultsSheetCell total = sheet.Rows[^1].Cells[0];
        Assert.Equal(ResultsTextTone.Theme, name.Tone);
        Assert.Equal(ResultsTextWeight.Bold, name.Weight);
        Assert.Equal(ResultsTextTone.Theme, score.Tone);
        Assert.False(string.IsNullOrEmpty(score.Text));
        Assert.Equal("Total", total.Text);
        Assert.Equal(ResultsTextTone.Footer, total.Tone);
        Assert.Equal(ResultsTextWeight.SemiBold, total.Weight);
        Assert.Equal(screen.Columns[1].ToolTip, sheet.Columns[1].ToolTip);
        Assert.Equal("", sheet.Columns[0].ToolTip);
    }

    [Fact]
    public void A_mismatch_keeps_roster_names_and_paints_outliers_black()
    {
        ResultsScreen screen = ResultsScreen.Load(TrialSets.Build("TT-Mismatch"));
        ResultsSheetModel sheet = ResultsSheetBuilder.Build(screen.Columns, screen.Rows);
        bool foundOutlier = false;

        for (int row = 0; row < screen.Rows.Count; row++)
        {
            for (int column = 0; column < screen.Columns.Count; column++)
            {
                ResultsCell source = screen.Rows[row].Cells[column];
                ResultsSheetCell cell = sheet.Rows[row].Cells[column];
                Assert.Equal(source.Text, cell.Text);
                Assert.False(string.IsNullOrEmpty(cell.Text));
                Assert.Equal(source.Outlier, cell.Outlier);
                if (!source.Outlier)
                {
                    continue;
                }

                foundOutlier = true;
                Assert.Equal(ResultsTextTone.Black, cell.Tone);
                Assert.True(ResultsSheetBuilder.DrawsText(cell.Tone));
            }
        }

        Assert.True(foundOutlier);
    }

    [Fact]
    public void A_page_window_covers_the_revealed_columns_only()
    {
        var widths = new double[400];
        Array.Fill(widths, 80);
        // Offset 800 is the left edge of column 10. 320px covers columns 10 through 13.
        ColumnWindow window = ResultsSheetViewport.VisibleRange(widths, offset: 800, viewport: 320);

        Assert.Equal(10, window.First);
        Assert.Equal(14, window.LastExclusive);
        Assert.Equal(4, window.Count);
        Assert.True(window.Count < 20);

        ColumnWindow origin = ResultsSheetViewport.VisibleRange(widths, offset: 0, viewport: 100);
        Assert.Equal(0, origin.First);
        Assert.Equal(2, origin.LastExclusive);

        Assert.Equal(new ColumnWindow(0, 0), ResultsSheetViewport.VisibleRange(widths, offset: 0, viewport: 0));
    }
}
