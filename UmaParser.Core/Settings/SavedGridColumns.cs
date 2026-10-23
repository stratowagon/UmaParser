using UmaParser.MasterData;
using UmaParser.Presentation;

namespace UmaParser.Settings;

/// <summary>
/// Reads one grid's saved column order and hidden list from settings.json.
/// WinForms and WPF both call this so a preference written by either app means the same columns.
/// </summary>
internal static class SavedGridColumns
{
    public static GridColumnGridPreferences Load(
        string gridKey,
        IReadOnlyList<string> defaultCustomizableOrder,
        GameMasterSettings? settings = null)
    {
        settings ??= GameMasterSettings.Load();
        return GridColumnPreferencesMerger.Merge(
            defaultCustomizableOrder,
            settings.GetGridColumnPreferences(gridKey));
    }

    public static GridColumnGridPreferences ForAnalysis(GameMasterSettings? settings = null) =>
        Load(
            GridKeys.Analysis,
            GridColumnLayout.GetDefaultCustomizableOrder(AnalysisColumnSet.All),
            settings);

    public static GridColumnGridPreferences ForSkills(GameMasterSettings? settings = null) =>
        Load(
            GridKeys.Skills,
            GridColumnLayout.GetDefaultCustomizableOrder(SkillsColumnSet.All),
            settings);

    public static GridColumnGridPreferences ForTracks(GameMasterSettings? settings = null) =>
        Load(
            GridKeys.Tracks,
            GridColumnLayout.GetDefaultCustomizableOrder(TracksColumnSet.All),
            settings);
}
