using UmaParser.Presentation;

namespace UmaParser.Views;

/// <summary>
/// How a Results cell's text is painted. Every value is a real brush in the sheet view.
/// There is no "unset" tone: a null foreground is what hid names and scores.
/// </summary>
internal enum ResultsTextTone
{
    /// <summary>Body text. Painted with the theme control-text brush.</summary>
    Theme,

    /// <summary>Outlier text. Painted black, on the yellow fill.</summary>
    Black,

    /// <summary>Total row. Painted with the theme control-text brush, semibold unless bold.</summary>
    Footer,
}

/// <summary>Font weight for one Results cell. Bold wins over the footer semibold.</summary>
internal enum ResultsTextWeight
{
    Normal,
    SemiBold,
    Bold,
}

/// <summary>One Results column. <see cref="ToolTip"/> is the full filename. The name column has none.</summary>
internal sealed class ResultsSheetColumn
{
    public required string Header { get; init; }
    public required string ToolTip { get; init; }
}

/// <summary>
/// One Results cell. <see cref="Text"/> is the string the sheet draws.
/// The property name is <see cref="ResultsSheetBuilder.DisplayTextProperty"/>.
/// </summary>
internal sealed class ResultsSheetCell
{
    public required string Text { get; init; }
    public ScoreHeatColor? Heat { get; init; }
    public bool Outlier { get; init; }
    public ResultsTextTone Tone { get; init; }
    public ResultsTextWeight Weight { get; init; }
}

/// <summary>One Results row. The total row has <see cref="Footer"/> set.</summary>
internal sealed class ResultsSheetRow
{
    public required bool Footer { get; init; }
    public required bool TeamBoundary { get; init; }
    public required IReadOnlyList<ResultsSheetCell> Cells { get; init; }
}

/// <summary>
/// The Results grid as data: columns, rows, and the text that must be drawn.
/// Brushes and column widths are chosen by the sheet view, from this.
/// </summary>
internal sealed class ResultsSheetModel
{
    public static ResultsSheetModel Empty { get; } = new([], []);

    public ResultsSheetModel(IReadOnlyList<ResultsSheetColumn> columns, IReadOnlyList<ResultsSheetRow> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<ResultsSheetColumn> Columns { get; }
    public IReadOnlyList<ResultsSheetRow> Rows { get; }
}

/// <summary>Builds a <see cref="ResultsSheetModel"/> from the screen rows. Text is copied, not dropped.</summary>
internal static class ResultsSheetBuilder
{
    /// <summary>The property the sheet reads when it draws a cell. A binding has to point here too.</summary>
    public const string DisplayTextProperty = nameof(ResultsSheetCell.Text);

    public static bool DrawsText(ResultsTextTone tone) =>
        tone is ResultsTextTone.Theme or ResultsTextTone.Black or ResultsTextTone.Footer;

    public static ResultsSheetModel Build(IReadOnlyList<ResultsColumn> columns, IReadOnlyList<ResultsGridRow> rows)
    {
        var sheetColumns = new ResultsSheetColumn[columns.Count];
        for (int column = 0; column < columns.Count; column++)
        {
            sheetColumns[column] = new ResultsSheetColumn
            {
                Header = columns[column].Header,
                ToolTip = columns[column].ToolTip,
            };
        }

        var sheetRows = new ResultsSheetRow[rows.Count];
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            ResultsGridRow row = rows[rowIndex];
            var cells = new ResultsSheetCell[columns.Count];
            for (int column = 0; column < columns.Count; column++)
            {
                ResultsCell? source = column < row.Cells.Count ? row.Cells[column] : null;
                cells[column] = new ResultsSheetCell
                {
                    Text = source?.Text ?? "",
                    Heat = source?.Heat,
                    Outlier = source?.Outlier ?? false,
                    Tone = ToneFor(source, row.IsFooter),
                    Weight = WeightFor(source, row.IsFooter),
                };
            }

            sheetRows[rowIndex] = new ResultsSheetRow
            {
                Footer = row.IsFooter,
                TeamBoundary = row.IsTeamBoundary,
                Cells = cells,
            };
        }

        return new ResultsSheetModel(sheetColumns, sheetRows);
    }

    /// <summary>
    /// Outlier, then footer, then ordinary body text. Same order as the old cell triggers.
    /// A body cell is <see cref="ResultsTextTone.Theme"/>, never a null brush.
    /// </summary>
    public static ResultsTextTone ToneFor(ResultsCell? cell, bool footer)
    {
        if (cell?.Outlier == true)
        {
            return ResultsTextTone.Black;
        }

        return footer ? ResultsTextTone.Footer : ResultsTextTone.Theme;
    }

    public static ResultsTextWeight WeightFor(ResultsCell? cell, bool footer)
    {
        if (cell?.Bold == true)
        {
            return ResultsTextWeight.Bold;
        }

        return footer ? ResultsTextWeight.SemiBold : ResultsTextWeight.Normal;
    }
}

/// <summary>Which columns intersect a horizontal window. The sheet draws only this range.</summary>
internal readonly record struct ColumnWindow(int First, int LastExclusive)
{
    public int Count => Math.Max(0, LastExclusive - First);
}

internal static class ResultsSheetViewport
{
    public static ColumnWindow VisibleRange(IReadOnlyList<double> widths, double offset, double viewport)
    {
        int count = widths.Count;
        if (count == 0 || viewport <= 0)
        {
            return new ColumnWindow(0, 0);
        }

        if (offset < 0)
        {
            offset = 0;
        }

        int first = count;
        int last = count;
        double x = 0;
        double rightEdge = offset + viewport;
        for (int i = 0; i < count; i++)
        {
            double next = x + widths[i];
            if (first == count && next > offset)
            {
                first = i;
            }

            if (x >= rightEdge)
            {
                last = i;
                break;
            }

            x = next;
        }

        if (first == count)
        {
            return new ColumnWindow(count, count);
        }

        return new ColumnWindow(first, last);
    }
}

/// <summary>What the pointer is over. Resize is only set inside the header row.</summary>
internal readonly record struct ResultsSheetHit(bool InHeader, int Row, int Column, int ResizeColumn)
{
    public bool IsResize => InHeader && ResizeColumn >= 0;
}

/// <summary>
/// Hit test in sheet coordinates. Y above the sheet or below the header is not a resize,
/// even when X sits on a column edge. That is the case while the mouse is captured and the
/// pointer is over the tabs.
/// </summary>
internal static class ResultsSheetHitTest
{
    public static ResultsSheetHit Hit(
        double x,
        double y,
        double headerHeight,
        double rowHeight,
        double verticalOffset,
        int rowCount,
        IReadOnlyList<double> prefix,
        double horizontalOffset,
        double resizeSlop)
    {
        bool inHeader = y >= 0 && y < headerHeight;
        int resizeColumn = inHeader ? NearestBoundary(x + horizontalOffset, prefix, resizeSlop) : -1;
        int row = -1;
        if (y >= headerHeight && rowHeight > 0 && rowCount > 0)
        {
            int index = (int)((y - headerHeight + verticalOffset) / rowHeight);
            if ((uint)index < (uint)rowCount)
            {
                row = index;
            }
        }

        return new ResultsSheetHit(inHeader, row, ColumnIndex(x + horizontalOffset, prefix), resizeColumn);
    }

    private static int ColumnIndex(double contentX, IReadOnlyList<double> prefix)
    {
        int columns = prefix.Count - 1;
        if (columns <= 0)
        {
            return -1;
        }

        int lo = 0;
        int hi = columns - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (contentX < prefix[mid])
            {
                hi = mid - 1;
            }
            else if (contentX >= prefix[mid + 1])
            {
                lo = mid + 1;
            }
            else
            {
                return mid;
            }
        }

        return -1;
    }

    private static int NearestBoundary(double contentX, IReadOnlyList<double> prefix, double slop)
    {
        int columns = prefix.Count - 1;
        int found = -1;
        double best = slop;
        for (int column = 0; column < columns; column++)
        {
            double distance = Math.Abs(contentX - prefix[column + 1]);
            if (distance <= best)
            {
                best = distance;
                found = column;
            }
        }

        return found;
    }
}

/// <summary>Plain click, row-range drag, or column resize. Capture is only for an active drag.</summary>
internal enum ResultsDrag
{
    None,
    Selecting,
    Resizing,
}

/// <summary>
/// Pointer state for the Results sheet. A click selects the whole row and does not take capture.
/// Capture turns on only after the pointer moves past <see cref="DragSlop"/> during a drag,
/// and <see cref="Release"/> / <see cref="Cancel"/> always drop it.
/// </summary>
internal sealed class ResultsInteraction
{
    public const double DragSlop = 3;

    private readonly HashSet<int> _selected = new();
    private double _pressX;
    private double _pressY;

    public ResultsDrag Drag { get; private set; }
    public bool CaptureHeld { get; private set; }
    public bool Pressed { get; private set; }
    public int ResizeColumn { get; private set; } = -1;
    public int AnchorRow { get; private set; } = -1;
    public int CurrentRow { get; private set; } = -1;
    public int CurrentColumn { get; private set; } = -1;
    public int SelectedCount => _selected.Count;

    public bool IsSelected(int row) => _selected.Contains(row);

    public IEnumerable<int> SelectedRows => _selected.Order();

    /// <summary>True when this press should restore the column's measured width.</summary>
    public bool Press(ResultsSheetHit hit, int clickCount, bool shift, bool control, double x, double y)
    {
        Pressed = true;
        _pressX = x;
        _pressY = y;
        CaptureHeld = false;

        if (hit.IsResize && clickCount >= 2)
        {
            Drag = ResultsDrag.None;
            ResizeColumn = hit.ResizeColumn;
            Pressed = false;
            return true;
        }

        if (hit.IsResize)
        {
            Drag = ResultsDrag.Resizing;
            ResizeColumn = hit.ResizeColumn;
            return false;
        }

        ResizeColumn = -1;
        Drag = ResultsDrag.None;
        if (hit.Row < 0)
        {
            return false;
        }

        CurrentRow = hit.Row;
        if (hit.Column >= 0)
        {
            CurrentColumn = hit.Column;
        }

        if (control && !shift)
        {
            if (!_selected.Add(hit.Row))
            {
                _selected.Remove(hit.Row);
            }

            AnchorRow = hit.Row;
            return false;
        }

        if (shift)
        {
            SelectRange(AnchorRow < 0 ? hit.Row : AnchorRow, hit.Row);
            return false;
        }

        _selected.Clear();
        _selected.Add(hit.Row);
        AnchorRow = hit.Row;
        Drag = ResultsDrag.Selecting;
        return false;
    }

    public void Move(ResultsSheetHit hit, bool leftButtonDown, double x, double y)
    {
        if (!Pressed || !leftButtonDown || Drag == ResultsDrag.None)
        {
            return;
        }

        if (!CaptureHeld)
        {
            double dx = x - _pressX;
            double dy = y - _pressY;
            if ((dx * dx) + (dy * dy) >= DragSlop * DragSlop)
            {
                CaptureHeld = true;
            }
        }

        if (Drag == ResultsDrag.Selecting && hit.Row >= 0)
        {
            CurrentRow = hit.Row;
            SelectRange(AnchorRow < 0 ? hit.Row : AnchorRow, hit.Row);
        }
    }

    /// <summary>Mouse button up. Drops capture even if the pointer has left the sheet.</summary>
    public void Release()
    {
        Pressed = false;
        Drag = ResultsDrag.None;
        ResizeColumn = -1;
        CaptureHeld = false;
    }

    /// <summary>Lost capture, Escape, or the window deactivating.</summary>
    public void Cancel() => Release();

    public void NoteCaptureFailed() => CaptureHeld = false;

    public void Clear()
    {
        Release();
        _selected.Clear();
        AnchorRow = -1;
        CurrentRow = -1;
        CurrentColumn = -1;
    }

    public void MoveRow(int delta, bool extend, int rowCount)
    {
        if (rowCount <= 0)
        {
            return;
        }

        int current = CurrentRow < 0 ? (delta > 0 ? -1 : rowCount) : CurrentRow;
        int next = Math.Clamp(current + delta, 0, rowCount - 1);
        CurrentRow = next;
        if (CurrentColumn < 0)
        {
            CurrentColumn = 0;
        }

        if (extend)
        {
            SelectRange(AnchorRow < 0 ? next : AnchorRow, next);
        }
        else
        {
            _selected.Clear();
            _selected.Add(next);
            AnchorRow = next;
        }
    }

    public void MoveColumn(int delta, int columnCount)
    {
        if (columnCount <= 0)
        {
            return;
        }

        CurrentColumn = Math.Clamp((CurrentColumn < 0 ? 0 : CurrentColumn) + delta, 0, columnCount - 1);
    }

    public void SelectAll(int rowCount)
    {
        _selected.Clear();
        for (int row = 0; row < rowCount; row++)
        {
            _selected.Add(row);
        }

        AnchorRow = 0;
        if (CurrentRow < 0 && rowCount > 0)
        {
            CurrentRow = 0;
        }
    }

    private void SelectRange(int from, int to)
    {
        _selected.Clear();
        int start = Math.Min(from, to);
        int end = Math.Max(from, to);
        for (int row = start; row <= end; row++)
        {
            _selected.Add(row);
        }
    }
}
