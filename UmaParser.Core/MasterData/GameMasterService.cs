using System.Text.Json;

namespace UmaBlobber.MasterData;

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

    private GameMasterSettings _settings = new();
    private GameMasterCatalog _catalog = new();
    private MasterDataSourceKind _primarySource = MasterDataSourceKind.None;
    private string? _resolvedDbPath;
    private string? _lastError;

    public GameMasterCatalog Catalog => _catalog;

    public MasterDataSourceKind PrimarySource => _primarySource;

    public string? ResolvedDatabasePath => _resolvedDbPath;

    public string? LastError => _lastError;

    public GameMasterSettings Settings => _settings;

    public void Initialize()
    {
        _settings = GameMasterSettings.Load();
        Refresh();
    }

    public void Refresh()
    {
        _lastError = null;
        _resolvedDbPath = MasterDataPaths.ResolveMasterDbPath(_settings.CustomMasterDbPath);
        var catalog = new GameMasterCatalog();
        MasterDataSourceKind source = MasterDataSourceKind.Embedded;

        if (File.Exists(_resolvedDbPath))
        {
            if (MasterDbReader.TryLoad(_resolvedDbPath, catalog, out var loadError))
            {
                source = MasterDataSourceKind.LiveDatabase;
                TryWriteCache(catalog, _resolvedDbPath);
            }
            else
            {
                _lastError = loadError;
                if (TryLoadCache(catalog, out _))
                {
                    source = MasterDataSourceKind.Cache;
                }
            }
        }
        else if (TryLoadCache(catalog, out _))
        {
            source = MasterDataSourceKind.Cache;
            _lastError = "Master database not found; using cached names.";
        }
        else
        {
            _lastError = "Master database not found; using built-in names.";
        }

        EmbeddedMasterFallback.Apply(catalog);
        _catalog = catalog;
        _primarySource = source;
    }

    public void UseDefaultDatabaseLocation()
    {
        _settings.ClearCustomPath();
        Refresh();
    }

    public bool TrySetCustomDatabasePath(string path)
    {
        string resolved = MasterDataPaths.ResolveMasterDbPath(path);
        if (!File.Exists(resolved))
        {
            _lastError = $"File not found: {resolved}";
            return false;
        }

        _settings.SetCustomPath(path);
        Refresh();
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

        var parts = new List<string>
        {
            $"Names: {source} ({charaCount} umas, {skillCount} skills)"
        };

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
            $"Cache file: {MasterDataPaths.CacheFilePath}",
        };

        foreach (var (category, count) in _catalog.SectionCounts.OrderBy(kv => kv.Key))
        {
            lines.Add($"  {category}: {count} entries");
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

    private static bool TryLoadCache(GameMasterCatalog catalog, out string? error)
    {
        error = null;
        string path = MasterDataPaths.CacheFilePath;
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

    private static void TryWriteCache(GameMasterCatalog catalog, string sourcePath)
    {
        try
        {
            MasterDataPaths.EnsureAppDataFolder();
            DateTime? lastWrite = File.Exists(sourcePath)
                ? File.GetLastWriteTimeUtc(sourcePath)
                : null;
            var cache = catalog.ExportToCache(sourcePath, lastWrite);
            var options = new JsonSerializerOptions { WriteIndented = false };
            File.WriteAllText(MasterDataPaths.CacheFilePath, JsonSerializer.Serialize(cache, options));
        }
        catch
        {
            // Cache is optional; ignore write failures.
        }
    }
}