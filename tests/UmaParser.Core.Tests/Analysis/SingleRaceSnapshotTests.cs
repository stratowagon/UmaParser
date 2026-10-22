using UmaParser.Core.Tests.TestSupport;
using UmaParser.Import;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class SingleRaceSnapshotTests
{
    public static IEnumerable<object[]> Files() =>
        FixtureCatalog.All
            .Where(entry => entry.Group.StartsWith("Single-", StringComparison.Ordinal))
            .Select(entry => new object[] { entry.RelativePath });

    [Theory]
    [MemberData(nameof(Files))]
    public void Participants_skills_and_tracks_match_golden_master(string relativePath)
    {
        FixtureEntry entry = FixtureCatalog.Require(relativePath);
        CaptureImportResult import = FixtureCatalog.Import(entry);
        Assert.Equal(ImportStatus.Success, import.Status);
        string suffix = Path.GetFileNameWithoutExtension(entry.FileName).Replace(' ', '_');
        Snapshot.Match(Projections.SingleRace(import), suffix);
    }
}
