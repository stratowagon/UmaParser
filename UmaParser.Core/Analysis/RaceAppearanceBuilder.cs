using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.ObjectModel;

namespace UmaParser.Analysis;

/// <summary>
/// Builds the race list the Skills and Tracks tabs used to assemble inline for
/// Champions Meet, Room Match, and Practice captures.
/// Each appearance carries an empty <see cref="RaceResult"/> because those captures
/// are not Team Trials results. The simulation and the horse come from the capture.
/// </summary>
public static class RaceAppearanceBuilder
{
    public static List<RaceAppearance> ForSkills(
        IEnumerable<SingleRaceParticipants> races,
        UmaIdentity uma)
    {
        var appearances = new List<RaceAppearance>();
        foreach (SingleRaceParticipants race in races)
        {
            foreach ((UmaIdentity identity, RaceHorseData horse) in race.LocalPlayerHorses)
            {
                if (uma.IsSameUma(identity))
                {
                    appearances.Add(new RaceAppearance(new RaceResult(), race.Simulation, horse));
                }
            }
        }

        return appearances;
    }

    public static List<(RaceAppearance Appearance, int Distance, string TrackName)> ForTracks(
        IEnumerable<SingleRaceParticipants> races,
        UmaIdentity uma)
    {
        var items = new List<(RaceAppearance Appearance, int Distance, string TrackName)>();
        foreach (SingleRaceParticipants race in races)
        {
            foreach ((UmaIdentity identity, RaceHorseData horse) in race.LocalPlayerHorses)
            {
                if (uma.IsSameUma(identity))
                {
                    var appearance = new RaceAppearance(new RaceResult(), race.Simulation, horse);
                    items.Add((appearance, race.Distance, race.TrackName));
                }
            }
        }

        return items;
    }
}
