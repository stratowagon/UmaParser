using System.ComponentModel;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.Ui;

public static class GridColumnBinder
{
    public static IReadOnlyList<GridColumnDefinition<TRow>> ResolveEffectiveColumns<TRow>(
        IReadOnlyList<GridColumnDefinition<TRow>> registry,
        GridColumnGridPreferences prefs,
        GridColumnContext context) =>
        GridColumnLayout.ResolveEffectiveColumns(registry, prefs, context);

    public static void Bind<TRow>(
        DataGridView grid,
        IReadOnlyList<GridColumnDefinition<TRow>> registry,
        GridColumnGridPreferences prefs,
        GridColumnContext context,
        IEnumerable<TRow> rows,
        DataGridViewColumnSortMode sortMode,
        Action<DataGridViewRow, TRow>? configureRow = null)
    {
        grid.Columns.Clear();
        grid.Rows.Clear();

        var effective = ResolveEffectiveColumns(registry, prefs, context);
        foreach (var def in effective)
        {
            int index = grid.Columns.Add(def.Header, def.Header);
            var column = grid.Columns[index];
            column.Name = def.Id;
            column.ValueType = def.ValueType;
            column.SortMode = sortMode;
            column.ToolTipText = def.Tooltip;
        }

        foreach (TRow row in rows)
        {
            var values = effective.Select(def => def.GetValue(row) ?? DBNull.Value).Cast<object>().ToArray();
            int rowIndex = grid.Rows.Add(values);
            configureRow?.Invoke(grid.Rows[rowIndex], row);
        }
    }

    public static object? GetFooterValue<TRow>(
        IReadOnlyList<GridColumnDefinition<TRow>> registry,
        GridColumnGridPreferences prefs,
        GridColumnContext context,
        string columnId,
        object? value)
    {
        var effective = ResolveEffectiveColumns(registry, prefs, context);
        return effective.Any(c => c.Id == columnId) ? value ?? DBNull.Value : null;
    }

    public static IReadOnlyList<string> GetDefaultCustomizableOrder<TRow>(
        IReadOnlyList<GridColumnDefinition<TRow>> registry) =>
        GridColumnLayout.GetDefaultCustomizableOrder(registry);

    public static DataGridViewColumn? FindColumn(DataGridView grid, string columnId) =>
        grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Name == columnId);

    public static void ApplyDefaultSort(DataGridView grid, string preferredColumnId, string fallbackColumnId)
    {
        var column = FindColumn(grid, preferredColumnId) ?? FindColumn(grid, fallbackColumnId);
        if (column != null)
        {
            grid.Sort(column, ListSortDirection.Descending);
        }
    }

    public static void ApplyDefaultSortAscending(DataGridView grid, string columnId)
    {
        var column = FindColumn(grid, columnId);
        if (column != null)
        {
            grid.Sort(column, ListSortDirection.Ascending);
        }
    }

    public static object[] BuildRowValues<TRow>(
        IReadOnlyList<GridColumnDefinition<TRow>> effective,
        Func<string, object?> getValueById) =>
        effective.Select(def => getValueById(def.Id) ?? DBNull.Value).Cast<object>().ToArray();
}