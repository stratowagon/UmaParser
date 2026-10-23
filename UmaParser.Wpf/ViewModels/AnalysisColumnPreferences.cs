using UmaParser.MasterData;
using UmaParser.Presentation;
using UmaParser.Settings;

namespace UmaParser.ViewModels;

/// <summary>
/// Reads the Team Analysis column order and hidden list from the same settings.json the WinForms app writes.
/// The WPF app does not edit those preferences yet.
/// </summary>
internal static class AnalysisColumnPreferences
{
    public static GridColumnGridPreferences Load()
    {
        GameMasterSettings settings = GameMasterSettings.Load();
        IReadOnlyList<string> defaultOrder = GridColumnLayout.GetDefaultCustomizableOrder(AnalysisColumnSet.All);
        return GridColumnPreferencesMerger.Merge(defaultOrder, settings.GetGridColumnPreferences(GridKeys.Analysis));
    }
}
