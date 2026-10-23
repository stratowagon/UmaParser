using System.Text.Json;

namespace UmaParser.MasterData;

internal enum MasterDataSourceKind
{
    None,
    Embedded,
    Cache,
    LiveDatabase,
}

internal sealed class GameMasterService
{
    public static GameMasterService Current { get; } = new();

    private readonly string? _settingsFilePath;
    private readonly string? _defaultDbPath;
    private readonly string? _cacheFilePath;
    private readonly bool _persistCache;

    private GameMasterSettings _settings = new();
    private GameMasterCatalog _catalog = new();
    private MasterDataSourceKind _primarySource = MasterDataSourceKind.None;
    private string? _resolvedDbPath;
    private string? _lastError;
    private string? _statusNotice;

    public GameMasterService()
        : this(settingsFilePath: null, defaultDbPath: null, cacheFilePath: null, persistCache: true)
    {
    }

    /// <summary>
    /// Reads and writes <paramref name="settingsFilePath"/> instead of AppData, and looks for
    /// the default <c>master.mdb</c> and cache at the given paths. Does not write a cache file.
    /// Tests use this so they do not touch the machine's master data.
    /// </summary>
    internal GameMasterService(string settingsFilePath, string defaultDbPath, string cacheFilePath)
        : this(settingsFilePath, defaultDbPath, cacheFilePath, persistCache: false)
    {
    }

    private GameMasterService(
        string? settingsFilePath,
        string? defaultDbPath,
        string? cacheFilePath,
        bool persistCache)
    {
        _settingsFilePath = settingsFilePath;
        _defaultDbPath = defaultDbPath;
        _cacheFilePath = cacheFilePath;
        _persistCache = persistCache;
    }

    public GameMasterCatalog Catalog => _catalog;

    public MasterDataSourceKind PrimarySource => _primarySource;

    public string? ResolvedDatabasePath => _resolvedDbPath;

    public string? LastError => _lastError;

    public GameMasterSettings Settings => _settings;

    private string DefaultDatabasePath => _defaultDbPath ?? MasterDataPaths.DefaultMasterDbPath;

    private string CacheFilePath => _cacheFilePath ?? MasterDataPaths.CacheFilePath;

    public void Initialize()
    {
        _settings = GameMasterSettings.Load(_settingsFilePath);
        Refresh();
    }

    /// <summary>
    /// Points <see cref="Current"/> at <see cref="EmbeddedMasterFallback"/> only.
    /// Does not read AppData, a live <c>master.mdb</c>, or the master cache.
    /// Tests call this so character, skill, and track names do not depend on the machine.
    /// </summary>
    internal void InitializeEmbeddedOnly()
    {
        _settings = new GameMasterSettings();
        _resolvedDbPath = null;
        _lastError = null;
        _statusNotice = null;
        var catalog = new GameMasterCatalog();
        EmbeddedMasterFallback.Apply(catalog);
        _catalog = catalog;
        _primarySource = MasterDataSourceKind.Embedded;
    }

    public void Refresh()
    {
        _lastError = null;
        string? customPath = _settings.CustomMasterDbPath;
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
            _settings.ClearCustomPath(_settingsFilePath);
            return;
        }

        LoadDefaultLocation(out var catalog, out var source, out string path);
        Apply(catalog, source, path, statusNotice: null);
    }

    public void UseDefaultDatabaseLocation()
    {
        _settings.ClearCustomPath(_settingsFilePath);
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
            _lastError = failure;
            return false;
        }

        _settings.SetCustomPath(path.Trim(), _settingsFilePath);
        _lastError = null;
        Apply(catalog, MasterDataSourceKind.LiveDatabase, resolved, statusNotice: null);
        return true;
    }

    public string GetStatusLine()
    {
        string source = _primarySource switch
        {
            MasterDataSourceKind.LiveDatabase => "game master DB",
            MasterDataSourceKind.Cache => "cached master data",
            MasterDataSourceKind.Embedded => "built-in names",
            _ => "unknown",
        };

        int charaCount = _catalog.SectionCounts.GetValueOrDefault(MasterTextCategory.CharaShortName);
        int skillCount = _catalog.SectionCounts.GetValueOrDefault(MasterTextCategory.SkillName);
        int raceCourseCount = _catalog.GetAllRaceCourses().Count;

        var parts = new List<string>
        {
            $"Names: {source} ({charaCount} umas, {skillCount} skills)"
        };

        if (raceCourseCount > 0)
        {
            parts.Add($"({raceCourseCount} race courses)");
        }

        if (!string.IsNullOrEmpty(_resolvedDbPath) && _primarySource == MasterDataSourceKind.LiveDatabase)
        {
            try
            {
                var info = new FileInfo(_resolvedDbPath);
                parts.Add($"({info.LastWriteTime:yyyy-MM-dd})");
            }
            catch
            {
                // ignore
            }
        }

        if (!string.IsNullOrEmpty(_statusNotice))
        {
            parts.Add($"— {_statusNotice}");
        }

        if (!string.IsNullOrEmpty(_lastError) && _primarySource != MasterDataSourceKind.LiveDatabase)
        {
            parts.Add($"— {_lastError}");
        }

        return string.Join(" ", parts);
    }

    public string GetDetailedStatus()
    {
        var lines = new List<string>
        {
            GetStatusLine(),
            "",
            $"Resolved path: {_resolvedDbPath ?? "(none)"}",
            $"Custom path setting: {_settings.CustomMasterDbPath ?? "(default)"}",
            $"Cache file: {CacheFilePath}",
        };

        foreach (var (category, count) in _catalog.SectionCounts.OrderBy(kv => kv.Key))
        {
            lines.Add($"  {category}: {count} entries");
        }

        int raceCourseCount = _catalog.GetAllRaceCourses().Count;
        if (raceCourseCount > 0)
        {
            lines.Add($"  RaceCourses: {raceCourseCount} entries (from race_instance/race_course_set + text 35)");
        }

        if (_catalog.SkillActivateLotCount > 0)
        {
            lines.Add($"  skill activate_lot: {_catalog.SkillActivateLotCount} entries");
        }

        if (!string.IsNullOrEmpty(_lastError))
        {
            lines.Add("");
            lines.Add($"Note: {_lastError}");
        }

        return string.Join(Environment.NewLine, lines);
    }

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

            _lastError = loadError;
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
            _lastError = "Master database not found; using cached names.";
            return;
        }

        _lastError = "Master database not found; using built-in names.";
    }

    private void Apply(
        GameMasterCatalog catalog,
        MasterDataSourceKind source,
        string? resolvedPath,
        string? statusNotice)
    {
        _catalog = catalog;
        _primarySource = source;
        _resolvedDbPath = resolvedPath;
        _statusNotice = statusNotice;
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
            var cache = JsonSerializer.Deserialize<MasterCacheFile>(File.ReadAllText(path));
            if (cache?.Sections.Count > 0)
            {
                catalog.ImportFromCache(cache);
                return true;
            }

            error = "Cache file is empty.";
            return false;
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
