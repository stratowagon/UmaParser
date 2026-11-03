using System.Reflection;
using UmaParser.Import;
using UmaParser.MasterData;
using Xunit;

namespace UmaParser.Core.Tests.MasterData;

/// <summary>
/// The public boot path a web host can call without a master.mdb, AppData, or InternalsVisibleTo.
/// Restores the embedded catalog so later tests still see built-in names.
/// </summary>
public sealed class BrowserMasterDataTests
{
    [Fact]
    public void Embedded_catalog_and_cache_json_do_not_read_a_file()
    {
        try
        {
            GameMasterBootstrap.UseEmbeddedCatalog();
            Assert.Equal(MasterDataSourceKind.Embedded, GameMasterService.Current.PrimarySource);
            Assert.Contains("built-in names", GameMasterBootstrap.StatusLine(), StringComparison.Ordinal);
            Assert.Equal("Special Week", GameMasterService.Current.Catalog.FormatCharaShortName(1001));

            Assert.False(GameMasterBootstrap.TryUseCacheJson("{", out string? invalidError));
            Assert.False(string.IsNullOrEmpty(invalidError));
            Assert.Equal("Special Week", GameMasterService.Current.Catalog.FormatCharaShortName(1001));

            const string cacheJson = """{"Sections":{"CharaShortName":{"1001":"From Cache"}}}""";
            Assert.True(GameMasterBootstrap.TryUseCacheJson(cacheJson, out string? error));
            Assert.Null(error);
            Assert.Equal(MasterDataSourceKind.Cache, GameMasterService.Current.PrimarySource);
            Assert.Contains("cached master data", GameMasterBootstrap.StatusLine(), StringComparison.Ordinal);
            Assert.Null(GameMasterService.Current.ResolvedDatabasePath);
            Assert.Equal("From Cache", GameMasterService.Current.Catalog.FormatCharaShortName(1001));

            CaptureImportResult imported = CaptureImportService.TryImport("empty.json", jsonText: null);
            Assert.Equal(ImportStatus.EmptyContent, imported.Status);
        }
        finally
        {
            GameMasterBootstrap.UseEmbeddedCatalog();
        }
    }

    [Fact]
    public void Core_does_not_reference_sqlite()
    {
        string[] names = typeof(CaptureImportService).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray()!;
        Assert.DoesNotContain("Microsoft.Data.Sqlite", names);
    }
}
