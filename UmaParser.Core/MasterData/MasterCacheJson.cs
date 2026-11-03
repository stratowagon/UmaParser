using System.Text.Json;

namespace UmaParser.MasterData;

/// <summary>
/// Reads a master-cache JSON document into a catalog. The desktop shell reads the file;
/// a browser host can pass the same text without touching a path.
/// </summary>
internal static class MasterCacheJson
{
    public static bool TryImport(string json, GameMasterCatalog catalog, out string? error)
    {
        error = null;
        try
        {
            var cache = JsonSerializer.Deserialize<MasterCacheFile>(json);
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
}
