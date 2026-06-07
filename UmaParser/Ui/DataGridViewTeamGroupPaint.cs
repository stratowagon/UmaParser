using System.Drawing;
using System.Windows.Forms;

namespace UmaBlobber.Ui;

internal static class DataGridViewTeamGroupPaint
{
    private static readonly int[] DefaultTeamBoundaryRows = [3, 6, 9, 12];

    public static void PaintTopBorder(
        DataGridView grid,
        DataGridViewCellPaintingEventArgs e,
        float borderWidth,
        IReadOnlyList<int> boundaryRows)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics == null)
        {
            return;
        }

        if (!boundaryRows.Contains(e.RowIndex))
        {
            return;
        }

        e.Paint(e.ClipBounds, DataGridViewPaintParts.All);

        Color borderColor = grid.GridColor;
        if (grid.ColumnHeadersDefaultCellStyle.BackColor != Color.Empty)
        {
            borderColor = ControlPaint.Dark(grid.ColumnHeadersDefaultCellStyle.BackColor);
        }

        using var pen = new Pen(borderColor, borderWidth);
        int y = e.CellBounds.Top + (int)(borderWidth / 2f);
        e.Graphics.DrawLine(pen, e.CellBounds.Left, y, e.CellBounds.Right - 1, y);
        e.Handled = true;
    }

    public static void PaintDefaultTeamBoundaries(
        DataGridView grid,
        DataGridViewCellPaintingEventArgs e,
        float borderWidth)
    {
        PaintTopBorder(grid, e, borderWidth, DefaultTeamBoundaryRows);
    }
}