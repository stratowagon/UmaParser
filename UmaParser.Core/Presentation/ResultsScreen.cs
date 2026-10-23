using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.ObjectModel;

namespace UmaParser.Presentation;

/// <summary>Which Results grid a drop produced. Mixed and non-Team-Trials drops leave the welcome text up.</summary>
public enum ResultsGridKind
{
    /// <summary>No roster grid. The welcome paragraph stays visible.</summary>
    None,
    RosterStats,
    RosterMismatch,
}

/// <summary>
/// The Results tab after a drop: file columns, roster rows, the total row, outlier marks,
/// and the score colors for one <see cref="ColorTheme"/>. The WinForms form fills its grid from this
/// and then applies its own theme-aware heat map. The WPF view paints <see cref="ResultsCell.Heat"/>.
/// Columns are one per file, oldest first. They are not user-reorderable and not sortable.
/// </summary>
public sealed class ResultsScreen
{
    private ResultsScreen(
        ResultsGridKind kind,
        IReadOnlyList<ResultsColumn> columns,
        IReadOnlyList<ResultsGridRow> rows,
        bool[,]? outliers,
        IReadOnlyList<StableRosterUma> stableCore)
    {
        Kind = kind;
        Columns = columns;
        Rows = rows;
        Outliers = outliers;
        StableCore = stableCore;
    }

    public ResultsGridKind Kind { get; }
    public bool ShowGrid => Kind != ResultsGridKind.None;
    public IReadOnlyList<ResultsColumn> Columns { get; }
    public IReadOnlyList<ResultsGridRow> Rows { get; }

    /// <summary>
    /// <c>[fileIndex, rosterSlot]</c> for a roster mismatch. Null for the other kinds.
    /// The same matrix WinForms passes to the outlier styler.
    /// </summary>
    public bool[,]? Outliers { get; }

    /// <summary>
    /// Umas that kept the same team and running style in every mismatched file.
    /// Empty unless <see cref="Kind"/> is <see cref="ResultsGridKind.RosterMismatch"/>.
    /// </summary>
    public IReadOnlyList<StableRosterUma> StableCore { get; }

    /// <summary>The paragraph on the Results tab when no roster grid is showing.</summary>
    public static string Welcome { get; } =
        "Drop one or more race capture JSON files (Team Trials, Champions Meeting, Room Match, or Practice Room) anywhere on this window to load results." +
        Environment.NewLine + Environment.NewLine +
        "The Team Analysis tab is only available if all files are Team Trials with the exact same team." + Environment.NewLine +
        "Mixed Team Trials teams can still show Skills and Track points analysis for any umas that are present in all files.";

    public static ResultsScreen Empty { get; } = new(
        ResultsGridKind.None,
        Array.Empty<ResultsColumn>(),
        Array.Empty<ResultsGridRow>(),
        outliers: null,
        stableCore: Array.Empty<StableRosterUma>());

    public static ResultsScreen Load(TeamTrialBatch batch, ColorTheme theme = ColorTheme.Light)
    {
        switch (batch.Kind)
        {
            case TeamTrialBatchKind.RosterStats:
                return LoadRosterStats(batch.Trials.ToList(), theme);
            case TeamTrialBatchKind.RosterMismatch:
                return LoadRosterMismatch(batch.Trials.ToList());
            default:
                return Empty;
        }
    }

    /// <summary>
    /// Rewrites score colors on an existing roster-stats grid. Cells without a heat color are left alone,
    /// so a theme change does not rebuild columns or rows.
    /// </summary>
    public static void ApplyHeatTheme(IReadOnlyList<ResultsGridRow> rows, ColorTheme theme)
    {
        var scores = new List<int>();
        foreach (ResultsGridRow row in rows)
        {
            if (row.IsFooter)
            {
                continue;
            }

            foreach (ResultsCell cell in row.Cells)
            {
                if (cell.Heat != null && cell.Value is int score)
                {
                    scores.Add(score);
                }
            }
        }

        if (!ScoreHeat.TryGetRange(scores, out double min, out double max))
        {
            return;
        }

        foreach (ResultsGridRow row in rows)
        {
            if (row.IsFooter)
            {
                continue;
            }

            foreach (ResultsCell cell in row.Cells)
            {
                if (cell.Heat != null && cell.Value is int score)
                {
                    cell.Heat = ScoreHeat.ColorFor(score, min, max, theme);
                }
            }
        }
    }

    private static ResultsScreen LoadRosterStats(
        IReadOnlyList<KeyValuePair<string, TeamTrialResult>> files,
        ColorTheme theme)
    {
        if (files.Count == 0)
        {
            return Empty;
        }

        RosterStatsTable table = RosterStatsTableBuilder.Build(files);
        var columns = new List<ResultsColumn>(table.FileNames.Count + 1)
        {
            new() { Header = "Name", ToolTip = "" },
        };
        foreach (string fileName in table.FileNames)
        {
            columns.Add(new ResultsColumn
            {
                Header = ResultsFileHeader.FormatCompact(fileName),
                ToolTip = fileName,
            });
        }

        var scoreValues = new List<int>();
        int nameRows = table.Names.Count;
        for (int row = 0; row < nameRows && row < RosterGridLayout.RosterRowCount; row++)
        {
            for (int file = 0; file < table.FileNames.Count; file++)
            {
                scoreValues.Add(table.Scores[row, file]);
            }
        }

        bool heatOn = ScoreHeat.TryGetRange(scoreValues, out double heatMin, out double heatMax);
        var rows = new List<ResultsGridRow>(RosterGridLayout.TotalRowIndex + 1);
        for (int row = 0; row < RosterGridLayout.RosterRowCount; row++)
        {
            bool ace = RosterGridLayout.IsAceRow(row);
            bool hasName = row < nameRows;
            var cells = new List<ResultsCell>(columns.Count)
            {
                new()
                {
                    Text = hasName ? table.Names[row] : "",
                    Value = hasName ? table.Names[row] : null,
                    Bold = ace,
                },
            };

            for (int file = 0; file < table.FileNames.Count; file++)
            {
                bool hasScore = row < table.TrainedCharaIds.Count;
                int score = hasScore ? table.Scores[row, file] : 0;
                cells.Add(new ResultsCell
                {
                    Text = hasScore ? score.ToString() : "",
                    Value = hasScore ? score : null,
                    Heat = hasScore && heatOn ? ScoreHeat.ColorFor(score, heatMin, heatMax, theme) : null,
                });
            }

            rows.Add(new ResultsGridRow
            {
                IsTeamBoundary = RosterGridLayout.IsDefaultTeamBoundary(row),
                Cells = cells,
            });
        }

        var totalCells = new List<ResultsCell>(columns.Count)
        {
            new() { Text = "Total", Value = "Total" },
        };
        for (int file = 0; file < table.Totals.Count; file++)
        {
            int total = table.Totals[file];
            totalCells.Add(new ResultsCell
            {
                Text = total.ToString(),
                Value = total,
            });
        }

        rows.Add(new ResultsGridRow
        {
            IsFooter = true,
            IsTeamBoundary = true,
            Cells = totalCells,
        });

        return new ResultsScreen(
            ResultsGridKind.RosterStats,
            columns,
            rows,
            outliers: null,
            stableCore: Array.Empty<StableRosterUma>());
    }

    private static ResultsScreen LoadRosterMismatch(IReadOnlyList<KeyValuePair<string, TeamTrialResult>> files)
    {
        if (files.Count == 0)
        {
            return Empty;
        }

        var trials = new List<TeamTrialResult>(files.Count);
        foreach (KeyValuePair<string, TeamTrialResult> file in files)
        {
            trials.Add(file.Value);
        }

        RosterConsistencyReport consistency = RosterConsistencyAnalyzer.Analyze(trials);
        var columns = new List<ResultsColumn>(files.Count);
        foreach (KeyValuePair<string, TeamTrialResult> file in files)
        {
            columns.Add(new ResultsColumn
            {
                Header = ResultsFileHeader.FormatCompact(file.Key),
                ToolTip = file.Key,
            });
        }

        var rows = new List<ResultsGridRow>(RosterGridLayout.RosterRowCount);
        for (int row = 0; row < RosterGridLayout.RosterRowCount; row++)
        {
            bool ace = RosterGridLayout.IsAceRow(row);
            var cells = new List<ResultsCell>(files.Count);
            for (int file = 0; file < files.Count; file++)
            {
                IReadOnlyList<string> names = files[file].Value.RosterNames;
                bool hasName = row < names.Count;
                cells.Add(new ResultsCell
                {
                    Text = hasName ? names[row] : "",
                    Value = hasName ? names[row] : null,
                    Bold = ace,
                    Outlier = file < consistency.Outliers.GetLength(0)
                        && row < consistency.Outliers.GetLength(1)
                        && consistency.Outliers[file, row],
                });
            }

            rows.Add(new ResultsGridRow
            {
                IsTeamBoundary = RosterGridLayout.IsDefaultTeamBoundary(row),
                Cells = cells,
            });
        }

        return new ResultsScreen(
            ResultsGridKind.RosterMismatch,
            columns,
            rows,
            consistency.Outliers,
            consistency.StableCore);
    }
}
