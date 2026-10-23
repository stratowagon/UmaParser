namespace UmaParser.Ui;

internal static class ResultsGridHeaders
{
    public const int CompactHeaderHeight = 36;

    /// <summary>
    /// Compact two-line column label for a capture filename.
    /// The text comes from ResultsFileHeader. The full filename is the column tooltip.
    /// </summary>
    public static string FormatCompact(string fileName) =>
        Presentation.ResultsFileHeader.FormatCompact(fileName);

    public static void ApplyCompactHeaderLayout(DataGridView grid)
    {
        grid.ColumnHeadersHeight = CompactHeaderHeight;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
    }

    public static void ApplyDefaultHeaderLayout(DataGridView grid)
    {
        grid.ColumnHeadersHeight = 23;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
    }
}
