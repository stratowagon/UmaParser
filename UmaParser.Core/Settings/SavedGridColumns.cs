using UmaParser.MasterData;
using UmaParser.Presentation;

namespace UmaParser.Settings;

/// <summary>
/// Reads one grid's saved column order and hidden list from a <see cref="GameMasterSettings"/> object.
/// The desktop shell loads settings.json and passes it in. A null object is unsaved defaults, so this
/// type does not open AppData.
/// </summary>
internal static class SavedGridColumns
{
    public static GridColumnGridPreferences Load(
        string gridKey,
        IReadOnlyList<string> defaultCustomizableOrder,
        GameMasterSettings? settings = null)
    {
        settings ??= new GameMasterSettings();
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
