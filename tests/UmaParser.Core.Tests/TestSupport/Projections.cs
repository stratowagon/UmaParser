using UmaParser.Analysis;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.ObjectModel;

namespace UmaParser.Core.Tests.TestSupport;

internal sealed class SkillSnapshot
{
    public required string UmaName { get; init; }
    public int TrainedCharaId { get; init; }
    public int TrialCount { get; init; }
    public int RaceCount { get; init; }
    public int AverageWit { get; init; }
    public required IReadOnlyList<SkillSnapshotRow> Rows { get; init; }
}

internal sealed class SkillSnapshotRow
{
    public int SkillId { get; init; }
    public required string SkillName { get; init; }
    public int Activations { get; init; }
    public double AvgPoints { get; init; }
    public double? WitDelta { get; init; }
}

internal sealed class TracksGroupSnapshot
{
    public required string Group { get; init; }
    public required IReadOnlyList<TracksReport> Umas { get; init; }
}

internal sealed class SkillsGroupSnapshot
{
    public required string Group { get; init; }
    public required IReadOnlyList<SkillSnapshot> Umas { get; init; }
}

internal sealed class SingleRaceSnapshot
{
    public required string FileName { get; init; }
    public required string RaceType { get; init; }
    public int Horses { get; init; }
    public int LocalHorses { get; init; }
    public int Distance { get; init; }
    public required string Track { get; init; }
    public required IReadOnlyList<SingleRaceUmaSnapshot> Umas { get; init; }
}

internal sealed class SingleRaceUmaSnapshot
{
    public required string Name { get; init; }
    public int TrainedCharaId { get; init; }
    public int CharaId { get; init; }
    public int RankScore { get; init; }
    public required SkillSnapshot Skills { get; init; }
    public required TracksReport Tracks { get; init; }
}

internal static class Projections
{
    public static SkillSnapshot Skills(SkillActivationReport report) => new()
    {
        UmaName = report.UmaName,
        TrainedCharaId = report.TrainedCharaId,
        TrialCount = report.TrialCount,
        RaceCount = report.RaceCount,
        AverageWit = report.AverageWit,
        Rows = report.Rows.Select(row => new SkillSnapshotRow
        {
            SkillId = row.SkillId,
            SkillName = row.SkillName,
            Activations = row.ActivationCount,
            AvgPoints = row.PointsPerRace,
            WitDelta = row.WitDeltaPercent,
        }).ToList(),
    };

    public static TracksGroupSnapshot TracksFor(string group, IReadOnlyList<TeamTrialResult> trials)
    {
        var layout = trials[^1];
        IReadOnlyList<int> ids = layout.RosterTrainedCharaIds;
        IReadOnlyList<string> names = layout.RosterNames;
        var umas = new List<TracksReport>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            umas.Add(TracksAnalyzer.Analyze(trials, ids[i], names[i]));
        }

        return new TracksGroupSnapshot { Group = group, Umas = umas };
    }

    public static SkillsGroupSnapshot SkillsFor(string group, IReadOnlyList<TeamTrialResult> trials)
    {
        var layout = trials[^1];
        IReadOnlyList<int> ids = layout.RosterTrainedCharaIds;
        IReadOnlyList<string> names = layout.RosterNames;
        var umas = new List<SkillSnapshot>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            umas.Add(Skills(SkillActivationAnalyzer.Analyze(trials, ids[i], names[i])));
        }

        return new SkillsGroupSnapshot { Group = group, Umas = umas };
    }

    /// <summary>
    /// Same appearance the Skills and Tracks tabs build for a non-team-trial capture:
    /// a dummy <see cref="RaceResult"/> plus the parsed simulation and the local horse.
    /// </summary>
    public static SingleRaceSnapshot SingleRace(CaptureImportResult import)
    {
        SingleRaceParticipants parts = SingleRaceParticipantExtractor.ExtractLocalPlayerParticipants(
            import.SingleRaceNormalizedJson ?? "",
            import.SingleRaceSimDataBase64);
        var umas = new List<SingleRaceUmaSnapshot>(parts.LocalPlayerHorses.Count);
        foreach ((UmaIdentity identity, RaceHorseData horse) in parts.LocalPlayerHorses)
        {
            var appearance = new RaceAppearance(new RaceResult(), parts.Simulation, horse);
            string name = identity.GetDisplayName();
            umas.Add(new SingleRaceUmaSnapshot
            {
                Name = name,
                TrainedCharaId = identity.TrainedCharaId,
                CharaId = identity.CharaId,
                RankScore = identity.RankScore,
                Skills = Skills(SkillActivationAnalyzer.Analyze(new[] { appearance }, name)),
                Tracks = TracksAnalyzer.Analyze(new[] { (appearance, parts.Distance, parts.TrackName) }, name),
            });
        }

        return new SingleRaceSnapshot
        {
            FileName = import.FileName,
            RaceType = import.SingleRaceType ?? "",
            Horses = parts.Simulation.HorseNum,
            LocalHorses = parts.LocalPlayerHorses.Count,
            Distance = parts.Distance,
            Track = parts.TrackName,
            Umas = umas,
        };
    }
}
