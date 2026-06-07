using UmaBlobber.ObjectModel;

namespace UmaBlobber.Analysis
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

    public sealed class UmaAnalysisReport
    {
        public required IReadOnlyList<UmaAnalysisRow> Rows { get; init; }
        public required IReadOnlyList<TeamAceRecommendation> AceRecommendations { get; init; }
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
                    TeamIndex = stat.TeamIndex
                });
            }

            var aceRecommendations = BuildAceRecommendations(rows, trialCount);
            ApplyPotentialAceLabels(rows, aceRecommendations);
            ApplyTeamStyleOverlap(rows);

            rows.Sort((a, b) => a.RosterIndex.CompareTo(b.RosterIndex));

            return new UmaAnalysisReport
            {
                Rows = rows,
                AceRecommendations = aceRecommendations,
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

        private static string FormatDelta(double delta)
        {
            double abs = Math.Abs(delta);
            if (abs >= 1000)
            {
                return $"{(delta >= 0 ? "+" : "-")}{abs / 1000:0.#}k";
            }

            return $"{(delta >= 0 ? "+" : "")}{abs:0}";
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