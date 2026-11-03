using System.Text.Json;

namespace UmaParser.MasterData;

/// <summary>
/// Desktop master-data startup: <c>master.mdb</c> when one is available, then the cache file,
/// then the built-in names. Writes the optional cache next to the shared settings file.
/// The catalog itself lives on <see cref="GameMasterService"/> so analysis stays in Core.
/// </summary>
internal sealed class DesktopGameMaster
{
    private static readonly Lazy<DesktopGameMaster> LazyCurrent = new(() => new DesktopGameMaster(
        GameMasterService.Current,
        settingsFilePath: null,
        defaultDbPath: null,
        cacheFilePath: null,
        persistCache: true));

    public static DesktopGameMaster Current => LazyCurrent.Value;

    private readonly GameMasterService _service;
    private readonly string? _settingsFilePath;
    private readonly string? _defaultDbPath;
    private readonly string? _cacheFilePath;
    private readonly bool _persistCache;

    /// <summary>
    /// Reads and writes <paramref name="settingsFilePath"/> instead of AppData, and looks for
    /// the default <c>master.mdb</c> and cache at the given paths. Does not write a cache file.
    /// Tests use this so they do not touch the machine's master data.
    /// </summary>
    internal DesktopGameMaster(string settingsFilePath, string defaultDbPath, string cacheFilePath)
        : this(new GameMasterService(), settingsFilePath, defaultDbPath, cacheFilePath, persistCache: false)
    {
    }

    private DesktopGameMaster(
        GameMasterService service,
        string? settingsFilePath,
        string? defaultDbPath,
        string? cacheFilePath,
        bool persistCache)
    {
        _service = service;
        _settingsFilePath = settingsFilePath;
        _defaultDbPath = defaultDbPath;
        _cacheFilePath = cacheFilePath;
        _persistCache = persistCache;
        _service.SetCacheFilePath(CacheFilePath);
    }

    public GameMasterCatalog Catalog => _service.Catalog;

    public MasterDataSourceKind PrimarySource => _service.PrimarySource;

    public string? ResolvedDatabasePath => _service.ResolvedDatabasePath;

    public string? LastError => _service.LastError;

    public GameMasterSettings Settings => _service.Settings;

    private string DefaultDatabasePath => _defaultDbPath ?? MasterDataPaths.DefaultMasterDbPath;

    private string CacheFilePath => _cacheFilePath ?? MasterDataPaths.CacheFilePath;

    public void Initialize()
    {
        _service.SetSettings(GameMasterSettingsStore.Load(_settingsFilePath));
        Refresh();
    }

    public void Refresh()
    {
        _service.SetLastError(null);
        string? customPath = _service.Settings.CustomMasterDbPath;
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (TryReadDatabase(customPath, out var customCatalog, out string resolved, out string failure))
            {
                Apply(customCatalog, MasterDataSourceKind.LiveDatabase, resolved, statusNotice: null);
                return;
            }

            // Missing or unreadable saved file. Load the default location first (default
            // master.mdb, then the cache, then built-in names). Clear the saved path only
            // after that catalog is applied. If the settings write fails, the file still
            // has the old path and the next launch tries this fallback again.
            LoadDefaultLocation(out var fallbackCatalog, out var fallbackSource, out string defaultPath);
            string notice =
                $"Saved master database could not be loaded ({customPath.Trim()}): {failure}. Using the default location.";
            Apply(fallbackCatalog, fallbackSource, defaultPath, notice);
            _service.Settings.CustomMasterDbPath = null;
            _service.Settings.Save(_settingsFilePath);
            return;
        }

        LoadDefaultLocation(out var catalog, out var source, out string path);
        Apply(catalog, source, path, statusNotice: null);
    }

    public void UseDefaultDatabaseLocation()
    {
        _service.Settings.CustomMasterDbPath = null;
        _service.Settings.Save(_settingsFilePath);
        Refresh();
    }

    /// <summary>
    /// Loads <paramref name="path"/> and, only when that load succeeds, stores it as the custom
    /// master database. A file that is missing or not a master database leaves the current
    /// catalog and the saved path untouched.
    /// </summary>
    public bool TrySetCustomDatabasePath(string path)
    {
        if (!TryReadDatabase(path, out var catalog, out string resolved, out string failure))
        {
            _service.SetLastError(failure);
            return false;
        }

        _service.Settings.CustomMasterDbPath = path.Trim();
        _service.Settings.Save(_settingsFilePath);
        _service.SetLastError(null);
        Apply(catalog, MasterDataSourceKind.LiveDatabase, resolved, statusNotice: null);
        return true;
    }

    public string GetStatusLine() => _service.GetStatusLine();

    public string GetDetailedStatus() => _service.GetDetailedStatus();

    /// <summary>
    /// Loads a chosen file into a new catalog. Does not read or write settings.
    /// A folder is accepted when it contains <c>master.mdb</c>. The default install path is not used.
    /// </summary>
    private static bool TryReadDatabase(
        string? path,
        out GameMasterCatalog catalog,
        out string resolved,
        out string failure)
    {
        catalog = new GameMasterCatalog();
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(path) ||
            !MasterDataPaths.TryResolveCustomMasterDbFile(path, out resolved))
        {
            failure = string.IsNullOrWhiteSpace(path)
                ? "File not found."
                : $"File not found: {path.Trim()}";
            return false;
        }

        EmbeddedMasterFallback.Apply(catalog);
        if (MasterDbReader.TryLoad(resolved, catalog, out string? loadError))
        {
            failure = string.Empty;
            return true;
        }

        catalog = new GameMasterCatalog();
        failure = string.IsNullOrEmpty(loadError) ? "Could not load the selected file." : loadError;
        return false;
    }

    private void LoadDefaultLocation(
        out GameMasterCatalog catalog,
        out MasterDataSourceKind source,
        out string resolvedPath)
    {
        catalog = new GameMasterCatalog();
        EmbeddedMasterFallback.Apply(catalog);
        resolvedPath = DefaultDatabasePath;
        source = MasterDataSourceKind.Embedded;

        if (File.Exists(resolvedPath))
        {
            if (MasterDbReader.TryLoad(resolvedPath, catalog, out string? loadError))
            {
                source = MasterDataSourceKind.LiveDatabase;
                return;
            }

            _service.SetLastError(loadError);
            catalog = new GameMasterCatalog();
            EmbeddedMasterFallback.Apply(catalog);
            if (TryLoadCache(catalog, out _))
            {
                source = MasterDataSourceKind.Cache;
            }

            return;
        }

        if (TryLoadCache(catalog, out _))
        {
            source = MasterDataSourceKind.Cache;
            _service.SetLastError("Master database not found; using cached names.");
            return;
        }

        _service.SetLastError("Master database not found; using built-in names.");
    }

    private void Apply(
        GameMasterCatalog catalog,
        MasterDataSourceKind source,
        string? resolvedPath,
        string? statusNotice)
    {
        _service.Apply(catalog, source, resolvedPath, statusNotice);
        if (_persistCache &&
            source == MasterDataSourceKind.LiveDatabase &&
            !string.IsNullOrEmpty(resolvedPath))
        {
            TryWriteCache(catalog, resolvedPath);
        }
    }

    private bool TryLoadCache(GameMasterCatalog catalog, out string? error)
    {
        error = null;
        string path = CacheFilePath;
        if (!File.Exists(path))
        {
            error = "No cache file.";
            return false;
        }

        try
        {
            return MasterCacheJson.TryImport(File.ReadAllText(path), catalog, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void TryWriteCache(GameMasterCatalog catalog, string sourcePath)
    {
        try
        {
            MasterDataPaths.EnsureAppDataFolder();
            DateTime? lastWrite = File.Exists(sourcePath)
                ? File.GetLastWriteTimeUtc(sourcePath)
                : null;
            var cache = catalog.ExportToCache(sourcePath, lastWrite);
            var options = new JsonSerializerOptions { WriteIndented = false };
            File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(cache, options));
        }
        catch
        {
            // Cache is optional; ignore write failures.
        }
    }
}
