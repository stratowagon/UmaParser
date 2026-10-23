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
