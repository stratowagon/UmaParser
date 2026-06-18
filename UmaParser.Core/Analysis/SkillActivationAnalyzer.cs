using UmaBlobber.DataModel.RaceScenario;
using UmaBlobber.DataModel.ResponseData;
using UmaBlobber.MasterData;
using UmaBlobber.ObjectModel;

namespace UmaBlobber.Analysis;

public static class SkillActivationAnalyzer
{
    /// <summary>Activation rate below this (per race) is highlighted as low.</summary>
    public const double LowActivationThresholdPercent = 50.0;

    /// <summary>Per-race rate must be at least this many points below wit expectation to flag.</summary>
    public const double UnderperformVsWitGapPercent = 12.0;

    /// <summary>Minimum races before wit underperformance highlighting.</summary>
    public const int MinRacesForWitUnderperform = 5;

    public static SkillActivationReport Analyze(
        IEnumerable<TeamTrialResult> trials,
        int trainedCharaId,
        string umaName)
    {
        var trialList = trials.ToList();
        int raceCount = 0;
        var activationCounts = new Dictionary<int, int>();
        var racesWithActivation = new Dictionary<int, int>();
        var pointsEarned = new Dictionary<int, double>();
        var knownSkills = new HashSet<int>();
        int witSum = 0;

        foreach (var trial in trialList)
        {
            foreach (var appearance in trial.GetAppearances(trainedCharaId))
            {
                raceCount++;
                witSum += appearance.Horse.Wiz;

                foreach (var skill in appearance.Horse.SkillArray)
                {
                    knownSkills.Add(skill.SkillId);
                }

                var activatedThisRace = new HashSet<int>();
                foreach (var skillId in CountActivations(appearance))
                {
                    activationCounts.TryGetValue(skillId, out int count);
                    activationCounts[skillId] = count + 1;
                    activatedThisRace.Add(skillId);
                }

                foreach (int skillId in activatedThisRace)
                {
                    racesWithActivation.TryGetValue(skillId, out int raceHits);
                    racesWithActivation[skillId] = raceHits + 1;
                }

                foreach (var (skillId, points) in SkillRaceScoring.ScoreActivations(appearance))
                {
                    pointsEarned.TryGetValue(skillId, out double total);
                    pointsEarned[skillId] = total + points;
                }
            }
        }

        int averageWit = raceCount > 0 ? witSum / raceCount : 0;
        double expectedWitForUma = WitSkillActivationChance.PercentFromBaseWit(averageWit);
        var catalog = GameMasterService.Current.Catalog;
        var rows = new List<SkillActivationRow>();

        foreach (int skillId in knownSkills.OrderBy(id => id))
        {
            int activations = activationCounts.GetValueOrDefault(skillId);
            double totalPoints = pointsEarned.GetValueOrDefault(skillId);
            int racesHit = racesWithActivation.GetValueOrDefault(skillId);
            double rate = raceCount > 0 ? activations * 100.0 / raceCount : 0;
            double pointsPerRace = raceCount > 0 ? totalPoints / raceCount : 0;
            double perRaceRate = raceCount > 0 ? racesHit * 100.0 / raceCount : 0;
            var lot = catalog.GetSkillActivateLot(skillId);
            double? expectedWit = lot == SkillActivateLotKind.Wit
                ? expectedWitForUma
                : null;
            double? witDelta = expectedWit.HasValue ? perRaceRate - expectedWit.Value : null;
            bool underVsWit = expectedWit.HasValue
                && raceCount >= MinRacesForWitUnderperform
                && witDelta!.Value <= -UnderperformVsWitGapPercent;

            rows.Add(new SkillActivationRow
            {
                SkillId = skillId,
                SkillName = catalog.FormatSkillName(skillId),
                ActivationCount = activations,
                TotalPointsEarned = totalPoints,
                PointsPerRace = pointsPerRace,
                RaceCount = raceCount,
                ActivationRatePercent = rate,
                PerRaceActivationRatePercent = perRaceRate,
                ActivateLot = lot,
                ExpectedWitPercent = expectedWit,
                WitDeltaPercent = witDelta,
                IsLowActivation = raceCount > 0 && perRaceRate < LowActivationThresholdPercent,
                IsUnderperformingVsWit = underVsWit,
            });
        }

        rows.Sort((a, b) =>
        {
            int cmp = b.PointsPerRace.CompareTo(a.PointsPerRace);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = a.PerRaceActivationRatePercent.CompareTo(b.PerRaceActivationRatePercent);
            return cmp != 0 ? cmp : string.Compare(a.SkillName, b.SkillName, StringComparison.Ordinal);
        });

        return new SkillActivationReport
        {
            TrainedCharaId = trainedCharaId,
            UmaName = umaName,
            TrialCount = trialList.Count,
            RaceCount = raceCount,
            AverageWit = averageWit,
            ExpectedWitPercentForUma = expectedWitForUma,
            HasSkillLotMetadata = catalog.SkillActivateLotCount > 0,
            Rows = rows,
        };
    }

    /// <summary>
    /// Version for non-TT single-race data (CM, Room, Practice, etc.).
    /// Each RaceAppearance represents one race the uma participated in.
    /// TT-specific scoring (points) will naturally be zero/empty.
    /// </summary>
    public static SkillActivationReport Analyze(
        IEnumerable<RaceAppearance> appearances,
        string umaName)
    {
        var appList = appearances.ToList();
        int raceCount = 0;
        var activationCounts = new Dictionary<int, int>();
        var racesWithActivation = new Dictionary<int, int>();
        var pointsEarned = new Dictionary<int, double>();
        var knownSkills = new HashSet<int>();
        int witSum = 0;

        foreach (var appearance in appList)
        {
            raceCount++;
            witSum += appearance.Horse.Wiz;

            foreach (var skill in appearance.Horse.SkillArray)
            {
                knownSkills.Add(skill.SkillId);
            }

            var activatedThisRace = new HashSet<int>();
            foreach (var skillId in CountActivations(appearance))
            {
                activationCounts.TryGetValue(skillId, out int count);
                activationCounts[skillId] = count + 1;
                activatedThisRace.Add(skillId);
            }

            foreach (int skillId in activatedThisRace)
            {
                racesWithActivation.TryGetValue(skillId, out int raceHits);
                racesWithActivation[skillId] = raceHits + 1;
            }

            foreach (var (skillId, points) in SkillRaceScoring.ScoreActivations(appearance))
            {
                pointsEarned.TryGetValue(skillId, out double total);
                pointsEarned[skillId] = total + points;
            }
        }

        int averageWit = raceCount > 0 ? witSum / raceCount : 0;
        double expectedWitForUma = WitSkillActivationChance.PercentFromBaseWit(averageWit);
        var catalog = GameMasterService.Current.Catalog;
        var rows = new List<SkillActivationRow>();

        foreach (int skillId in knownSkills.OrderBy(id => id))
        {
            int activations = activationCounts.GetValueOrDefault(skillId);
            double totalPoints = pointsEarned.GetValueOrDefault(skillId);
            int racesHit = racesWithActivation.GetValueOrDefault(skillId);
            double rate = raceCount > 0 ? activations * 100.0 / raceCount : 0;
            double pointsPerRace = raceCount > 0 ? totalPoints / raceCount : 0;
            double perRaceRate = raceCount > 0 ? racesHit * 100.0 / raceCount : 0;
            var lot = catalog.GetSkillActivateLot(skillId);
            double? expectedWit = lot == SkillActivateLotKind.Wit
                ? expectedWitForUma
                : null;
            double? witDelta = expectedWit.HasValue ? perRaceRate - expectedWit.Value : null;
            bool underVsWit = expectedWit.HasValue
                && raceCount >= MinRacesForWitUnderperform
                && witDelta!.Value <= -UnderperformVsWitGapPercent;

            rows.Add(new SkillActivationRow
            {
                SkillId = skillId,
                SkillName = catalog.FormatSkillName(skillId),
                ActivationCount = activations,
                TotalPointsEarned = totalPoints,
                PointsPerRace = pointsPerRace,
                RaceCount = raceCount,
                ActivationRatePercent = rate,
                PerRaceActivationRatePercent = perRaceRate,
                ActivateLot = lot,
                ExpectedWitPercent = expectedWit,
                WitDeltaPercent = witDelta,
                IsLowActivation = raceCount > 0 && perRaceRate < LowActivationThresholdPercent,
                IsUnderperformingVsWit = underVsWit,
            });
        }

        rows.Sort((a, b) =>
        {
            int cmp = b.PointsPerRace.CompareTo(a.PointsPerRace);
            if (cmp != 0)
            {
                return cmp;
            }

            cmp = a.PerRaceActivationRatePercent.CompareTo(b.PerRaceActivationRatePercent);
            return cmp != 0 ? cmp : string.Compare(a.SkillName, b.SkillName, StringComparison.Ordinal);
        });

        return new SkillActivationReport
        {
            TrainedCharaId = 0,
            UmaName = umaName,
            TrialCount = appList.Count, // number of source races/files for this uma
            RaceCount = raceCount,
            AverageWit = averageWit,
            ExpectedWitPercentForUma = expectedWitForUma,
            HasSkillLotMetadata = catalog.SkillActivateLotCount > 0,
            Rows = rows,
        };
    }

    private static IEnumerable<int> CountActivations(RaceAppearance appearance)
    {
        int gate = appearance.Horse.FrameOrder;

        foreach (var skill in appearance.Simulation.SkillEvents)
        {
            if (MatchesGate(gate, skill.HorseIndex))
            {
                yield return skill.SkillId;
            }
        }
    }

    /// <summary>
    /// Skill events use 0-based horse index in gate order; <see cref="RaceHorseData.FrameOrder"/> is 1-based gate.
    /// </summary>
    private static bool MatchesGate(int frameOrder, int horseParam)
    {
        return frameOrder > 0 && horseParam == frameOrder - 1;
    }
}