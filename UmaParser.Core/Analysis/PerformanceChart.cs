using System.Globalization;
using System.Text;
using UmaParser.DataModel.ResponseData;
using UmaParser.Import;
using UmaParser.MasterData;
using UmaParser.ObjectModel;

namespace UmaParser.Analysis;

/// <summary>Which per-race base-score statistic orders the Performance rows.</summary>
public enum PerformanceSortKey
{
    Average,
    Median,
    Max,
    P75,
    P90,
}

/// <summary>Mean base points for one source, already averaged across the loaded trials.</summary>
public sealed class PerformanceSegment
{
    public required PerformanceScoreSource Source { get; init; }
    public required string Label { get; init; }
    public required double Average { get; init; }
}

/// <summary>
/// One skill on the hover card. Rates come from <see cref="SkillActivationAnalyzer"/>
/// (per-race activation, 0–100). Points per race are that report's attributed skill points.
/// </summary>
public sealed class PerformanceSkillLine
{
    public required int SkillId { get; init; }
    public required string Name { get; init; }
    public required double PerRaceRatePercent { get; init; }
    public required double PointsPerRace { get; init; }
}

/// <summary>One roster uma, ready for a stacked bar and the spread drawn under it.</summary>
public sealed class PerformanceUmaRow
{
    public required int TrainedCharaId { get; init; }
    public required int RosterIndex { get; init; }
    public required string Name { get; init; }
    public required string Distance { get; init; }
    public required string Style { get; init; }
    public required string Role { get; init; }
    public required bool IsAce { get; init; }
    public required string Subtitle { get; init; }
    public required int TrialCount { get; init; }
    public required IReadOnlyList<PerformanceSegment> Segments { get; init; }
    public required double Average { get; init; }
    public required double Median { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public required double P25 { get; init; }
    public required double P75 { get; init; }
    public required double P90 { get; init; }
    public required double SkillAverage { get; init; }
    public required IReadOnlyList<PerformanceSkillLine> Skills { get; init; }
    public required string Tooltip { get; init; }

    public double Value(PerformanceSortKey key) => key switch
    {
        PerformanceSortKey.Average => Average,
        PerformanceSortKey.Median => Median,
        PerformanceSortKey.Max => Max,
        PerformanceSortKey.P75 => P75,
        PerformanceSortKey.P90 => P90,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown performance sort key."),
    };
}

/// <summary>
/// Chart for one consistent Team Trials roster. <see cref="Rows"/> stay in roster order
/// until <see cref="Sorted"/> is called. <see cref="TeamAverage"/> is the mean of the
/// per-uma average base scores.
/// </summary>
public sealed class PerformanceChart
{
    public bool Available { get; init; }
    public string EmptyMessage { get; init; } = "";
    public int TrialCount { get; init; }
    public double TeamAverage { get; init; }
    public IReadOnlyList<PerformanceUmaRow> Rows { get; init; } = Array.Empty<PerformanceUmaRow>();

    public PerformanceChart Sorted(PerformanceSortKey key, bool ascending)
    {
        if (!Available || Rows.Count <= 1)
        {
            return this;
        }

        List<PerformanceUmaRow> rosterOrder = Rows.OrderBy(row => row.RosterIndex).ToList();
        IEnumerable<PerformanceUmaRow> ordered = ascending
            ? rosterOrder.OrderBy(row => row.Value(key))
            : rosterOrder.OrderByDescending(row => row.Value(key));
        return Copy(ordered.ToList());
    }

    private PerformanceChart Copy(IReadOnlyList<PerformanceUmaRow> rows) => new()
    {
        Available = Available,
        EmptyMessage = EmptyMessage,
        TrialCount = TrialCount,
        TeamAverage = TeamAverage,
        Rows = rows,
    };
}

/// <summary>
/// Builds the Performance chart from the same normalized base scores Team Analysis uses.
/// Each loaded file contributes one race (one base total) per uma.
/// </summary>
public static class PerformanceChartEngine
{
    public const string DropMessage = "Drop one or more .json capture files onto this window.";

    public const string UnavailableMessage =
        "Performance is only active for Team Trials files that all have the exact same roster of 15 umas.";

    public static PerformanceChart Build(TeamTrialBatch batch)
    {
        if (batch.Kind != TeamTrialBatchKind.RosterStats)
        {
            return Unavailable(UnavailableMessage);
        }

        var trials = new List<TeamTrialResult>(batch.Trials.Count);
        foreach (TeamTrialResult trial in batch.Trials.Values)
        {
            trials.Add(trial);
        }

        return Build(trials);
    }

    public static PerformanceChart Build(IReadOnlyList<TeamTrialResult> trials)
    {
        if (trials.Count == 0)
        {
            return Unavailable(DropMessage);
        }

        TeamTrialResult layout = trials[^1];
        IReadOnlyList<int> ids = layout.RosterTrainedCharaIds;
        IReadOnlyList<string> names = layout.RosterNames;
        if (ids.Count == 0 || names.Count != ids.Count)
        {
            return Unavailable(DropMessage);
        }

        int trialCount = trials.Count;
        int sourceCount = PerformanceScoreSources.StackOrder.Count;
        var sourceSums = new double[ids.Count, sourceCount];
        var totals = new double[ids.Count][];
        for (int i = 0; i < ids.Count; i++)
        {
            totals[i] = new double[trialCount];
        }

        GameMasterCatalog catalog = GameMasterService.Current.Catalog;
        for (int trialIndex = 0; trialIndex < trialCount; trialIndex++)
        {
            TeamTrialResult trial = trials[trialIndex];
            for (int umaIndex = 0; umaIndex < ids.Count; umaIndex++)
            {
                if (!trial.Results.TryGetValue(ids[umaIndex], out CharaResult? chara))
                {
                    continue;
                }

                double total = 0;
                foreach (TeamTrialsScore bucket in chara.ScoreArray)
                {
                    double points = bucket.BaseScore;
                    total += points;
                    PerformanceScoreSource source = PerformanceScoreSources.Classify(bucket.RawScoreId, catalog);
                    sourceSums[umaIndex, SourceIndex(source)] += points;
                }

                totals[umaIndex][trialIndex] = total;
            }
        }

        var rows = new List<PerformanceUmaRow>(ids.Count);
        for (int umaIndex = 0; umaIndex < ids.Count; umaIndex++)
        {
            rows.Add(BuildRow(
                trials,
                layout,
                ids[umaIndex],
                names[umaIndex],
                umaIndex,
                trialCount,
                sourceSums,
                totals[umaIndex]));
        }

        double teamAverage = rows.Count > 0 ? rows.Average(row => row.Average) : 0;
        return new PerformanceChart
        {
            Available = true,
            EmptyMessage = "",
            TrialCount = trialCount,
            TeamAverage = teamAverage,
            Rows = rows,
        };
    }

    public static string Describe(PerformanceChart chart) =>
        $"Base scores with support, opponent, and ace bonuses removed across {chart.TrialCount} trial(s). " +
        $"Team average {chart.TeamAverage.ToString("N0", CultureInfo.CurrentCulture)}. " +
        "Strong start is a gate within 0.02s. The dashed line is the team average; P90 marks the peak.";

    private static PerformanceUmaRow BuildRow(
        IReadOnlyList<TeamTrialResult> trials,
        TeamTrialResult layout,
        int trainedCharaId,
        string name,
        int rosterIndex,
        int trialCount,
        double[,] sourceSums,
        double[] scores)
    {
        var segments = new List<PerformanceSegment>(PerformanceScoreSources.StackOrder.Count);
        foreach (PerformanceScoreSource source in PerformanceScoreSources.StackOrder)
        {
            segments.Add(new PerformanceSegment
            {
                Source = source,
                Label = PerformanceScoreSources.Label(source),
                Average = sourceSums[rosterIndex, SourceIndex(source)] / trialCount,
            });
        }

        double average = scores.Average();
        double skillAverage = segments.Where(segment => PerformanceScoreSources.IsSkill(segment.Source)).Sum(segment => segment.Average);
        (string distance, string role, string style, bool isAce) = LayoutOf(layout, rosterIndex);
        string subtitle = $"{distance} · {role} · {style}";
        IReadOnlyList<PerformanceSkillLine> skills = SkillLines(trials, trainedCharaId, name);

        var row = new PerformanceUmaRow
        {
            TrainedCharaId = trainedCharaId,
            RosterIndex = rosterIndex,
            Name = name,
            Distance = distance,
            Style = style,
            Role = role,
            IsAce = isAce,
            Subtitle = subtitle,
            TrialCount = trialCount,
            Segments = segments,
            Average = average,
            Median = ScorePercentile.Linear(scores, 0.5),
            Min = scores.Min(),
            Max = scores.Max(),
            P25 = ScorePercentile.Linear(scores, 0.25),
            P75 = ScorePercentile.Linear(scores, 0.75),
            P90 = ScorePercentile.Linear(scores, 0.9),
            SkillAverage = skillAverage,
            Skills = skills,
            Tooltip = "",
        };

        return new PerformanceUmaRow
        {
            TrainedCharaId = row.TrainedCharaId,
            RosterIndex = row.RosterIndex,
            Name = row.Name,
            Distance = row.Distance,
            Style = row.Style,
            Role = row.Role,
            IsAce = row.IsAce,
            Subtitle = row.Subtitle,
            TrialCount = row.TrialCount,
            Segments = row.Segments,
            Average = row.Average,
            Median = row.Median,
            Min = row.Min,
            Max = row.Max,
            P25 = row.P25,
            P75 = row.P75,
            P90 = row.P90,
            SkillAverage = row.SkillAverage,
            Skills = row.Skills,
            Tooltip = FormatTooltip(row),
        };
    }

    private static IReadOnlyList<PerformanceSkillLine> SkillLines(
        IReadOnlyList<TeamTrialResult> trials,
        int trainedCharaId,
        string name)
    {
        SkillActivationReport report = SkillActivationAnalyzer.Analyze(trials, trainedCharaId, name);
        var lines = new List<PerformanceSkillLine>(report.Rows.Count);
        foreach (SkillActivationRow skill in report.Rows)
        {
            lines.Add(new PerformanceSkillLine
            {
                SkillId = skill.SkillId,
                Name = skill.SkillName,
                PerRaceRatePercent = skill.PerRaceActivationRatePercent,
                PointsPerRace = skill.PointsPerRace,
            });
        }

        return lines;
    }

    internal static string FormatTooltip(PerformanceUmaRow row)
    {
        var text = new StringBuilder();
        text.Append(row.Name);
        text.Append(" · ");
        text.Append(row.Subtitle);
        text.Append(" · ");
        text.Append(row.TrialCount);
        text.Append(row.TrialCount == 1 ? " trial" : " trials");
        text.AppendLine();
        text.Append("Average ").Append(Points(row.Average));
        text.Append("    Median ").Append(Points(row.Median));
        text.Append("    P25 ").Append(Points(row.P25));
        text.Append("    P75 ").Append(Points(row.P75));
        text.Append("    P90 ").Append(Points(row.P90));
        text.AppendLine();
        text.Append("Min ").Append(Points(row.Min));
        text.Append("    Max ").Append(Points(row.Max));
        text.AppendLine();
        foreach (PerformanceSegment segment in row.Segments)
        {
            text.Append(segment.Label).Append("    ").Append(Points(segment.Average)).AppendLine();
        }

        text.Append("Skill total    ").Append(Points(row.SkillAverage));
        if (row.Skills.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Skills (proc rate, pts/race)");
            foreach (PerformanceSkillLine skill in row.Skills)
            {
                text.Append(skill.Name);
                text.Append("    ");
                text.Append(skill.PerRaceRatePercent.ToString("0.#", CultureInfo.CurrentCulture));
                text.Append("%    ");
                text.Append(skill.PointsPerRace.ToString("0", CultureInfo.CurrentCulture));
                text.AppendLine();
            }
        }

        return text.ToString().TrimEnd();
    }

    private static string Points(double value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static int SourceIndex(PerformanceScoreSource source)
    {
        IReadOnlyList<PerformanceScoreSource> order = PerformanceScoreSources.StackOrder;
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] == source)
            {
                return i;
            }
        }

        return order.Count - 1;
    }

    private static (string Distance, string Role, string Style, bool IsAce) LayoutOf(TeamTrialResult layout, int rosterIndex)
    {
        int teamIndex = rosterIndex / 3;
        if (layout.RaceRoster.Count > rosterIndex)
        {
            var uma = layout.RaceRoster.ElementAt(rosterIndex).Value;
            bool isAce = uma.TeamMemberId == 1;
            int distanceType = layout.FindRaceResultByUma(uma)?.DistanceType ?? (teamIndex + 1);
            return (
                DistanceTypeNames.FormatAnalysis(distanceType),
                isAce ? "Ace" : "Support",
                RunningStyleNames.FormatAnalysis(uma.RunningStyle),
                isAce);
        }

        bool fallbackAce = rosterIndex % 3 == 0;
        return (DistanceTypeNames.FormatTeamSlot(teamIndex), fallbackAce ? "Ace" : "Support", "—", fallbackAce);
    }

    private static PerformanceChart Unavailable(string message) => new()
    {
        Available = false,
        EmptyMessage = message,
        Rows = Array.Empty<PerformanceUmaRow>(),
    };
}
