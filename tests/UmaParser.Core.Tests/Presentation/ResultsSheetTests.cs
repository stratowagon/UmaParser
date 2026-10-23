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

    [Fact]
    public void Resize_hit_test_stays_inside_the_header_row()
    {
        double[] prefix = [0, 100, 220];
        const double header = 40;
        const double rowHeight = 22;

        ResultsSheetHit above = ResultsSheetHitTest.Hit(100, y: -12, header, rowHeight, verticalOffset: 0, rowCount: 16, prefix, horizontalOffset: 0, resizeSlop: 5);
        Assert.False(above.InHeader);
        Assert.Equal(-1, above.ResizeColumn);
        Assert.False(above.IsResize);

        ResultsSheetHit headerEdge = ResultsSheetHitTest.Hit(100, y: 8, header, rowHeight, verticalOffset: 0, rowCount: 16, prefix, horizontalOffset: 0, resizeSlop: 5);
        Assert.True(headerEdge.InHeader);
        Assert.Equal(0, headerEdge.ResizeColumn);
        Assert.True(headerEdge.IsResize);

        ResultsSheetHit body = ResultsSheetHitTest.Hit(50, y: header, header, rowHeight, verticalOffset: 0, rowCount: 16, prefix, horizontalOffset: 0, resizeSlop: 5);
        Assert.False(body.InHeader);
        Assert.Equal(-1, body.ResizeColumn);
        Assert.Equal(0, body.Row);
        Assert.Equal(0, body.Column);
    }

    [Fact]
    public void A_click_selects_the_whole_row_and_capture_ends_with_the_gesture()
    {
        var pointer = new ResultsInteraction();
        ResultsSheetHit row2 = new(InHeader: false, Row: 2, Column: 4, ResizeColumn: -1);
        ResultsSheetHit row4 = new(InHeader: false, Row: 4, Column: 4, ResizeColumn: -1);

        Assert.False(pointer.Press(row2, clickCount: 1, shift: false, control: false, x: 10, y: 80));
        Assert.False(pointer.CaptureHeld);
        Assert.Equal(ResultsDrag.Selecting, pointer.Drag);
        Assert.True(pointer.IsSelected(2));
        Assert.Equal(1, pointer.SelectedCount);

        pointer.Move(row2, leftButtonDown: true, x: 11, y: 81);
        Assert.False(pointer.CaptureHeld);

        pointer.Move(row4, leftButtonDown: true, x: 12, y: 130);
        Assert.True(pointer.CaptureHeld);
        Assert.Equal(new[] { 2, 3, 4 }, pointer.SelectedRows.ToArray());

        pointer.Release();
        Assert.False(pointer.CaptureHeld);
        Assert.Equal(ResultsDrag.None, pointer.Drag);
        Assert.Equal(new[] { 2, 3, 4 }, pointer.SelectedRows.ToArray());

        pointer.Press(row2, clickCount: 1, shift: false, control: false, x: 10, y: 80);
        pointer.Move(row4, leftButtonDown: true, x: 20, y: 140);
        Assert.True(pointer.CaptureHeld);
        pointer.Cancel();
        Assert.False(pointer.CaptureHeld);
        Assert.Equal(ResultsDrag.None, pointer.Drag);

        pointer.Press(row2, clickCount: 1, shift: false, control: false, x: 10, y: 80);
        pointer.Move(row4, leftButtonDown: true, x: 20, y: 140);
        pointer.Release();
        pointer.Cancel();
        Assert.False(pointer.CaptureHeld);
    }

    [Fact]
    public void Shift_and_ctrl_clicks_change_whole_rows_without_capture()
    {
        var pointer = new ResultsInteraction();
        ResultsSheetHit Row(int row) => new(false, row, Column: 1, ResizeColumn: -1);

        pointer.Press(Row(1), clickCount: 1, shift: false, control: false, x: 0, y: 0);
        pointer.Release();

        pointer.Press(Row(3), clickCount: 1, shift: true, control: false, x: 0, y: 0);
        Assert.False(pointer.CaptureHeld);
        Assert.Equal(ResultsDrag.None, pointer.Drag);
        Assert.Equal(new[] { 1, 2, 3 }, pointer.SelectedRows.ToArray());

        pointer.Press(Row(1), clickCount: 1, shift: false, control: true, x: 0, y: 0);
        Assert.False(pointer.CaptureHeld);
        Assert.Equal(new[] { 2, 3 }, pointer.SelectedRows.ToArray());

        pointer.Press(Row(0), clickCount: 1, shift: false, control: true, x: 0, y: 0);
        Assert.Equal(new[] { 0, 2, 3 }, pointer.SelectedRows.ToArray());
        pointer.Release();
        Assert.False(pointer.CaptureHeld);
    }

    [Fact]
    public void A_selected_row_uses_the_datagrid_highlight_over_the_heat_fill()
    {
        Assert.Equal(ResultsRowChrome.Content, ResultsRowPaint.Chrome(selected: false, sheetFocused: true));
        Assert.Equal(ResultsRowChrome.ActiveSelection, ResultsRowPaint.Chrome(selected: true, sheetFocused: true));
        Assert.Equal(ResultsRowChrome.InactiveSelection, ResultsRowPaint.Chrome(selected: true, sheetFocused: false));
        Assert.Equal(ResultsRowChrome.Content, ResultsRowPaint.Chrome(selected: false, sheetFocused: false));
    }
}
