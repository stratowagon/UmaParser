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

/// <summary>One trained uma, ready for a stacked bar and the spread drawn under it.</summary>
public sealed class PerformanceUmaRow
{
    public required int TrainedCharaId { get; init; }
    public required int RosterIndex { get; init; }

    /// <summary>Catalog short name, without the rank-score suffix.</summary>
    public required string ShortName { get; init; }

    /// <summary>
    /// Catalog short name. Team Trials captures do not include a portrait rating, so this stays the short name.
    /// A positive <see cref="RankScore"/> is appended as <c>[18,317]</c> when some other capture type supplies one.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Portrait rating when the capture has one. Team Trials rows leave this at 0.</summary>
    public required int RankScore { get; init; }
    public required string Distance { get; init; }
    public required string Style { get; init; }

    /// <summary>Distance, running style, and how many loaded trials this uma ran. Ace and support are not shown.</summary>
    public required string Subtitle { get; init; }

    /// <summary>Loaded trials in which this uma raced. Averages use only those trials.</summary>
    public required int TrialCount { get; init; }

    /// <summary>True when this trained uma is on the roster of the newest loaded file.</summary>
    public required bool InCurrentLineup { get; init; }

    /// <summary>
    /// Set from the Options thresholds. Updated in place when those thresholds change,
    /// so a sort does not have to rebuild the bars.
    /// </summary>
    public PerformanceSampleWarning SampleWarning { get; set; }
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
/// Chart for every trained uma, running style, and distance in the loaded Team Trials files.
/// <see cref="Rows"/> start in
/// current-lineup order (newest file), with umas who are not on that roster after them,
/// until <see cref="Sorted"/> is called. <see cref="TeamAverage"/> is the mean of the
/// current lineup's average base scores. On a consistent roster that lineup is every row,
/// so the line matches the old team average.
/// </summary>
public sealed class PerformanceChart
{
    public bool Available { get; init; }
    public string EmptyMessage { get; init; } = "";

    /// <summary>How many Team Trials files were loaded. A row's own count can be smaller.</summary>
    public int TrialCount { get; init; }

    /// <summary>Mean of the current lineup's per-uma average base scores (the newest file's roster).</summary>
    public double TeamAverage { get; init; }
    public IReadOnlyList<PerformanceUmaRow> Rows { get; init; } = Array.Empty<PerformanceUmaRow>();

    /// <summary>Restamps <see cref="PerformanceUmaRow.SampleWarning"/> from <paramref name="thresholds"/>.</summary>
    public void ApplySampleThresholds(PerformanceSampleThresholds thresholds)
    {
        foreach (PerformanceUmaRow row in Rows)
        {
            row.SampleWarning = thresholds.Classify(row.TrialCount);
        }
    }

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
/// Each loaded file contributes one race (one base total) for each uma who was on that file's roster.
/// Identity is trained character id, running style, and distance type. The same uma in two
/// styles or two distances is two rows. Team slot is not part of the key, so ace and support
/// stay one row when id, style, and distance match. The last trial is the current lineup,
/// matching <see cref="TeamTrialBatch.MostRecentTrial"/> (files are oldest first). A current
/// row is the style and distance that uma uses on that newest roster.
/// </summary>
public static class PerformanceChartEngine
{
    public const string DropMessage = "Drop one or more .json capture files onto this window.";

    public const string NonTeamTrialsMessage =
        "Performance reads Team Trials captures. Drop Team Trials files on their own, without Champions Meet, Room Match, or Practice.";

    public static PerformanceChart Build(TeamTrialBatch batch) =>
        Build(batch, PerformanceSampleThresholds.Default);

    public static PerformanceChart Build(TeamTrialBatch batch, PerformanceSampleThresholds thresholds)
    {
        if (batch.Kind is not (TeamTrialBatchKind.RosterStats or TeamTrialBatchKind.RosterMismatch))
        {
            string message = batch.Kind == TeamTrialBatchKind.Empty ? DropMessage : NonTeamTrialsMessage;
            return Unavailable(message);
        }

        var trials = new List<TeamTrialResult>(batch.Trials.Count);
        foreach (TeamTrialResult trial in batch.Trials.Values)
        {
            trials.Add(trial);
        }

        return Build(trials, thresholds);
    }

    public static PerformanceChart Build(IReadOnlyList<TeamTrialResult> trials) =>
        Build(trials, PerformanceSampleThresholds.Default);

    public static PerformanceChart Build(IReadOnlyList<TeamTrialResult> trials, PerformanceSampleThresholds thresholds)
    {
        if (trials.Count == 0)
        {
            return Unavailable(DropMessage);
        }

        GameMasterCatalog catalog = GameMasterService.Current.Catalog;
        var byKey = new Dictionary<UmaRowKey, UmaAccumulator>();
        var seen = new List<UmaRowKey>();
        foreach (TeamTrialResult trial in trials)
        {
            int rosterIndex = 0;
            foreach (KeyValuePair<int, RaceHorseData> entry in trial.RaceRoster)
            {
                int trainedCharaId = entry.Key;
                RaceHorseData horse = entry.Value;
                var key = new UmaRowKey(trainedCharaId, horse.RunningStyle, DistanceTypeOf(trial, horse, rosterIndex));
                if (!byKey.TryGetValue(key, out UmaAccumulator? acc))
                {
                    acc = new UmaAccumulator(trainedCharaId);
                    byKey[key] = acc;
                    seen.Add(key);
                }

                acc.CharaId = horse.CharaId;
                (acc.Distance, acc.Style) = LayoutOf(trial, horse, rosterIndex);
                acc.Add(trial, catalog);
                rosterIndex++;
            }
        }

        if (seen.Count == 0)
        {
            return Unavailable(DropMessage);
        }

        TeamTrialResult newest = trials[^1];
        var current = new List<UmaRowKey>();
        int currentIndex = 0;
        foreach (KeyValuePair<int, RaceHorseData> entry in newest.RaceRoster)
        {
            current.Add(new UmaRowKey(
                entry.Key,
                entry.Value.RunningStyle,
                DistanceTypeOf(newest, entry.Value, currentIndex)));
            currentIndex++;
        }

        var currentSet = new HashSet<UmaRowKey>(current);
        var order = new List<UmaRowKey>(current);
        foreach (UmaRowKey key in seen)
        {
            if (!currentSet.Contains(key))
            {
                order.Add(key);
            }
        }

        var rows = new List<PerformanceUmaRow>(order.Count);
        for (int index = 0; index < order.Count; index++)
        {
            UmaAccumulator acc = byKey[order[index]];
            rows.Add(acc.ToRow(index, currentSet.Contains(order[index]), thresholds, catalog));
        }

        IEnumerable<PerformanceUmaRow> lineup = rows.Where(row => row.InCurrentLineup);
        if (!lineup.Any())
        {
            lineup = rows;
        }

        return new PerformanceChart
        {
            Available = true,
            EmptyMessage = "",
            TrialCount = trials.Count,
            TeamAverage = lineup.Average(row => row.Average),
            Rows = rows,
        };
    }

    public static string FormatRaceCount(int raceCount) =>
        raceCount == 1 ? "1 race" : $"{raceCount.ToString(CultureInfo.CurrentCulture)} races";

    public static string FormatName(string shortName, int rankScore)
    {
        if (rankScore <= 0)
        {
            return shortName;
        }

        return $"{shortName} [{rankScore.ToString("N0", CultureInfo.CurrentCulture)}]";
    }

    public static string FormatSubtitle(string distance, string style, int raceCount) =>
        $"{distance} · {style} · {FormatRaceCount(raceCount)}";

    public static string Describe(PerformanceChart chart)
    {
        string average = chart.TeamAverage.ToString("N0", CultureInfo.CurrentCulture);
        return
            $"Base scores without bonuses across {chart.TrialCount} total trials. " +
            $"Current lineup average: {average}";
    }

    /// <summary>
    /// One chart row. Team member slot is intentionally absent: ace and support share a row
    /// when the trained uma, running style, and distance match. Ace bonus is already removed
    /// from the base score.
    /// </summary>
    private readonly record struct UmaRowKey(int TrainedCharaId, int RunningStyle, int DistanceType);

    private sealed class UmaAccumulator
    {
        private readonly double[] _sourceSums = new double[PerformanceScoreSources.StackOrder.Count];
        private readonly List<double> _scores = new();

        public UmaAccumulator(int trainedCharaId)
        {
            TrainedCharaId = trainedCharaId;
        }

        public int TrainedCharaId { get; }
        public int CharaId { get; set; }
        public int RankScore { get; set; }
        public string Distance { get; set; } = "";
        public string Style { get; set; } = "";

        public void Add(TeamTrialResult trial, GameMasterCatalog catalog)
        {
            double total = 0;
            if (trial.Results.TryGetValue(TrainedCharaId, out CharaResult? chara))
            {
                foreach (TeamTrialsScore bucket in chara.ScoreArray)
                {
                    double points = bucket.BaseScore;
                    total += points;
                    PerformanceScoreSource source = PerformanceScoreSources.Classify(bucket.RawScoreId, catalog);
                    _sourceSums[SourceIndex(source)] += points;
                }
            }

            _scores.Add(total);
        }

        public PerformanceUmaRow ToRow(
            int rosterIndex,
            bool inCurrentLineup,
            PerformanceSampleThresholds thresholds,
            GameMasterCatalog catalog)
        {
            int raceCount = _scores.Count;
            var segments = new List<PerformanceSegment>(PerformanceScoreSources.StackOrder.Count);
            foreach (PerformanceScoreSource source in PerformanceScoreSources.StackOrder)
            {
                segments.Add(new PerformanceSegment
                {
                    Source = source,
                    Label = PerformanceScoreSources.Label(source),
                    Average = _sourceSums[SourceIndex(source)] / raceCount,
                });
            }

            string shortName = catalog.FormatCharaShortName(CharaId);
            string name = FormatName(shortName, RankScore);
            string subtitle = FormatSubtitle(Distance, Style, raceCount);
            double average = _scores.Average();
            double median = ScorePercentile.Linear(_scores, 0.5);
            double p90 = ScorePercentile.Linear(_scores, 0.9);
            double skillAverage = segments.Where(segment => PerformanceScoreSources.IsSkill(segment.Source)).Sum(segment => segment.Average);
            var row = new PerformanceUmaRow
            {
                TrainedCharaId = TrainedCharaId,
                RosterIndex = rosterIndex,
                ShortName = shortName,
                Name = name,
                RankScore = RankScore,
                Distance = Distance,
                Style = Style,
                Subtitle = subtitle,
                TrialCount = raceCount,
                InCurrentLineup = inCurrentLineup,
                SampleWarning = thresholds.Classify(raceCount),
                Segments = segments,
                Average = average,
                Median = median,
                Min = _scores.Min(),
                Max = _scores.Max(),
                P25 = ScorePercentile.Linear(_scores, 0.25),
                P75 = ScorePercentile.Linear(_scores, 0.75),
                P90 = p90,
                SkillAverage = skillAverage,
                Tooltip = "",
            };
            return new PerformanceUmaRow
            {
                TrainedCharaId = row.TrainedCharaId,
                RosterIndex = row.RosterIndex,
                ShortName = row.ShortName,
                Name = row.Name,
                RankScore = row.RankScore,
                Distance = row.Distance,
                Style = row.Style,
                Subtitle = row.Subtitle,
                TrialCount = row.TrialCount,
                InCurrentLineup = row.InCurrentLineup,
                SampleWarning = row.SampleWarning,
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

    private static int DistanceTypeOf(TeamTrialResult trial, RaceHorseData uma, int rosterIndex)
    {
        int teamIndex = rosterIndex / 3;
        return trial.FindRaceResultByUma(uma)?.DistanceType ?? (teamIndex + 1);
    }

    private static (string Distance, string Style) LayoutOf(TeamTrialResult trial, RaceHorseData uma, int rosterIndex) =>
        (
            DistanceTypeNames.FormatAnalysis(DistanceTypeOf(trial, uma, rosterIndex)),
            RunningStyleNames.FormatAnalysis(uma.RunningStyle));

    private static PerformanceChart Unavailable(string message) => new()
    {
        Available = false,
        EmptyMessage = message,
        Rows = Array.Empty<PerformanceUmaRow>(),
    };
}
