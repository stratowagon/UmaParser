using System.Text.Json;
using UmaParser.Settings;

namespace UmaParser.MasterData;

internal sealed class GameMasterSettings
{
    /// <summary>When set, path to <c>master.mdb</c> or its containing <c>master</c> folder.</summary>
    public string? CustomMasterDbPath { get; set; }

    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }

    /// <summary>Normal or Maximized. Both apps read and write this string.</summary>
    public string? WindowState { get; set; }

    /// <summary>Appearance: System, Light, or Dark. Null, empty, or unknown = System.</summary>
    public string? ColorMode { get; set; }

    /// <summary>Performance tab sort: Average, Median, Max, P75, or P90. Null or unknown = Average.</summary>
    public string? PerformanceSort { get; set; }

    /// <summary>Performance tab direction. Null or true = ascending (weakest first). False = descending.</summary>
    public bool? PerformanceSortAscending { get; set; }

    /// <summary>
    /// Performance tab yellow sample threshold. Null uses 20.
    /// A row is yellow when its race count is at least the red threshold and below this value.
    /// </summary>
    public int? PerformanceSampleSoftThreshold { get; set; }

    /// <summary>
    /// Performance tab red sample threshold. Null uses 10.
    /// A row is red when its race count is below this value.
    /// </summary>
    public int? PerformanceSampleHardThreshold { get; set; }

    public GridColumnPreferencesStore? GridColumns { get; set; }

    public GridColumnGridPreferences GetGridColumnPreferences(string gridKey)
    {
        if (GridColumns == null)
        {
            return new GridColumnGridPreferences();
        }

        return GridColumns.GetOrCreate(gridKey);
    }

    public void SetGridColumnPreferences(string gridKey, GridColumnGridPreferences prefs)
    {
        GridColumns ??= new GridColumnPreferencesStore();
        GridColumns.Grids[gridKey] = prefs;
    }

    /// <param name="path">
    /// Settings file to read. Null uses <see cref="MasterDataPaths.SettingsFilePath"/>,
    /// the file both desktop apps share.
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
    public void Save(string? path = null)
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
        File.WriteAllText(file, JsonSerializer.Serialize(this, options));
    }

    public void ClearCustomPath(string? path = null)
    {
        CustomMasterDbPath = null;
        Save(path);
    }

    public void SetCustomPath(string path, string? settingsPath = null)
    {
        CustomMasterDbPath = path;
        Save(settingsPath);
    }
}