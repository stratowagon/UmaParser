using UmaParser.ObjectModel;

namespace UmaParser.Analysis
{
    public enum RetrainPriorityLevel
    {
        Stable,
        NeedsWork,
        Critical
    }

    /// <summary>Duplicate running style within a 3-uma team.</summary>
    public enum TeamStyleOverlap
    {
        None,
        /// <summary>Two of three share the same style.</summary>
        Pair,
        /// <summary>All three share the same style.</summary>
        FullTeam,
    }

    public sealed class UmaAnalysisRow
    {
        public required string Name { get; init; }
        public required string RunningStyleLabel { get; init; }
        public TeamStyleOverlap StyleOverlap { get; set; }
        public required string Distance { get; init; }
        public required string Role { get; init; }
        public int TrialCount { get; init; }
        public double Average { get; init; }
        public double TrimmedAverage { get; init; }
        public double CoefficientOfVariation { get; init; }
        public double AdjustedCoefficientOfVariation { get; init; }
        public double GapToTop { get; init; }
        public double Ceiling { get; init; }
        public double Floor { get; init; }
        public double CeilingFloorSpread { get; init; }
        public bool IsCurrentAce { get; init; }
        public bool IsSuggestedAce { get; set; }
        public string PotentialAceLabel { get; set; } = string.Empty;

        /// <summary>Trimmed base score difference vs this team's current ace (+ = better than ace).</summary>
        public double AceDelta { get; set; }

        /// <summary>AceDelta as percent of the ace's trimmed average.</summary>
        public double AceDeltaPercent { get; set; }

        /// <summary>Formatted display for the Ace delta column, e.g. "+123 (+2.3%)" or "+0 (+0%)".</summary>
        public string AceDeltaLabel { get; set; } = string.Empty;
        public double RetrainScore { get; init; }
        public RetrainPriorityLevel PriorityLevel { get; init; }
        public required string RetrainPriorityLabel { get; init; }
        public int RosterIndex { get; init; }
        public int TeamIndex { get; init; }
    }

    public sealed class TeamAceRecommendation
    {
        public required string Distance { get; init; }
        public required string CurrentAce { get; init; }
        public required string BestSupport { get; init; }
        public bool SuggestSwap { get; init; }
        public double AceTrimmedAverage { get; init; }
        public double BestSupportTrimmedAverage { get; init; }
        public double SwapDelta { get; init; }
        public double SwapDeltaPercent { get; init; }
        public bool IsWeakSuggestion { get; init; }
        public required string Note { get; init; }
    }

    /// <summary>Information about running style duplication within a team for summary notes.</summary>
    public sealed class TeamStyleOverlapRecommendation
    {
        public required string Distance { get; init; }
        public TeamStyleOverlap Overlap { get; init; }
        public required string SharedStyle { get; init; }
        public required string Note { get; init; }
    }

    public sealed class UmaAnalysisReport
    {
        public required IReadOnlyList<UmaAnalysisRow> Rows { get; init; }
        public required IReadOnlyList<TeamAceRecommendation> AceRecommendations { get; init; }
        public required IReadOnlyList<TeamStyleOverlapRecommendation> StyleOverlapRecommendations { get; init; }
        public double TeamAverage { get; init; }
        public int TrialCount { get; init; }
    }

    public static class UmaAnalysisEngine
    {
        public const double OutlierTeamFraction = 0.8;
        public const double PerformanceWeight = 0.85;
        public const double ConsistencyWeight = 0.15;
        public const double CriticalThreshold = 8.0;
        public const double NeedsWorkThreshold = 5.0;
        /// <summary>Percent lead required for a strong ace-swap suggestion (scales down with more trials).</summary>
        public const double AceSwapStrongPercentFloor = 1.5;

        private static readonly string[] DistanceNames = ["Sprint", "Mile", "Medium", "Long", "Dirt"];

        public static UmaAnalysisReport Analyze(
            IReadOnlyList<string> rosterNames,
            IReadOnlyList<IReadOnlyList<double>> scoreMatrix,
            TeamTrialResult? rosterLayoutSource = null)
        {
            if (rosterNames.Count == 0 || scoreMatrix.Count != rosterNames.Count)
            {
                return EmptyReport();
            }

            int trialCount = scoreMatrix[0].Count;
            if (trialCount == 0 || scoreMatrix.Any(row => row.Count != trialCount))
            {
                return EmptyReport();
            }

            var rawStats = new List<RawUmaStats>(rosterNames.Count);
            for (int i = 0; i < rosterNames.Count; i++)
            {
                var layout = GetRosterLayout(rosterLayoutSource, i);
                rawStats.Add(new RawUmaStats(
                    rosterNames[i],
                    scoreMatrix[i].ToArray(),
                    layout.Distance,
                    layout.Role,
                    layout.RunningStyleLabel,
                    layout.IsAce,
                    layout.TeamIndex,
                    i));
            }

            double teamBenchmark = rawStats.Select(s => s.Average).DefaultIfEmpty(0).Average();

            foreach (var stat in rawStats)
            {
                stat.ComputeDerived(teamBenchmark);
            }

            double maxTrimmed = rawStats.Max(s => s.TrimmedAverage);
            double maxGap = rawStats.Max(s => maxTrimmed - s.TrimmedAverage);
            double maxAdjustedCv = rawStats.Max(s => s.AdjustedCoefficientOfVariation);

            if (maxGap <= 0)
            {
                maxGap = 1;
            }

            if (maxAdjustedCv <= 0)
            {
                maxAdjustedCv = 1;
            }

            var rows = new List<UmaAnalysisRow>(rawStats.Count);
            foreach (var stat in rawStats)
            {
                double gap = maxTrimmed - stat.TrimmedAverage;
                double retrainScore = ((gap / maxGap * PerformanceWeight)
                    + (stat.AdjustedCoefficientOfVariation / maxAdjustedCv * ConsistencyWeight)) * 10;

                var level = ClassifyPriority(retrainScore);
                rows.Add(new UmaAnalysisRow
                {
                    Name = stat.Name,
                    RunningStyleLabel = stat.RunningStyleLabel,
                    Distance = stat.Distance,
                    Role = stat.Role,
                    TrialCount = trialCount,
                    Average = Round(stat.Average),
                    TrimmedAverage = Round(stat.TrimmedAverage),
                    CoefficientOfVariation = stat.CoefficientOfVariation,
                    AdjustedCoefficientOfVariation = stat.AdjustedCoefficientOfVariation,
                    GapToTop = Round(gap),
                    Ceiling = Round(stat.Ceiling),
                    Floor = Round(stat.Floor),
                    CeilingFloorSpread = Round(stat.Ceiling - stat.Floor),
                    IsCurrentAce = stat.IsAce,
                    RetrainScore = Math.Round(retrainScore, 1),
                    PriorityLevel = level,
                    RetrainPriorityLabel = FormatPriorityLabel(level, retrainScore),
                    RosterIndex = stat.RosterIndex,
                    TeamIndex = stat.TeamIndex,
                    // AceDelta* and AceDeltaLabel are populated by ApplyAceDeltas below
                });
            }

            ApplyAceDeltas(rows);

            var aceRecommendations = BuildAceRecommendations(rows, trialCount);
            ApplyPotentialAceLabels(rows, aceRecommendations);
            ApplyTeamStyleOverlap(rows);

            var styleOverlapRecommendations = BuildStyleOverlapRecommendations(rows);

            rows.Sort((a, b) => a.RosterIndex.CompareTo(b.RosterIndex));

            return new UmaAnalysisReport
            {
                Rows = rows,
                AceRecommendations = aceRecommendations,
                StyleOverlapRecommendations = styleOverlapRecommendations,
                TeamAverage = Round(teamBenchmark),
                TrialCount = trialCount
            };
        }

        private static void ApplyPotentialAceLabels(
            List<UmaAnalysisRow> rows,
            IReadOnlyList<TeamAceRecommendation> recommendations)
        {
            foreach (var row in rows)
            {
                row.PotentialAceLabel = string.Empty;
                row.IsSuggestedAce = false;
            }

            foreach (var rec in recommendations.Where(r => r.SuggestSwap))
            {
                var row = rows.FirstOrDefault(r => r.Distance == rec.Distance && r.Name == rec.BestSupport);
                if (row == null)
                {
                    continue;
                }

                row.IsSuggestedAce = true;
                row.PotentialAceLabel = FormatPotentialAceLabel(rec);
            }
        }

        private static string FormatPotentialAceLabel(TeamAceRecommendation rec)
        {
            string prefix = rec.IsWeakSuggestion ? "Maybe" : "Yes";
            string delta = FormatDelta(rec.SwapDelta);
            string pct = rec.SwapDeltaPercent.ToString("0.#");
            return $"{prefix} {delta} ({pct}%)";
        }

        private static void ApplyAceDeltas(List<UmaAnalysisRow> rows)
        {
            foreach (var team in rows.GroupBy(r => r.TeamIndex))
            {
                var members = team.OrderBy(m => m.RosterIndex).ToList();
                var ace = members.FirstOrDefault(m => m.IsCurrentAce);
                if (ace == null) continue;

                double aceTrimmed = ace.TrimmedAverage;

                foreach (var row in members)
                {
                    if (row.IsCurrentAce)
                    {
                        row.AceDelta = 0;
                        row.AceDeltaPercent = 0;
                        row.AceDeltaLabel = "+0 (+0%)";
                    }
                    else
                    {
                        row.AceDelta = row.TrimmedAverage - aceTrimmed;
                        row.AceDeltaPercent = aceTrimmed > 0
                            ? row.AceDelta / aceTrimmed * 100
                            : 0;

                        string deltaStr = FormatDelta(row.AceDelta);
                        string pct = row.AceDeltaPercent.ToString("0.#");
                        row.AceDeltaLabel = $"{deltaStr} ({pct}%)";
                    }
                }
            }
        }

        private static string FormatDelta(double delta)
        {
            double abs = Math.Abs(delta);
            if (abs >= 1000)
            {
                return $"{(delta >= 0 ? "+" : "-")}{abs / 1000:0.#}k";
            }

            return $"{(delta >= 0 ? "+" : "-")}{abs:0}";
        }

        private static IReadOnlyList<TeamAceRecommendation> BuildAceRecommendations(
            List<UmaAnalysisRow> rows,
            int trialCount)
        {
            var recommendations = new List<TeamAceRecommendation>();
            foreach (var team in rows.GroupBy(r => r.TeamIndex).OrderBy(g => g.Key))
            {
                var members = team.OrderBy(m => m.RosterIndex).ToList();
                var ace = members.First(m => m.IsCurrentAce);
                var supports = members.Where(m => !m.IsCurrentAce).ToList();
                var bestSupport = supports.OrderByDescending(m => m.TrimmedAverage).First();

                bool suggestSwap = bestSupport.TrimmedAverage > ace.TrimmedAverage;
                double swapDelta = bestSupport.TrimmedAverage - ace.TrimmedAverage;
                double swapDeltaPercent = ace.TrimmedAverage > 0
                    ? swapDelta / ace.TrimmedAverage * 100
                    : 0;
                double strongPercentThreshold = AceSwapStrongPercentFloor + (3.0 / Math.Max(1, trialCount));
                bool isWeak = suggestSwap && swapDeltaPercent < strongPercentThreshold;

                string note = suggestSwap
                    ? isWeak
                        ? $"Weak hint: {bestSupport.Name} edges {ace.Name} by {FormatDelta(swapDelta)} ({swapDeltaPercent:0.#}%) — thin margin for {trialCount} trial(s)."
                        : $"Consider {bestSupport.Name} as ace (+{swapDelta:N0} trimmed base, {swapDeltaPercent:0.#}% vs {ace.Name})."
                    : "";

                recommendations.Add(new TeamAceRecommendation
                {
                    Distance = ace.Distance,
                    CurrentAce = ace.Name,
                    BestSupport = bestSupport.Name,
                    SuggestSwap = suggestSwap,
                    AceTrimmedAverage = ace.TrimmedAverage,
                    BestSupportTrimmedAverage = bestSupport.TrimmedAverage,
                    SwapDelta = swapDelta,
                    SwapDeltaPercent = swapDeltaPercent,
                    IsWeakSuggestion = isWeak,
                    Note = note
                });
            }

            return recommendations;
        }

        private static void ApplyTeamStyleOverlap(List<UmaAnalysisRow> rows)
        {
            foreach (var team in rows.GroupBy(r => r.TeamIndex))
            {
                var members = team.ToList();
                if (members.Count != 3)
                {
                    continue;
                }

                var byStyle = members.GroupBy(r => r.RunningStyleLabel)
                    .OrderByDescending(g => g.Count())
                    .First();

                TeamStyleOverlap overlap = byStyle.Count() switch
                {
                    3 => TeamStyleOverlap.FullTeam,
                    2 => TeamStyleOverlap.Pair,
                    _ => TeamStyleOverlap.None
                };

                if (overlap == TeamStyleOverlap.None)
                {
                    continue;
                }

                string sharedStyle = byStyle.Key;
                foreach (var row in members)
                {
                    if (row.RunningStyleLabel == sharedStyle)
                    {
                        row.StyleOverlap = overlap;
                    }
                }
            }
        }

        private static IReadOnlyList<TeamStyleOverlapRecommendation> BuildStyleOverlapRecommendations(List<UmaAnalysisRow> rows)
        {
            var recommendations = new List<TeamStyleOverlapRecommendation>();

            foreach (var team in rows.GroupBy(r => r.TeamIndex).OrderBy(g => g.Key))
            {
                var members = team.ToList();
                if (members.Count != 3)
                {
                    continue;
                }

                var byStyle = members.GroupBy(r => r.RunningStyleLabel)
                    .OrderByDescending(g => g.Count())
                    .First();

                TeamStyleOverlap overlap = byStyle.Count() switch
                {
                    3 => TeamStyleOverlap.FullTeam,
                    2 => TeamStyleOverlap.Pair,
                    _ => TeamStyleOverlap.None
                };

                if (overlap == TeamStyleOverlap.None)
                {
                    continue;
                }

                string sharedStyle = byStyle.Key;

                string note = overlap switch
                {
                    TeamStyleOverlap.FullTeam => $"All three share the same running style ({sharedStyle}). This reduces team effectiveness.",
                    TeamStyleOverlap.Pair => $"Two share the same running style ({sharedStyle}).",
                    _ => ""
                };

                recommendations.Add(new TeamStyleOverlapRecommendation
                {
                    Distance = members.First().Distance,
                    Overlap = overlap,
                    SharedStyle = sharedStyle,
                    Note = note
                });
            }

            return recommendations;
        }

        private static (string Distance, string Role, string RunningStyleLabel, bool IsAce, int TeamIndex) GetRosterLayout(
            TeamTrialResult? source,
            int rosterIndex)
        {
            int teamIndex = rosterIndex / 3;
            string distance = teamIndex < DistanceNames.Length
                ? DistanceNames[teamIndex]
                : $"Team {teamIndex + 1}";

            if (source?.RaceRoster.Count > rosterIndex)
            {
                var uma = source.RaceRoster.ElementAt(rosterIndex).Value;
                bool isAce = uma.TeamMemberId == 1;
                int distanceType = source.FindRaceResultByUma(uma)?.DistanceType ?? (teamIndex + 1);
                distance = FormatDistance(distanceType);
                return (distance, isAce ? "Ace" : "Support", FormatRunningStyle(uma.RunningStyle), isAce, teamIndex);
            }

            bool fallbackAce = rosterIndex % 3 == 0;
            return (distance, fallbackAce ? "Ace" : "Support", "—", fallbackAce, teamIndex);
        }

        private static string FormatRunningStyle(int runningStyle) => runningStyle switch
        {
            1 => "Front",
            2 => "Pace",
            3 => "Late",
            4 => "End",
            _ => $"#{runningStyle}"
        };

        private static UmaAnalysisReport EmptyReport() => new()
        {
            Rows = Array.Empty<UmaAnalysisRow>(),
            AceRecommendations = Array.Empty<TeamAceRecommendation>(),
            StyleOverlapRecommendations = Array.Empty<TeamStyleOverlapRecommendation>(),
            TeamAverage = 0,
            TrialCount = 0
        };

        private static RetrainPriorityLevel ClassifyPriority(double score)
        {
            if (score >= CriticalThreshold)
            {
                return RetrainPriorityLevel.Critical;
            }

            if (score >= NeedsWorkThreshold)
            {
                return RetrainPriorityLevel.NeedsWork;
            }

            return RetrainPriorityLevel.Stable;
        }

        private static string FormatPriorityLabel(RetrainPriorityLevel level, double score)
        {
            string tag = level switch
            {
                RetrainPriorityLevel.Critical => "CRITICAL",
                RetrainPriorityLevel.NeedsWork => "NEEDS WORK",
                _ => "STABLE"
            };
            return $"{tag} ({Math.Round(score, 1)}/10)";
        }

        private static string FormatDistance(int distanceType) => distanceType switch
        {
            1 => "Sprint",
            2 => "Mile",
            3 => "Medium",
            4 => "Long",
            5 => "Dirt",
            _ => $"Race {distanceType}"
        };

        private static double Round(double value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

        private sealed class RawUmaStats
        {
            public RawUmaStats(
                string name,
                double[] scores,
                string distance,
                string role,
                string runningStyleLabel,
                bool isAce,
                int teamIndex,
                int rosterIndex)
            {
                Name = name;
                Scores = scores;
                Distance = distance;
                Role = role;
                RunningStyleLabel = runningStyleLabel;
                IsAce = isAce;
                TeamIndex = teamIndex;
                RosterIndex = rosterIndex;
                Average = scores.Length > 0 ? scores.Average() : 0;
            }

            public string Name { get; }
            public double[] Scores { get; }
            public string Distance { get; }
            public string Role { get; }
            public string RunningStyleLabel { get; }
            public bool IsAce { get; }
            public int TeamIndex { get; }
            public int RosterIndex { get; }
            public double Average { get; }
            public double TrimmedAverage { get; private set; }
            public double CoefficientOfVariation { get; private set; }
            public double AdjustedCoefficientOfVariation { get; private set; }
            public double Ceiling { get; private set; }
            public double Floor { get; private set; }

            public void ComputeDerived(double teamBenchmark)
            {
                TrimmedAverage = ComputeTrimmedAverage(Scores, teamBenchmark);
                CoefficientOfVariation = ComputeCoefficientOfVariation(Scores, Average);
                AdjustedCoefficientOfVariation = ComputeAdjustedCoefficientOfVariation(
                    Scores,
                    teamBenchmark,
                    CoefficientOfVariation);
                Ceiling = Percentile(Scores, 0.9);
                Floor = Percentile(Scores, 0.1);
            }

            private static double ComputeTrimmedAverage(double[] scores, double teamBenchmark)
            {
                if (scores.Length == 0)
                {
                    return 0;
                }

                double min = scores.Min();
                double avg = scores.Average();
                if (min < teamBenchmark * OutlierTeamFraction && scores.Length > 1)
                {
                    return (scores.Sum() - min) / (scores.Length - 1);
                }

                return avg;
            }

            private static double ComputeCoefficientOfVariation(double[] scores, double average)
            {
                if (scores.Length == 0 || average == 0)
                {
                    return 0;
                }

                return PopulationStdev(scores) / average;
            }

            private static double ComputeAdjustedCoefficientOfVariation(
                double[] scores,
                double teamBenchmark,
                double trueCv)
            {
                if (scores.Length == 0 || trueCv == 0)
                {
                    return 0;
                }

                double min = scores.Min();
                if (min < teamBenchmark * OutlierTeamFraction && scores.Length > 1)
                {
                    var filtered = scores.Where(s => s > min).ToArray();
                    double trimmedMean = filtered.Average();
                    return ComputeCoefficientOfVariation(filtered, trimmedMean);
                }

                return trueCv;
            }

            private static double PopulationStdev(double[] values)
            {
                if (values.Length == 0)
                {
                    return 0;
                }

                double avg = values.Average();
                double variance = values.Sum(v => (v - avg) * (v - avg)) / values.Length;
                return Math.Sqrt(variance);
            }

            private static double Percentile(double[] values, double p)
            {
                if (values.Length == 0)
                {
                    return 0;
                }

                var sorted = values.OrderBy(v => v).ToArray();
                double rank = p * (sorted.Length - 1);
                int lo = (int)Math.Floor(rank);
                int hi = (int)Math.Ceiling(rank);
                if (lo == hi)
                {
                    return sorted[lo];
                }

                return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
            }
        }
    }
}

// Top-level types for track performance (to avoid complex nesting issues in the containing static class)
namespace UmaParser.Analysis
{
    /// <summary>
    /// Per-track performance breakdown for a specific uma across the provided trials.
    /// </summary>
    public static class TracksAnalyzer
    {
        public static TracksReport Analyze(
            IEnumerable<TeamTrialResult> trials,
            int trainedCharaId,
            string umaName)
        {
            var catalog = UmaParser.MasterData.GameMasterService.Current.Catalog;
            var trialList = trials.ToList();

            int totalRaces = 0;
            var perTrack = new Dictionary<(string Name, int Distance), TrackStats>();

            foreach (var trial in trialList)
            {
                foreach (var appearance in trial.GetAppearances(trainedCharaId))
                {
                    totalRaces++;

                    int raceInstanceId = 0;

                    // Match the start params using the Round from the result (same logic as GetAppearances)
                    var result = appearance.Result;
                    var startParams = trial.Data?.RaceStartParamsArray?
                        .FirstOrDefault(s => s.Round == result.Round);
                    if (startParams != null)
                    {
                        raceInstanceId = startParams.RaceInstanceId;
                    }

                    catalog.TryGetRaceCourse(raceInstanceId, out var course);

                    var key = course != null
                        ? (course.Name, course.Distance)
                        : ("Unknown", 0);

                    if (!perTrack.TryGetValue(key, out var stats))
                    {
                        stats = new TrackStats
                        {
                            Name = course?.Name ?? "Unknown",
                            Distance = course?.Distance ?? 0,
                            Ground = course?.Ground ?? 0
                        };
                        perTrack[key] = stats;
                    }

                    // Spurt Rate calculation
                    if (course != null && course.Distance > 0)
                    {
                        int horseIndex = appearance.Horse.FrameOrder - 1;
                        if (horseIndex >= 0 && horseIndex < appearance.Simulation.HorseResults.Count)
                        {
                            var horseResult = appearance.Simulation.HorseResults[horseIndex];
                            float lastSpurt = horseResult.LastSpurtStartDistance;
                            if (lastSpurt < 0)
                            {
                                // -1 means the uma never began their spurt (insufficient HP)
                                // → counts as a bad spurt (not good)
                            }
                            else
                            {
                                float idealSpurt = course.Distance * (2f / 3f);
                                float delay = lastSpurt - idealSpurt;
                                if (delay <= 3.0f)
                                {
                                    stats.GoodSpurtCount++;
                                }
                            }
                        }
                    }

                    // Survival calculation: % of races where uma reached goal with >0 HP
                    // (did not hit 0 HP before or in the frame where distance >= goal)
                    if (course != null && course.Distance > 0)
                    {
                        int horseIndex = appearance.Horse.FrameOrder - 1;
                        if (horseIndex >= 0 && appearance.Simulation.Frames.Count > 0 &&
                            horseIndex < appearance.Simulation.Frames[0].HorseFrames.Count)
                        {
                            float raceDist = course.Distance;
                            bool survived = true;
                            foreach (var frame in appearance.Simulation.Frames)
                            {
                                var hf = frame.HorseFrames[horseIndex];
                                if (hf.Hp <= 0 && hf.Distance < raceDist)
                                {
                                    survived = false;
                                    break;
                                }
                                if (hf.Distance >= raceDist)
                                {
                                    if (hf.Hp <= 0)
                                        survived = false;
                                    break;
                                }
                            }
                            if (survived)
                            {
                                stats.SurvivedCount++;
                            }
                        }
                    }

                    // Placement
                    int? finishOrder = null;
                    var raceResult = trial.FindRaceResultByUma(appearance.Horse);
                    if (raceResult != null)
                    {
                        var charaRes = raceResult.CharaResultArray
                            .FirstOrDefault(c => c.TrainedCharaId == trainedCharaId);
                        finishOrder = charaRes?.FinishOrder;
                    }

                    if (finishOrder.HasValue)
                    {
                        stats.Placements.Add(finishOrder.Value);
                    }

                    // Total skill points this race (no bonuses)
                    int raceSkillPoints = 0;
                    foreach (var (_, points) in SkillRaceScoring.ScoreActivations(appearance))
                    {
                        raceSkillPoints += (int)points;
                    }
                    stats.TotalSkillPoints += raceSkillPoints;
                    stats.RaceCount++;
                }
            }

            var rows = perTrack.Values
                .Select(s => s.ToRow())
                .OrderBy(r => r.Track)
                .ThenBy(r => r.Distance)
                .ToList();

            string teamType = DeriveTeamType(trialList, trainedCharaId);

            return new TracksReport
            {
                UmaName = umaName,
                TotalRaces = totalRaces,
                TeamType = teamType,
                Rows = rows
            };
        }

        /// <summary>
        /// Version for non-TT single-race data.
        /// Course/distance info is limited (no raceInstanceId), so tracks are grouped generically.
        /// Spurt/survival still work from simulation. Skill points are TT-only and will be 0.
        /// Placements come from the simulation HorseResults.
        /// </summary>
        public static TracksReport Analyze(
            IEnumerable<RaceAppearance> appearances,
            string umaName)
        {
            var appList = appearances.ToList();
            int totalRaces = 0;
            var perTrack = new Dictionary<(string Name, int Distance), TrackStats>();

            foreach (var appearance in appList)
            {
                totalRaces++;

                // For non-TT we don't have reliable raceInstanceId / master course lookup.
                // Use a generic name + distance 0 (or we could parse per-file metadata in future).
                var key = ("Single Race / Practice", 0);

                if (!perTrack.TryGetValue(key, out var stats))
                {
                    stats = new TrackStats
                    {
                        Name = key.Item1,
                        Distance = key.Item2,
                        Ground = 0
                    };
                    perTrack[key] = stats;
                }

                int horseIndex = appearance.Horse.FrameOrder - 1;
                if (horseIndex < 0 || horseIndex >= appearance.Simulation.HorseResults.Count)
                    continue;

                var horseResult = appearance.Simulation.HorseResults[horseIndex];

                // Placement from simulation (reliable for non-TT)
                int finishOrder = horseResult.FinishOrder + 1; // 0-based -> 1-based
                stats.Placements.Add(finishOrder);

                // Spurt rate (use a reasonable default distance if unknown; 2000m is common for many CM)
                int effectiveDistance = key.Item2 > 0 ? key.Item2 : 2000;
                float lastSpurt = horseResult.LastSpurtStartDistance;
                if (lastSpurt >= 0)
                {
                    float idealSpurt = effectiveDistance * (2f / 3f);
                    float delay = lastSpurt - idealSpurt;
                    if (delay <= 3.0f)
                    {
                        stats.GoodSpurtCount++;
                    }
                }

                // Survival from frames
                if (appearance.Simulation.Frames.Count > 0 &&
                    horseIndex < appearance.Simulation.Frames[0].HorseFrames.Count)
                {
                    float raceDist = effectiveDistance;
                    bool survived = true;
                    foreach (var frame in appearance.Simulation.Frames)
                    {
                        if (horseIndex >= frame.HorseFrames.Count) break;
                        var hf = frame.HorseFrames[horseIndex];
                        if (hf.Hp <= 0 && hf.Distance < raceDist)
                        {
                            survived = false;
                            break;
                        }
                        if (hf.Distance >= raceDist)
                        {
                            if (hf.Hp <= 0) survived = false;
                            break;
                        }
                    }
                    if (survived) stats.SurvivedCount++;
                }

                // Skill points: TT-specific, leave at 0 for non-TT
                stats.RaceCount++;
            }

            var rows = perTrack.Values
                .Select(s => s.ToRow())
                .OrderBy(r => r.Track)
                .ThenBy(r => r.Distance)
                .ToList();

            return new TracksReport
            {
                UmaName = umaName,
                TotalRaces = totalRaces,
                TeamType = "Non-TT (CM/Room/Practice)",
                Rows = rows
            };
        }

        /// <summary>
        /// Non-TT overload that receives per-race course metadata (parsed from the capture files).
        /// </summary>
        public static TracksReport Analyze(
            IEnumerable<(RaceAppearance Appearance, int Distance, string TrackName)> items,
            string umaName)
        {
            var itemList = items.ToList();
            int totalRaces = 0;
            var perTrack = new Dictionary<(string Name, int Distance), TrackStats>();

            foreach (var (appearance, distance, trackName) in itemList)
            {
                totalRaces++;

                int effDist = distance > 0 ? distance : 2000; // fallback for spurt calc
                string name = string.IsNullOrWhiteSpace(trackName) ? "Single Race" : trackName;

                var key = (name, effDist);

                if (!perTrack.TryGetValue(key, out var stats))
                {
                    stats = new TrackStats
                    {
                        Name = name,
                        Distance = effDist,
                        Ground = 0
                    };
                    perTrack[key] = stats;
                }

                int horseIndex = appearance.Horse.FrameOrder - 1;
                if (horseIndex < 0 || horseIndex >= appearance.Simulation.HorseResults.Count)
                    continue;

                var horseResult = appearance.Simulation.HorseResults[horseIndex];

                int finishOrder = horseResult.FinishOrder + 1;
                stats.Placements.Add(finishOrder);

                // Spurt
                float lastSpurt = horseResult.LastSpurtStartDistance;
                if (lastSpurt >= 0)
                {
                    float idealSpurt = effDist * (2f / 3f);
                    float delay = lastSpurt - idealSpurt;
                    if (delay <= 3.0f)
                        stats.GoodSpurtCount++;
                }

                // Survival
                if (appearance.Simulation.Frames.Count > 0 && horseIndex < appearance.Simulation.Frames[0].HorseFrames.Count)
                {
                    bool survived = true;
                    float raceDist = effDist;
                    foreach (var frame in appearance.Simulation.Frames)
                    {
                        if (horseIndex >= frame.HorseFrames.Count) break;
                        var hf = frame.HorseFrames[horseIndex];
                        if (hf.Hp <= 0 && hf.Distance < raceDist)
                        {
                            survived = false;
                            break;
                        }
                        if (hf.Distance >= raceDist)
                        {
                            if (hf.Hp <= 0) survived = false;
                            break;
                        }
                    }
                    if (survived) stats.SurvivedCount++;
                }

                stats.RaceCount++;
            }

            var rows = perTrack.Values
                .Select(s => s.ToRow())
                .OrderBy(r => r.Track)
                .ThenBy(r => r.Distance)
                .ToList();

            return new TracksReport
            {
                UmaName = umaName,
                TotalRaces = totalRaces,
                TeamType = "Non-TT (CM/Room/Practice)",
                Rows = rows
            };
        }

        private static string DeriveTeamType(List<TeamTrialResult> trials, int trainedCharaId)
        {
            var counts = new int[6];
            foreach (var trial in trials)
            {
                foreach (var appearance in trial.GetAppearances(trainedCharaId))
                {
                    var raceResult = trial.FindRaceResultByUma(appearance.Horse);
                    int dt = raceResult?.DistanceType ?? 0;
                    if (dt >= 1 && dt <= 5) counts[dt]++;
                }
            }

            int max = counts.Skip(1).Max();
            if (max == 0) return "—";

            int best = Array.IndexOf(counts, max, 1);
            return best switch
            {
                1 => "Sprint",
                2 => "Mile",
                3 => "Medium",
                4 => "Long",
                5 => "Dirt",
                _ => "Mixed"
            };
        }

        private sealed class TrackStats
        {
            public string Name { get; set; } = string.Empty;
            public int Distance { get; set; }
            public int Ground { get; set; }
            public int RaceCount { get; set; }
            public List<int> Placements { get; } = new();
            public int TotalSkillPoints { get; set; }
            public int GoodSpurtCount { get; set; }
            public int SurvivedCount { get; set; }

            public TracksRow ToRow()
            {
                int races = RaceCount;
                double avgPlace = races > 0 && Placements.Count > 0
                    ? Placements.Average()
                    : 0;

                int firsts = Placements.Count(p => p == 1);
                int top3OrBetter = Placements.Count(p => p <= 3);
                int top5OrBetter = Placements.Count(p => p <= 5);

                double pctFirst = races > 0 ? firsts * 100.0 / races : 0;
                double pctTop3 = races > 0 ? top3OrBetter * 100.0 / races : 0;
                double pctTop5 = races > 0 ? top5OrBetter * 100.0 / races : 0;

                double avgSkillPts = races > 0 ? TotalSkillPoints / (double)races : 0;

                double spurtRate = races > 0 ? (GoodSpurtCount * 100.0 / races) : 0;
                double survival = races > 0 ? (SurvivedCount * 100.0 / races) : 0;

                return new TracksRow
                {
                    Track = Name,
                    Distance = Distance,
                    Races = races,
                    AvgPlace = Math.Round(avgPlace, 2),
                    PctFirst = Math.Round(pctFirst, 1),
                    PctTop3 = Math.Round(pctTop3, 1),
                    PctTop5 = Math.Round(pctTop5, 1),
                    AvgSkillPointsPerRace = Math.Round(avgSkillPts, 0),
                    SpurtRate = Math.Round(spurtRate, 1),
                    Survival = Math.Round(survival, 1)
                };
            }
        }
    }

    public sealed class TracksReport
    {
        public string UmaName { get; init; } = string.Empty;
        public int TotalRaces { get; init; }
        public string TeamType { get; init; } = "—";
        public IReadOnlyList<TracksRow> Rows { get; init; } = Array.Empty<TracksRow>();
    }

    public sealed class TracksRow
    {
        public string Track { get; init; } = string.Empty;
        public int Distance { get; init; }
        public int Races { get; init; }
        public double AvgPlace { get; init; }
        public double PctFirst { get; init; }
        public double PctTop3 { get; init; }
        public double PctTop5 { get; init; }
        public double AvgSkillPointsPerRace { get; init; }
        public double SpurtRate { get; init; }
        public double Survival { get; init; }
    }
}
