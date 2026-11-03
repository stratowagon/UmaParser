using UmaParser.MasterData;

namespace UmaParser.Analysis;

/// <summary>
/// Reads the Performance sample thresholds from the shared settings file.
/// Other keys in that file are left as they were. Writing goes through Options, which loads the same object.
/// </summary>
internal static class PerformanceSamplePreferences
{
    public static PerformanceSampleThresholds Load(string? path)
    {
        GameMasterSettings settings = GameMasterSettingsStore.Load(path);
        return PerformanceSampleThresholds.FromSaved(
            settings.PerformanceSampleSoftThreshold,
            settings.PerformanceSampleHardThreshold);
    }
}
