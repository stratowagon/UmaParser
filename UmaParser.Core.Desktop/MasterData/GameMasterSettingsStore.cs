using System.Text.Json;

namespace UmaParser.MasterData;

/// <summary>
/// Reads and writes <see cref="GameMasterSettings"/> on disk. A null path is the shared
/// <c>settings.json</c> under AppData. A browser host keeps the same object in memory
/// and does not use this type.
/// </summary>
internal static class GameMasterSettingsStore
{
    /// <param name="path">
    /// Settings file to read. Null uses <see cref="MasterDataPaths.SettingsFilePath"/>,
    /// the file the desktop app uses.
    /// </param>
    public static GameMasterSettings Load(string? path = null)
    {
        string file = path ?? MasterDataPaths.SettingsFilePath;
        if (path == null)
        {
            MasterDataPaths.EnsureAppDataFolder();
        }

        if (!File.Exists(file))
        {
            return new GameMasterSettings();
        }

        try
        {
            string json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<GameMasterSettings>(json) ?? new GameMasterSettings();
        }
        catch
        {
            return new GameMasterSettings();
        }
    }

    /// <param name="path">
    /// Settings file to write. Null uses <see cref="MasterDataPaths.SettingsFilePath"/>.
    /// </param>
    public static void Save(this GameMasterSettings settings, string? path = null)
    {
        string file = path ?? MasterDataPaths.SettingsFilePath;
        if (path == null)
        {
            MasterDataPaths.EnsureAppDataFolder();
        }
        else
        {
            string? directory = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(file, JsonSerializer.Serialize(settings, options));
    }
}
