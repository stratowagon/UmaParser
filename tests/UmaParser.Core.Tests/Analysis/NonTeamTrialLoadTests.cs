using UmaParser.Analysis;
using UmaParser.Core.Tests.TestSupport;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.ObjectModel;
using Xunit;

namespace UmaParser.Core.Tests.Analysis;

public sealed class NonTeamTrialLoadTests
{
    [Fact]
    public void Single_race_files_collect_distinct_umas_in_selector_order()
    {
        List<CaptureImportResult> imports = FixtureCatalog.InGroup("Single-CM12")
            .Concat(FixtureCatalog.InGroup("Single-Practice"))
            .Select(FixtureCatalog.Import)
            .ToList();
        NonTeamTrialLoad load = NonTeamTrialLoad.Collect(imports);

        Assert.Equal(imports.Count, load.Races.Count);
        Assert.True(load.ParsedSimulationCount > 0);
        Assert.True(load.TotalHorseSlots > 0);
        Assert.NotEmpty(load.Identities);
        Assert.Equal(
            load.Identities.OrderBy(identity => identity.ShortName).ThenByDescending(identity => identity.RankScore).Select(identity => identity.TrainedCharaId),
            load.Identities.Select(identity => identity.TrainedCharaId));

        UmaIdentity first = load.Identities[0];
        List<RaceAppearance> skills = RaceAppearanceBuilder.ForSkills(load.Races, first);
        List<(RaceAppearance Appearance, int Distance, string TrackName)> tracks =
            RaceAppearanceBuilder.ForTracks(load.Races, first);
        Assert.NotEmpty(skills);
        Assert.Equal(skills.Count, tracks.Count);

        int expected = load.Races.Sum(race => race.LocalPlayerHorses.Count(pair => first.IsSameUma(pair.Identity)));
        Assert.Equal(expected, skills.Count);
        Assert.All(skills, appearance =>
        {
            Assert.Equal(0, appearance.Result.DistanceType);
            Assert.Equal(string.Empty, appearance.Result.RaceScenario);
            Assert.True(first.IsSameUma(new UmaIdentity(
                first.OwnerViewerId,
                first.OwnerName,
                appearance.Horse.TrainedCharaId,
                appearance.Horse.CharaId,
                appearance.Horse.CardId,
                first.RankScore)));
        });
        Assert.Equal(skills.Select(appearance => appearance.Horse.TrainedCharaId), tracks.Select(item => item.Appearance.Horse.TrainedCharaId));
        Assert.Equal(load.Races.Where(race => race.LocalPlayerHorses.Any(pair => first.IsSameUma(pair.Identity))).Select(race => race.Distance),
            tracks.Select(item => item.Distance));
    }

    [Fact]
    public void Builder_matches_one_capture_the_way_the_tabs_used_to()
    {
        CaptureImportResult import = FixtureCatalog.Import(FixtureCatalog.InGroup("Single-RoomMatch").Single());
        SingleRaceParticipants participants = SingleRaceParticipantExtractor.ExtractLocalPlayerParticipants(
            import.SingleRaceNormalizedJson ?? "",
            import.SingleRaceSimDataBase64);
        Assert.NotEmpty(participants.LocalPlayerHorses);

        foreach ((UmaIdentity identity, RaceHorseData horse) in participants.LocalPlayerHorses)
        {
            RaceAppearance appearance = Assert.Single(
                RaceAppearanceBuilder.ForSkills(new[] { participants }, identity),
                item => ReferenceEquals(item.Horse, horse));
            Assert.Same(participants.Simulation, appearance.Simulation);
            Assert.Equal(0, appearance.Result.TeamTotalScore);

            (RaceAppearance trackAppearance, int distance, string trackName) = Assert.Single(
                RaceAppearanceBuilder.ForTracks(new[] { participants }, identity),
                item => ReferenceEquals(item.Appearance.Horse, horse));
            Assert.Same(horse, trackAppearance.Horse);
            Assert.Equal(participants.Distance, distance);
            Assert.Equal(participants.TrackName, trackName);
        }
    }
}
