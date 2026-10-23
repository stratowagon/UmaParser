using UmaParser.Settings;

namespace UmaParser.Presentation;

/// <summary>
/// Which columns a grid shows, in order. WinForms <c>GridColumnBinder</c> and the WPF
/// Skills view both call this so a saved column preference means the same thing in both apps.
/// </summary>
public static class GridColumnLayout
{
    public static IReadOnlyList<GridColumnDefinition<TRow>> ResolveEffectiveColumns<TRow>(
        IReadOnlyList<GridColumnDefinition<TRow>> registry,
        GridColumnGridPreferences prefs,
        GridColumnContext context)
    {
        var customizable = registry.Where(c => !c.IsLocked).Select(c => c.Id).ToList();
        var visibleIds = GridColumnPreferencesMerger.ResolveVisibleOrder(
            customizable,
            prefs,
            id => registry.First(c => c.Id == id).CheckAvailable(context));

        var byId = registry.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var effective = new List<GridColumnDefinition<TRow>>();

        foreach (var locked in registry.Where(c => c.IsLocked))
        {
            if (locked.CheckAvailable(context))
            {
                effective.Add(locked);
            }
        }

        foreach (string id in visibleIds)
        {
            effective.Add(byId[id]);
        }

        return effective;
    }

    public static IReadOnlyList<string> GetDefaultCustomizableOrder<TRow>(
        IReadOnlyList<GridColumnDefinition<TRow>> registry) =>
        registry.Where(c => !c.IsLocked).Select(c => c.Id).ToList();
}
