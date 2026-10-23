namespace UmaParser.Presentation;

/// <summary>One Results column. <see cref="ToolTip"/> is the full filename. The name column has none.</summary>
public sealed class ResultsColumn
{
    public required string Header { get; init; }
    public required string ToolTip { get; init; }
}

/// <summary>
/// One Results cell. <see cref="Value"/> is what WinForms puts in the grid (a name, a score, or "Total").
/// <see cref="Text"/> is that value's display text. <see cref="Heat"/> is set on roster-stats scores only.
/// </summary>
public sealed class ResultsCell
{
    public required string Text { get; init; }
    public object? Value { get; init; }
    public bool Bold { get; init; }
    public bool Outlier { get; init; }
    public ScoreHeatColor? Heat { get; init; }
}

/// <summary>
/// One Results row. The last row of a roster-stats grid is the total:
/// <see cref="IsFooter"/> is true, and it has no heat-map color.
/// </summary>
public sealed class ResultsGridRow
{
    public bool IsFooter { get; init; }
    public bool IsTeamBoundary { get; init; }
    /// <summary><see cref="IList{T}"/> so a WPF binding can read <c>Cells[0]</c>. <c>IReadOnlyList</c> indexers do not bind.</summary>
    public required IList<ResultsCell> Cells { get; init; }
}
