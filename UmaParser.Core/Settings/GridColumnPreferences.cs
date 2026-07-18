namespace UmaParser.Settings;

public sealed class GridColumnGridPreferences
{
    public List<string> Order { get; set; } = new();

    public List<string> Hidden { get; set; } = new();
}

public sealed class GridColumnPreferencesStore
{
    public Dictionary<string, GridColumnGridPreferences> Grids { get; set; } =
        new(StringComparer.Ordinal);

    public GridColumnGridPreferences GetOrCreate(string gridKey)
    {
        if (!Grids.TryGetValue(gridKey, out var prefs))
        {
            prefs = new GridColumnGridPreferences();
            Grids[gridKey] = prefs;
        }

        return prefs;
    }
}

public static class GridColumnPreferencesMerger
{
    public static GridColumnGridPreferences Merge(
        IReadOnlyList<string> defaultCustomizableOrder,
        GridColumnGridPreferences? saved)
    {
        var hidden = new HashSet<string>(
            saved?.Hidden ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        var order = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (saved?.Order != null)
        {
            foreach (string id in saved.Order)
            {
                if (!defaultCustomizableOrder.Contains(id, StringComparer.Ordinal))
                {
                    continue;
                }

                order.Add(id);
                seen.Add(id);
            }
        }

        foreach (string id in defaultCustomizableOrder)
        {
            if (seen.Add(id))
            {
                order.Add(id);
            }
        }

        hidden.IntersectWith(defaultCustomizableOrder);

        return new GridColumnGridPreferences
        {
            Order = order,
            Hidden = hidden.ToList(),
        };
    }

    public static IReadOnlyList<string> ResolveVisibleOrder(
        IReadOnlyList<string> defaultCustomizableOrder,
        GridColumnGridPreferences prefs,
        Func<string, bool> isAvailable)
    {
        var merged = Merge(defaultCustomizableOrder, prefs);
        var hidden = new HashSet<string>(merged.Hidden, StringComparer.Ordinal);
        var visible = new List<string>();

        foreach (string id in merged.Order)
        {
            if (hidden.Contains(id) || !isAvailable(id))
            {
                continue;
            }

            visible.Add(id);
        }

        return visible;
    }
}