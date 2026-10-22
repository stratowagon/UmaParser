using UmaParser.Core.Tests.TestSupport;
using UmaParser.MasterData;
using Xunit;

namespace UmaParser.Core.Tests.MasterData;

public sealed class EmbeddedCatalogTests
{
    [Fact]
    public void Names_come_from_the_embedded_fallback_only()
    {
        EmbeddedMaster.Ensure();
        GameMasterService current = GameMasterService.Current;
        Assert.Equal(MasterDataSourceKind.Embedded, current.PrimarySource);
        Assert.Null(current.ResolvedDatabasePath);
        Assert.True(current.Catalog.SectionCounts[MasterTextCategory.CharaShortName] > 100);
        Assert.True(current.Catalog.SectionCounts[MasterTextCategory.SkillName] > 100);
    }
}
