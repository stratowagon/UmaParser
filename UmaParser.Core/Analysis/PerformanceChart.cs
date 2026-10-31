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

/// <summary>
/// The <c>PerformanceSort</c> and <c>PerformanceSortAscending</c> values in settings.json.
/// A missing or unknown sort is <see cref="PerformanceSortKey.Average"/>. A missing direction is ascending.
/// </summary>
public static class PerformanceSortSetting
{
    public static PerformanceSortKey Parse(string? saved)
    {
        if (string.IsNullOrWhiteSpace(saved))
        {
            return PerformanceSortKey.Average;
        }

        string text = saved.Trim();
        foreach (PerformanceSortKey key in Enum.GetValues<PerformanceSortKey>())
        {
            if (string.Equals(Format(key), text, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return PerformanceSortKey.Average;
    }

    public static string Format(PerformanceSortKey key) => key switch
    {
        PerformanceSortKey.Median => "Median",
        PerformanceSortKey.Max => "Max",
        PerformanceSortKey.P75 => "P75",
        PerformanceSortKey.P90 => "P90",
        _ => "Average",
    };

    /// <summary>Null direction is ascending, the tab's default.</summary>
    public static bool AscendingOrDefault(bool? saved) => saved ?? true;
}

/// <summary>
/// Reads and writes the Performance sort in the shared settings file.
/// Other keys in that file are left as they were.
/// </summary>
internal static class PerformanceSortPreferences
{
    public static (PerformanceSortKey Key, bool Ascending) Load(string? path)
    {
        GameMasterSettings settings = GameMasterSettings.Load(path);
        return (PerformanceSortSetting.Parse(settings.PerformanceSort), PerformanceSortSetting.AscendingOrDefault(settings.PerformanceSortAscending));
    }

    public static void Save(string? path, PerformanceSortKey key, bool ascending)
    {
        GameMasterSettings settings = GameMasterSettings.Load(path);
        settings.PerformanceSort = PerformanceSortSetting.Format(key);
        settings.PerformanceSortAscending = ascending;
        settings.Save(path);
    }
}

/// <summary>Mean base points for one source, already averaged across the loaded trials.</summary>
public sealed class PerformanceSegment
{
    public required PerformanceScoreSource Source { get; init; }
    public required string Label { get; init; }
    public required double Average { get; init; }
}

/// <summary>One roster uma, ready for a stacked bar and the spread drawn under it.</summary>
public sealed class PerformanceUmaRow
{
    public required int TrainedCharaId { get; init; }
    public required int RosterIndex { get; init; }
    public required string Name { get; init; }
    public required string Distance { get; init; }
    public required string Style { get; init; }

    /// <summary>Distance and running style. Ace and support are not shown; those bonuses are already stripped.</summary>
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

    /// <summary>Sum of the white, gold, and unique segment averages. Not drawn on the tab.</summary>
    public required double SkillAverage { get; init; }
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
        $"Team average {chart.TeamAverage.ToString("N0", CultureInfo.CurrentCulture)}. ";

    private static PerformanceUmaRow BuildRow(
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
        (string distance, string style) = LayoutOf(layout, rosterIndex);
        string subtitle = $"{distance} · {style}";
        double median = ScorePercentile.Linear(scores, 0.5);
        double p90 = ScorePercentile.Linear(scores, 0.9);

        var row = new PerformanceUmaRow
        {
            TrainedCharaId = trainedCharaId,
            RosterIndex = rosterIndex,
            Name = name,
            Distance = distance,
            Style = style,
            Subtitle = subtitle,
            TrialCount = trialCount,
            Segments = segments,
            Average = average,
            Median = median,
            Min = scores.Min(),
            Max = scores.Max(),
            P25 = ScorePercentile.Linear(scores, 0.25),
            P75 = ScorePercentile.Linear(scores, 0.75),
            P90 = p90,
            SkillAverage = skillAverage,
            Tooltip = "",
        };

        return new PerformanceUmaRow
        {
            TrainedCharaId = row.TrainedCharaId,
            RosterIndex = row.RosterIndex,
            Name = row.Name,
            Distance = row.Distance,
            Style = row.Style,
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
            Tooltip = FormatTooltip(row),
        };
    }

    internal static string FormatTooltip(PerformanceUmaRow row)
    {
        var text = new StringBuilder();
        text.Append(row.Name);
        text.Append(" · ");
        text.Append(row.Subtitle);
        text.AppendLine();
        text.Append("Average ").Append(Points(row.Average));
        text.Append("    Median ").Append(Points(row.Median));
        text.Append("    P90 ").Append(Points(row.P90));
        foreach (PerformanceSegment segment in row.Segments)
        {
            text.AppendLine();
            text.Append(segment.Label).Append("    ").Append(Points(segment.Average));
        }

        return text.ToString();
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

    private static (string Distance, string Style) LayoutOf(TeamTrialResult layout, int rosterIndex)
    {
        int teamIndex = rosterIndex / 3;
        if (layout.RaceRoster.Count > rosterIndex)
        {
            var uma = layout.RaceRoster.ElementAt(rosterIndex).Value;
            int distanceType = layout.FindRaceResultByUma(uma)?.DistanceType ?? (teamIndex + 1);
            return (
                DistanceTypeNames.FormatAnalysis(distanceType),
                RunningStyleNames.FormatAnalysis(uma.RunningStyle));
        }

        return (DistanceTypeNames.FormatTeamSlot(teamIndex), "—");
    }

    private static PerformanceChart Unavailable(string message) => new()
    {
        Available = false,
        EmptyMessage = message,
        Rows = Array.Empty<PerformanceUmaRow>(),
    };
}
