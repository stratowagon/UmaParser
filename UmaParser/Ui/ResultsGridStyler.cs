using System.Drawing;
using System.Windows.Forms;

namespace UmaParser.Ui;

/// <summary>
/// Bakes results-grid cell styles once at bind time so scrolling does not re-run CellFormatting.
/// </summary>
internal static class ResultsGridStyler
{
    private const int UmaPerTeam = 3;
    private const int RosterRowCount = 15;

    public static void ApplyRosterStats(DataGridView grid, int statsTotalRowIndex, AceNameFontCache fontCache)
    {
        double scoreMin = double.MaxValue;
        double scoreMax = double.MinValue;

        for (int col = 1; col < grid.ColumnCount; col++)
        {
            for (int row = 0; row < statsTotalRowIndex; row++)
            {
                if (TryGetNumericCellValue(grid[col, row].Value, out double score))
                {
                    scoreMin = Math.Min(scoreMin, score);
                    scoreMax = Math.Max(scoreMax, score);
                }
            }
        }

        bool heatmapActive = scoreMin <= scoreMax;
        var headerStyle = grid.ColumnHeadersDefaultCellStyle;
        Font boldFont = fontCache.GetBoldFont(grid);

        grid.SuspendLayout();
        try
        {
            for (int row = 0; row < grid.RowCount; row++)
            {
                for (int col = 0; col < grid.ColumnCount; col++)
                {
                    DataGridViewCellStyle style = grid[col, row].Style;

                    if (row == statsTotalRowIndex)
                    {
                        ApplyHeaderLikeStyle(style, headerStyle);
                        continue;
                    }

                    if (col == 0 && IsAceRow(row))
                    {
                        style.Font = boldFont;
                    }

                    if (heatmapActive && col >= 1 && TryGetNumericCellValue(grid[col, row].Value, out double score))
                    {
                        ApplyColorScaleStyle(style, score, scoreMin, scoreMax);
                    }
                }
            }
        }
        finally
        {
            grid.ResumeLayout();
        }
    }

    public static void ApplyRosterMismatch(
        DataGridView grid,
        bool[,] outliers,
        int fileCount,
        AceNameFontCache fontCache)
    {
        Color outlierBack = AppColors.IsDark
            ? Color.FromArgb(90, 80, 30)
            : Color.FromArgb(255, 255, 200);
        Color outlierFore = AppColors.SeverityForeFor(outlierBack);
        Font boldFont = fontCache.GetBoldFont(grid);

        grid.SuspendLayout();
        try
        {
            for (int row = 0; row < grid.RowCount; row++)
            {
                for (int col = 0; col < grid.ColumnCount; col++)
                {
                    DataGridViewCellStyle style = grid[col, row].Style;

                    if (IsAceRow(row))
                    {
                        style.Font = boldFont;
                    }

                    if (col < fileCount && row < RosterRowCount && outliers[col, row])
                    {
                        style.BackColor = outlierBack;
                        style.ForeColor = outlierFore;
                    }
                }
            }
        }
        finally
        {
            grid.ResumeLayout();
        }
    }

    private static void ApplyColorScaleStyle(DataGridViewCellStyle style, double score, double min, double max)
    {
        Color back = ConditionalColorScale.ColorForValue(score, min, max);
        style.BackColor = back;
        style.ForeColor = AppColors.IsDark
            ? HeatmapForeForDark(back)
            : AppColors.SeverityForeFor(back);
        style.SelectionBackColor = ControlPaint.Light(back, 0.28f);
        style.SelectionForeColor = style.ForeColor;
    }

    /// <summary>
    /// Bright text on tinted extremes; muted text on near-neutral cells so outliers stand out.
    /// </summary>
    private static Color HeatmapForeForDark(Color back)
    {
        Color neutral = AppColors.HeatmapNeutralBack;
        int delta = Math.Abs(back.R - neutral.R)
            + Math.Abs(back.G - neutral.G)
            + Math.Abs(back.B - neutral.B);
        return delta < 45 ? AppColors.MutedFore : AppColors.GridFore;
    }

    private static void ApplyHeaderLikeStyle(DataGridViewCellStyle cellStyle, DataGridViewCellStyle headerStyle)
    {
        cellStyle.BackColor = headerStyle.BackColor;
        cellStyle.ForeColor = headerStyle.ForeColor;
        cellStyle.Font = headerStyle.Font;
        cellStyle.SelectionBackColor = ControlPaint.Dark(headerStyle.BackColor);
        cellStyle.SelectionForeColor = headerStyle.ForeColor;
    }

    private static bool IsAceRow(int rowIndex) =>
        rowIndex >= 0 && rowIndex < RosterRowCount && rowIndex % UmaPerTeam == 0;

    private static bool TryGetNumericCellValue(object? value, out double number)
    {
        switch (value)
        {
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case double d:
                number = d;
                return true;
            case float f:
                number = f;
                return true;
            case decimal m:
                number = (double)m;
                return true;
            case string s when double.TryParse(s, out double parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }
}