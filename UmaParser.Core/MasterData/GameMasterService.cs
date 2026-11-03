namespace UmaParser.MasterData;

internal enum MasterDataSourceKind
{
    None,
    Embedded,
    Cache,
    LiveDatabase,
}

/// <summary>
/// Shared master-data catalog. Desktop startup fills it from <c>master.mdb</c> or the cache
/// via <c>UmaParser.Core.Desktop</c>. A browser host uses <see cref="GameMasterBootstrap"/>.
/// </summary>
internal sealed class GameMasterService
{
    public static GameMasterService Current { get; } = new();

    private GameMasterSettings _settings = new();
    private GameMasterCatalog _catalog = new();
    private MasterDataSourceKind _primarySource = MasterDataSourceKind.None;
    private string? _resolvedDbPath;
    private string? _lastError;
    private string? _statusNotice;
    private string? _cacheFilePath;

    public GameMasterService()
    {
    }

    public GameMasterCatalog Catalog => _catalog;

    public MasterDataSourceKind PrimarySource => _primarySource;

    public string? ResolvedDatabasePath => _resolvedDbPath;

    public string? LastError => _lastError;

    public GameMasterSettings Settings => _settings;

    /// <summary>
    /// Points this service at <see cref="EmbeddedMasterFallback"/> only.
    /// Does not read AppData, a live <c>master.mdb</c>, or the master cache.
    /// </summary>
    internal void InitializeEmbeddedOnly()
    {
        _settings = new GameMasterSettings();
        _resolvedDbPath = null;
        _lastError = null;
        _statusNotice = null;
        _cacheFilePath = null;
        var catalog = new GameMasterCatalog();
        EmbeddedMasterFallback.Apply(catalog);
        _catalog = catalog;
        _primarySource = MasterDataSourceKind.Embedded;
    }

    internal void SetSettings(GameMasterSettings settings) => _settings = settings;

    internal void SetLastError(string? error) => _lastError = error;

    internal void SetCacheFilePath(string cacheFilePath) => _cacheFilePath = cacheFilePath;

    internal void Apply(
        GameMasterCatalog catalog,
        MasterDataSourceKind source,
        string? resolvedPath,
        string? statusNotice)
    {
        _catalog = catalog;
        _primarySource = source;
        _resolvedDbPath = resolvedPath;
        _statusNotice = statusNotice;
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

        // A live path is set only by the desktop host. Embedded and cache boots leave it null.
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
            $"Cache file: {_cacheFilePath ?? "(none)"}",
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
}
