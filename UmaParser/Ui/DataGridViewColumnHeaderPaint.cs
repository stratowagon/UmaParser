using System.Drawing;
using System.Windows.Forms;

namespace UmaParser.Ui;

/// <summary>
/// With <see cref="DataGridView.EnableHeadersVisualStyles"/> disabled, header sort glyphs
/// can render in a low-contrast color. Repaint them using the header foreground color.
/// </summary>
internal static class DataGridViewColumnHeaderPaint
{
    private const int GlyphSize = 7;
    private const int GlyphMargin = 6;

    public static void EnableThemedSortGlyphs(DataGridView grid)
    {
        grid.CellPainting += OnCellPainting;
    }

    private static void OnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid
            || e.RowIndex != -1
            || e.ColumnIndex < 0
            || e.Graphics == null)
        {
            return;
        }

        SortOrder direction = grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection;
        if (direction == SortOrder.None)
        {
            return;
        }

        e.Paint(
            e.CellBounds,
            DataGridViewPaintParts.Background
                | DataGridViewPaintParts.Border
                | DataGridViewPaintParts.ContentForeground);
        Color glyphColor = e.CellStyle?.ForeColor ?? SystemColors.ControlText;
        DrawSortGlyph(e.Graphics, e.CellBounds, glyphColor, direction);
        e.Handled = true;
    }

    private static void DrawSortGlyph(Graphics graphics, Rectangle bounds, Color color, SortOrder direction)
    {
        int x = bounds.Right - GlyphSize - GlyphMargin;
        int y = bounds.Top + (bounds.Height - GlyphSize) / 2;

        Point[] points = direction == SortOrder.Ascending
            ?
            [
                new Point(x, y + GlyphSize),
                new Point(x + GlyphSize, y + GlyphSize),
                new Point(x + GlyphSize / 2, y),
            ]
            :
            [
                new Point(x, y),
                new Point(x + GlyphSize, y),
                new Point(x + GlyphSize / 2, y + GlyphSize),
            ];

        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, points);
    }
}