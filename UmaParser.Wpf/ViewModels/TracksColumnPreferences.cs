using UmaParser.MasterData;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.ViewModels;

/// <summary>
/// Reads the Tracks column order and hidden list from the same settings.json the WinForms app writes.
/// The WPF app does not edit those preferences yet.
/// </summary>
internal static class TracksColumnPreferences
{
    public static GridColumnGridPreferences Load()
    {
        GameMasterSettings settings = GameMasterSettings.Load();
        IReadOnlyList<string> defaultOrder = GridColumnLayout.GetDefaultCustomizableOrder(TracksColumnSet.All);
        return GridColumnPreferencesMerger.Merge(defaultOrder, settings.GetGridColumnPreferences(GridKeys.Tracks));
    }
}
