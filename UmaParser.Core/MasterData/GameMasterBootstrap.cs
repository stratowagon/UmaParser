namespace UmaParser.MasterData;

/// <summary>
/// Browser-safe master data for a future web host. Call <see cref="UseEmbeddedCatalog"/>
/// at startup, or <see cref="TryUseCacheJson"/> with an uploaded <c>master-cache.json</c>.
/// Import captures with <see cref="Import.CaptureImportService.TryImport"/> (file name plus JSON text).
/// Reference <c>UmaParser.Core</c> only. The WPF app uses <c>UmaParser.Core.Desktop</c> on top of Core
/// so it can open <c>master.mdb</c>, <c>settings.json</c>, and path-based drops.
/// </summary>
public static class GameMasterBootstrap
{
    /// <summary>
    /// Points the shared catalog at the built-in names.
    /// Does not read AppData, a live <c>master.mdb</c>, or a cache file.
    /// </summary>
    public static void UseEmbeddedCatalog() =>
        GameMasterService.Current.InitializeEmbeddedOnly();

    /// <summary>
    /// Replaces the shared catalog with a master-cache JSON document, the same shape the
    /// desktop app writes to <c>master-cache.json</c>. Does not read a file. Invalid or empty
    /// JSON leaves the current catalog unchanged.
    /// </summary>
    public static bool TryUseCacheJson(string json, out string? error)
    {
        var catalog = new GameMasterCatalog();
        if (!MasterCacheJson.TryImport(json, catalog, out error))
        {
            return false;
        }

        GameMasterService.Current.SetLastError(null);
        GameMasterService.Current.Apply(
            catalog,
            MasterDataSourceKind.Cache,
            resolvedPath: null,
            statusNotice: null);
        return true;
    }
}
