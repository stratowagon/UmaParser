using UmaParser.Presentation;

namespace UmaParser.Settings;

internal sealed class GridColumnOptionsGrid
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<GridColumnOptionsEntry> Columns { get; init; }
}

internal sealed class GridColumnOptionsEntry
{
    public required string Id { get; init; }
    public required string Header { get; init; }
    public required string Tooltip { get; init; }
    public required string Description { get; init; }
    public bool IsLocked { get; init; }
    public required Func<GridColumnContext, bool> IsAvailable { get; init; }
    public required Func<GridColumnContext, string?> UnavailableReason { get; init; }

    public override string ToString() => Header;
}

internal readonly record struct ColumnOptionRow(string Id, string Header, bool IsVisible, bool IsAvailable);

/// <summary>
/// The three grids the Options dialog edits, in the same order and with the same words as WinForms.
/// </summary>
internal static class ColumnOptionsCatalog
{
    public static IReadOnlyList<GridColumnOptionsGrid> All { get; } =
    [
        From(GridKeys.Analysis, "Team Analysis", AnalysisColumnSet.All),
        From(GridKeys.Skills, "Skills", SkillsColumnSet.All),
        From(GridKeys.Tracks, "Tracks", TracksColumnSet.All),
    ];

    public static string Description(GridColumnOptionsEntry entry, GridColumnContext context)
    {
        var lines = new List<string> { entry.Description };
        if (!string.IsNullOrWhiteSpace(entry.Tooltip) && entry.Tooltip != entry.Description)
        {
            lines.Add(string.Empty);
            lines.Add($"Tooltip: {entry.Tooltip}");
        }

        string? reason = entry.UnavailableReason(context);
        if (!entry.IsAvailable(context) && !string.IsNullOrWhiteSpace(reason))
        {
            lines.Add(string.Empty);
            lines.Add($"Not available: {reason}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string LockedColumnText(GridColumnOptionsGrid grid)
    {
        GridColumnOptionsEntry? locked = grid.Columns.FirstOrDefault(column => column.IsLocked);
        return locked == null ? string.Empty : $"{locked.Header} — always shown first";
    }

    private static GridColumnOptionsGrid From<TRow>(
        string key,
        string displayName,
        IReadOnlyList<GridColumnDefinition<TRow>> columns) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            Columns = columns.Select(column => new GridColumnOptionsEntry
            {
                Id = column.Id,
                Header = column.Header,
                Tooltip = column.Tooltip,
                Description = column.Description,
                IsLocked = column.IsLocked,
                IsAvailable = column.CheckAvailable,
                UnavailableReason = column.GetUnavailableReason,
            }).ToList(),
        };
}
