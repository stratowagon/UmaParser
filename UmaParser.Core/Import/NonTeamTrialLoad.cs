using UmaParser.DataModel.RaceScenario;
using UmaParser.ObjectModel;

namespace UmaParser.Import;

/// <summary>
/// Local-player umas from Champions Meet, Room Match, and Practice captures.
/// This is the list the Skills and Tracks selectors show when the drop is not a uniform Team Trials roster.
/// </summary>
public sealed class NonTeamTrialLoad
{
    public required IReadOnlyList<SingleRaceParticipants> Races { get; init; }

    /// <summary>Distinct local umas, ordered by short name, then higher rank score first.</summary>
    public required IReadOnlyList<UmaIdentity> Identities { get; init; }

    /// <summary>Simulation blobs that parsed. A bad blob is skipped and does not fail the load.</summary>
    public int ParsedSimulationCount { get; init; }

    /// <summary>Sum of <c>HorseNum</c> across the simulations that parsed.</summary>
    public int TotalHorseSlots { get; init; }

    public static NonTeamTrialLoad Collect(IEnumerable<CaptureImportResult> singleRaceImports)
    {
        var races = new List<SingleRaceParticipants>();
        var identities = new List<UmaIdentity>();
        int totalHorseSlots = 0;
        int parsedSimulations = 0;

        foreach (CaptureImportResult import in singleRaceImports)
        {
            if (!string.IsNullOrWhiteSpace(import.SingleRaceSimDataBase64))
            {
                try
                {
                    RaceScenarioData simulation = RaceScenarioParser.Parse(import.SingleRaceSimDataBase64);
                    totalHorseSlots += simulation.HorseNum;
                    parsedSimulations++;
                }
                catch
                {
                    // A bad blob only affects the status counts.
                }
            }

            if (!string.IsNullOrWhiteSpace(import.SingleRaceNormalizedJson))
            {
                try
                {
                    SingleRaceParticipants participants = SingleRaceParticipantExtractor.ExtractLocalPlayerParticipants(
                        import.SingleRaceNormalizedJson,
                        import.SingleRaceSimDataBase64);
                    races.Add(participants);

                    foreach ((UmaIdentity identity, _) in participants.LocalPlayerHorses)
                    {
                        if (!identities.Any(existing => existing.IsSameUma(identity)))
                        {
                            identities.Add(identity);
                        }
                    }
                }
                catch
                {
                    // Best effort, matching the previous form behaviour.
                }
            }
        }

        List<UmaIdentity> ordered = identities
            .OrderBy(identity => identity.ShortName)
            .ThenByDescending(identity => identity.RankScore)
            .ToList();

        return new NonTeamTrialLoad
        {
            Races = races,
            Identities = ordered,
            ParsedSimulationCount = parsedSimulations,
            TotalHorseSlots = totalHorseSlots,
        };
    }
}
