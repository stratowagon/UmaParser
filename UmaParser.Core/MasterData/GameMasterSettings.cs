using System.Text.Json;

namespace UmaBlobber.MasterData;

internal sealed class GameMasterSettings
{
    /// <summary>When set, path to <c>master.mdb</c> or its containing <c>master</c> folder.</summary>
    public string? CustomMasterDbPath { get; set; }

    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }

    /// <summary><see cref="FormWindowState"/> name, e.g. Normal or Maximized.</summary>
    public string? WindowState { get; set; }

    /// <summary>Appearance: System, Light, or Dark. Null/empty = System.</summary>
    public string? ColorMode { get; set; }

    public static GameMasterSettings Load()
    {
        MasterDataPaths.EnsureAppDataFolder();
        if (!File.Exists(MasterDataPaths.SettingsFilePath))
        {
            return new GameMasterSettings();
        }

        try
        {
            string json = File.ReadAllText(MasterDataPaths.SettingsFilePath);
            return JsonSerializer.Deserialize<GameMasterSettings>(json) ?? new GameMasterSettings();
        }
        catch
        {
            return new GameMasterSettings();
        }
    }

    public void Save()
    {
        MasterDataPaths.EnsureAppDataFolder();
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(MasterDataPaths.SettingsFilePath, JsonSerializer.Serialize(this, options));
    }

    public void ClearCustomPath()
    {
        CustomMasterDbPath = null;
        Save();
    }

    public void SetCustomPath(string path)
    {
        CustomMasterDbPath = path;
        Save();
    }
}