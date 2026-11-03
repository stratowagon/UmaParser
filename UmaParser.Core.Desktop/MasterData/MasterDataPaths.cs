namespace UmaParser.MasterData;

internal static class MasterDataPaths
{
    public const string AppFolderName = "UmaParser";

    /// <summary>
    /// <c>%UserProfile%\AppData\LocalLow</c> — not under <c>LocalApplicationData</c> (<c>...\Local</c>).
    /// </summary>
    public static string LocalLowFolder
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string? appData = Directory.GetParent(localAppData)?.FullName;
            if (string.IsNullOrEmpty(appData))
            {
                throw new InvalidOperationException(
                    $"Could not resolve LocalLow folder from LocalApplicationData: {localAppData}");
            }

            return Path.Combine(appData, "LocalLow");
        }
    }

    public static string DefaultMasterDbPath =>
        Path.Combine(LocalLowFolder, "Cygames", "Umamusume", "master", "master.mdb");

    public static string DefaultMasterFolder =>
        Path.GetDirectoryName(DefaultMasterDbPath) ?? DefaultMasterDbPath;

    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    public static string SettingsFilePath => Path.Combine(AppDataFolder, "settings.json");

    public static string CacheFilePath => Path.Combine(AppDataFolder, "master-cache.json");

    public static void EnsureAppDataFolder()
    {
        Directory.CreateDirectory(AppDataFolder);
    }

    public static string ResolveMasterDbPath(string? customPath)
    {
        if (TryResolveCustomMasterDbFile(customPath, out string resolved))
        {
            return resolved;
        }

        return DefaultMasterDbPath;
    }

    /// <summary>
    /// Resolves a chosen master file, or a folder that contains <c>master.mdb</c>.
    /// A missing or empty choice does not fall through to <see cref="DefaultMasterDbPath"/>.
    /// </summary>
    public static bool TryResolveCustomMasterDbFile(string? customPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(customPath))
        {
            return false;
        }

        string trimmed = customPath.Trim();
        if (Directory.Exists(trimmed))
        {
            string inFolder = Path.Combine(trimmed, "master.mdb");
            if (File.Exists(inFolder))
            {
                resolvedPath = inFolder;
                return true;
            }

            return false;
        }

        if (File.Exists(trimmed))
        {
            resolvedPath = trimmed;
            return true;
        }

        return false;
    }
}