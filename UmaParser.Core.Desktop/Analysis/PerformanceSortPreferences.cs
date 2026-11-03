using UmaParser.MasterData;

namespace UmaParser.Analysis;

/// <summary>
/// Reads and writes the Performance sort in the shared settings file.
/// Other keys in that file are left as they were.
/// </summary>
internal static class PerformanceSortPreferences
{
    public static (PerformanceSortKey Key, bool Ascending) Load(string? path)
    {
        GameMasterSettings settings = GameMasterSettingsStore.Load(path);
        return (PerformanceSortSetting.Parse(settings.PerformanceSort), PerformanceSortSetting.AscendingOrDefault(settings.PerformanceSortAscending));
    }

    public static void Save(string? path, PerformanceSortKey key, bool ascending)
    {
        GameMasterSettings settings = GameMasterSettingsStore.Load(path);
        settings.PerformanceSort = PerformanceSortSetting.Format(key);
        settings.PerformanceSortAscending = ascending;
        settings.Save(path);
    }
}
