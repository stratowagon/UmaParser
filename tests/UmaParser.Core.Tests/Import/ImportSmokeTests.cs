using UmaParser.Core.Tests.TestSupport;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Import;

public sealed class ImportSmokeTests
{
    public static IEnumerable<object[]> TeamTrialFiles() =>
        FixtureCatalog.TeamTrials.Select(entry => new object[] { entry.RelativePath });

    public static IEnumerable<object[]> SingleRaceFiles() =>
        Expectations.Select(row => new object[]
        {
            row.FileName, row.RaceType, row.Horses, row.LocalHorses, row.Distance, row.Track,
        });

    [Theory]
    [MemberData(nameof(TeamTrialFiles))]
    public void TeamTrial_imports_a_full_roster(string relativePath)
    {
        FixtureEntry entry = FixtureCatalog.Require(relativePath);
        CaptureImportResult result = FixtureCatalog.Import(entry);

        Assert.Equal(ImportStatus.Success, result.Status);
        Assert.Equal("TeamTrialResult", result.ResponseTypeName);
        var trial = Assert.IsType<TeamTrialResult>(result.Response);
        Assert.NotNull(trial.Data);
        Assert.Equal(5, trial.Data.RaceResultArray.Count);
        Assert.Equal(5, trial.Data.RaceStartParamsArray.Count);
        Assert.Equal(15, trial.RaceRoster.Count);
        Assert.Equal(15, trial.RosterNames.Count);
        Assert.True(trial.TotalScore > 0, entry.FileName);
        Assert.All(trial.RosterNames, name => Assert.False(name.StartsWith('#'), name));

        foreach (RaceStartParams start in trial.Data.RaceStartParamsArray)
        {
            Assert.Equal(3, start.RaceHorseDataArray.Count(horse => horse.TeamId == 1));
        }
    }

    [Theory]
    [MemberData(nameof(SingleRaceFiles))]
    public void SingleRace_imports_with_the_expected_field(
        string fileName,
        string raceType,
        int horses,
        int localHorses,
        int distance,
        string track)
    {
        FixtureEntry entry = FixtureCatalog.All.Single(item => item.FileName == fileName);
        CaptureImportResult result = FixtureCatalog.Import(entry);

        Assert.Equal(ImportStatus.Success, result.Status);
        Assert.Equal("SingleRaceCapture", result.ResponseTypeName);
        Assert.True(result.IsSingleRace);
        Assert.Equal(raceType, result.SingleRaceType);

        SingleRaceParticipants parts = SingleRaceParticipantExtractor.ExtractLocalPlayerParticipants(
            result.SingleRaceNormalizedJson!,
            result.SingleRaceSimDataBase64);
        Assert.Equal(horses, parts.Simulation.HorseNum);
        Assert.Equal(localHorses, parts.LocalPlayerHorses.Count);
        Assert.Equal(distance, parts.Distance);
        Assert.Equal(track, parts.TrackName);
        Assert.All(parts.LocalPlayerHorses, horse =>
            Assert.False(horse.Identity.GetDisplayName().StartsWith('#'), horse.Identity.GetDisplayName()));
    }

    [Fact]
    public void SupportsJson_is_rejected_as_invalid_format()
    {
        FixtureEntry entry = FixtureCatalog.Require("NonRace/supports.json");
        CaptureImportResult result = FixtureCatalog.Import(entry);

        Assert.Equal(ImportStatus.InvalidFormat, result.Status);
        Assert.False(result.IsTeamTrial);
        Assert.False(result.IsSingleRace);
        Assert.Null(result.Response);
    }

    [Fact]
    public void Manifest_lists_every_committed_fixture()
    {
        Assert.Equal(56, FixtureCatalog.All.Count);
        foreach (FixtureEntry entry in FixtureCatalog.All)
        {
            string path = FixtureCatalog.FullPath(entry);
            Assert.True(File.Exists(path), entry.RelativePath);
            Assert.Equal(entry.ScrubbedSizeBytes, new FileInfo(path).Length);
            Assert.False(string.IsNullOrWhiteSpace(entry.OriginalPath));
            Assert.False(string.IsNullOrWhiteSpace(entry.OriginalLastWritePacific));
            Assert.True(entry.OriginalSizeKb > 0);
        }

        var onDisk = Directory.GetFiles(FixtureCatalog.Root, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(FixtureCatalog.Root, path).Replace('\\', '/'))
            .Where(path => !path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var listed = FixtureCatalog.All
            .Select(entry => entry.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(listed, onDisk);
    }

    static readonly SingleRaceExpectation[] Expectations =
    [
        new("CM12-Maruzensky-95.3945s-20260423.json", "Champions", 9, 3, 2000, "Nakayama"),
        new("CM16-Air Groove-55.6789s-20260718.json", "Champions", 9, 3, 1200, "Nakayama"),
        new("CM20-Mihono Bourbon-121.4182s-20261010.json", "Champions", 9, 3, 2500, "Nakayama"),
        new("RoomMatch-Agnes Digital-95.1591s-20260805.json", "RoomMatch", 9, 3, 2000, "Oi"),
        new("Practice-Special Week-105.3822s-20260908.json", "Practice", 9, 9, 2200, "Kyoto"),
        new("Career-Narita Taishin-96.3071s-20261016.json", "Single", 18, 1, 2000, "Tokyo"),
        new("Legend-Special Week-121.8110s-20261021.json", "Legend", 16, 1, 2500, "Nakayama"),
    ];

    sealed record SingleRaceExpectation(
        string FileName,
        string RaceType,
        int Horses,
        int LocalHorses,
        int Distance,
        string Track);
}
