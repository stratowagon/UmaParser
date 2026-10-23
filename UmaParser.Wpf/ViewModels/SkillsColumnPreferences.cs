using UmaParser.MasterData;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.ViewModels;

/// <summary>
/// Reads the Skills column order and hidden list from the same settings.json the WinForms app writes.
/// The WPF app does not edit those preferences yet.
/// </summary>
internal static class SkillsColumnPreferences
{
    public static GridColumnGridPreferences Load()
    {
        GameMasterSettings settings = GameMasterSettings.Load();
        IReadOnlyList<string> defaultOrder = GridColumnLayout.GetDefaultCustomizableOrder(SkillsColumnSet.All);
        return GridColumnPreferencesMerger.Merge(defaultOrder, settings.GetGridColumnPreferences(GridKeys.Skills));
    }
}
