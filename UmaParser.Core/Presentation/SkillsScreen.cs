using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Settings;

namespace UmaParser.Presentation;

/// <summary>One entry in the Skills uma selector.</summary>
public sealed class SkillsUmaOption
{
    public required int TrainedCharaId { get; init; }
    public required string Name { get; init; }

    /// <summary>Set for Champions Meet, Room Match, and Practice. Null for Team Trials.</summary>
    public UmaIdentity? Identity { get; init; }
}

/// <summary>
/// The Skills tab after a drop: which umas are in the selector, which one is selected,
/// the grid rows, the visible columns, and the summary line.
/// The WinForms form and the WPF <c>SkillsViewModel</c> both display this. They do not
/// decide the list themselves.
/// </summary>
public sealed class SkillsScreen
{
    private readonly IReadOnlyList<TeamTrialResult> _trials;
    private readonly IReadOnlyList<SingleRaceParticipants> _races;
    private readonly GridColumnGridPreferences _preferences;
    private readonly bool _teamTrial;

    private SkillsScreen(
        IReadOnlyList<SkillsUmaOption> umas,
        int selectedIndex,
        string summary,
        IReadOnlyList<SkillActivationRow> rows,
        IReadOnlyList<GridColumnDefinition<SkillActivationRow>> columns,
        SkillActivationReport? report,
        IReadOnlyList<TeamTrialResult> trials,
        IReadOnlyList<SingleRaceParticipants> races,
        GridColumnGridPreferences preferences,
        bool teamTrial)
    {
        Umas = umas;
        SelectedIndex = selectedIndex;
        Summary = summary;
        Rows = rows;
        Columns = columns;
        Report = report;
        _trials = trials;
        _races = races;
        _preferences = preferences;
        _teamTrial = teamTrial;
    }

    public IReadOnlyList<SkillsUmaOption> Umas { get; }
    public int SelectedIndex { get; }
    public string Summary { get; }

    /// <summary>Rows in the same order as the WinForms default header sort.</summary>
    public IReadOnlyList<SkillActivationRow> Rows { get; }

    public IReadOnlyList<GridColumnDefinition<SkillActivationRow>> Columns { get; }
    public SkillActivationReport? Report { get; }

    /// <summary>True when the drop is Team Trials, including a roster mismatch.</summary>
    public bool IsTeamTrial => _teamTrial;

    public static SkillsScreen Empty { get; } = new(
        Array.Empty<SkillsUmaOption>(),
        selectedIndex: -1,
        summary: "",
        rows: Array.Empty<SkillActivationRow>(),
        columns: Array.Empty<GridColumnDefinition<SkillActivationRow>>(),
        report: null,
        trials: Array.Empty<TeamTrialResult>(),
        races: Array.Empty<SingleRaceParticipants>(),
        preferences: new GridColumnGridPreferences(),
        teamTrial: false);

    /// <summary>
    /// Builds the Skills selector, grid, and summary for a drop.
    /// </summary>
    /// <param name="singleRaceLoad">
    /// Pass the load the caller already built for a mixed drop so the sim blobs are not parsed twice.
    /// Null collects them here.
    /// </param>
    public static SkillsScreen Load(
        TeamTrialBatch batch,
        GridColumnGridPreferences preferences,
        NonTeamTrialLoad? singleRaceLoad = null)
    {
        switch (batch.Kind)
        {
            case TeamTrialBatchKind.RosterStats:
            {
                List<TeamTrialResult> trials = CopyTrials(batch);
                if (trials.Count == 0)
                {
                    return Empty;
                }

                List<SkillsUmaOption> umas = RosterListing.FromTrial(trials[0])
                    .Select(entry => new SkillsUmaOption
                    {
                        TrainedCharaId = entry.TrainedCharaId,
                        Name = entry.Name,
                    })
                    .ToList();
                return Build(umas, 0, trials, Array.Empty<SingleRaceParticipants>(), preferences, teamTrial: true, pinnedSummary: null);
            }
            case TeamTrialBatchKind.RosterMismatch:
            {
                List<TeamTrialResult> trials = CopyTrials(batch);
                RosterConsistencyReport consistency = RosterConsistencyAnalyzer.Analyze(trials);
                List<SkillsUmaOption> umas = consistency.StableCore
                    .Select(uma => new SkillsUmaOption
                    {
                        TrainedCharaId = uma.TrainedCharaId,
                        Name = uma.DisplayName,
                    })
                    .ToList();
                string pinned = umas.Count > 0
                    ? $"{umas.Count} consistent uma(s) across all {trials.Count} files (same team + style; position within team may vary)."
                    : "No umas are consistent (same team position + style) across all files.";
                int index = umas.Count > 0 ? 0 : -1;
                return Build(umas, index, trials, Array.Empty<SingleRaceParticipants>(), preferences, teamTrial: true, pinnedSummary: pinned);
            }
            case TeamTrialBatchKind.MixedOrNonTT:
            {
                NonTeamTrialLoad load = singleRaceLoad ?? NonTeamTrialLoad.Collect(batch.SingleRaceImports);
                List<SkillsUmaOption> umas = load.Identities
                    .Select(identity => new SkillsUmaOption
                    {
                        TrainedCharaId = identity.TrainedCharaId,
                        Name = identity.GetDisplayName(),
                        Identity = identity,
                    })
                    .ToList();
                string pinned = umas.Count > 0
                    ? $"{umas.Count} local player uma(s) across {batch.SingleRaceImports.Count} single-race file(s). Select one to see skill performance."
                    : "No local player umas detected in the loaded non-TT captures.";
                int index = umas.Count > 0 ? 0 : -1;
                return Build(
                    umas,
                    index,
                    Array.Empty<TeamTrialResult>(),
                    load.Races,
                    preferences,
                    teamTrial: false,
                    pinnedSummary: pinned);
            }
            default:
                return Empty;
        }
    }

    /// <summary>
    /// Another uma from the same drop. The summary switches to that uma's skill report.
    /// The load summary (consistent-roster or single-race hint) stays only until this is called.
    /// </summary>
    public SkillsScreen Select(int index)
    {
        if (index < 0 || index >= Umas.Count || index == SelectedIndex)
        {
            return this;
        }

        return Build(Umas, index, _trials, _races, _preferences, _teamTrial, pinnedSummary: null);
    }

    private static SkillsScreen Build(
        IReadOnlyList<SkillsUmaOption> umas,
        int index,
        IReadOnlyList<TeamTrialResult> trials,
        IReadOnlyList<SingleRaceParticipants> races,
        GridColumnGridPreferences preferences,
        bool teamTrial,
        string? pinnedSummary)
    {
        if (index < 0 || index >= umas.Count)
        {
            return new SkillsScreen(
                umas,
                -1,
                pinnedSummary ?? "",
                Array.Empty<SkillActivationRow>(),
                Array.Empty<GridColumnDefinition<SkillActivationRow>>(),
                null,
                trials,
                races,
                preferences,
                teamTrial);
        }

        SkillsUmaOption uma = umas[index];
        SkillActivationReport? report = TryAnalyze(uma, trials, races, teamTrial);
        if (report == null)
        {
            string summary = pinnedSummary ?? UnavailableSummary(uma, races);
            return new SkillsScreen(
                umas,
                index,
                summary,
                Array.Empty<SkillActivationRow>(),
                Array.Empty<GridColumnDefinition<SkillActivationRow>>(),
                null,
                trials,
                races,
                preferences,
                teamTrial);
        }

        var context = new GridColumnContext
        {
            IsTeamTrial = teamTrial,
            HasSkillLotMetadata = report.HasSkillLotMetadata,
        };
        IReadOnlyList<GridColumnDefinition<SkillActivationRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(SkillsColumnSet.All, preferences, context);
        bool pointsVisible = columns.Any(column => column.Id == GridColumnIds.Skills.PtsRace);
        List<SkillActivationRow> rows = SortRows(report.Rows, pointsVisible);

        return new SkillsScreen(
            umas,
            index,
            pinnedSummary ?? FormatReport(report),
            rows,
            columns,
            report,
            trials,
            races,
            preferences,
            teamTrial);
    }

    private static SkillActivationReport? TryAnalyze(
        SkillsUmaOption uma,
        IReadOnlyList<TeamTrialResult> trials,
        IReadOnlyList<SingleRaceParticipants> races,
        bool teamTrial)
    {
        if (teamTrial)
        {
            if (trials.Count == 0)
            {
                return null;
            }

            return SkillActivationAnalyzer.Analyze(trials, uma.TrainedCharaId, uma.Name);
        }

        if (uma.Identity == null || races.Count == 0)
        {
            return null;
        }

        List<RaceAppearance> appearances = RaceAppearanceBuilder.ForSkills(races, uma.Identity);
        if (appearances.Count == 0)
        {
            return null;
        }

        return SkillActivationAnalyzer.Analyze(appearances, uma.Name);
    }

    private static string UnavailableSummary(SkillsUmaOption uma, IReadOnlyList<SingleRaceParticipants> races)
    {
        if (uma.Identity != null && races.Count > 0)
        {
            return $"{uma.Name}: No appearances found.";
        }

        return $"{uma.Name}: analysis not available for this selection.";
    }

    /// <summary>Same sentence the WinForms Skills tab writes under the uma selector.</summary>
    public static string FormatReport(SkillActivationReport report) =>
        $"{report.UmaName}: {report.Rows.Count} skills, {report.RaceCount} race(s). " +
        $"Click column headers to sort. " +
        $"Avg wit {report.AverageWit} → {report.ExpectedWitPercentForUma:0.#}% activation rate. ";

    private static List<SkillActivationRow> SortRows(IReadOnlyList<SkillActivationRow> rows, bool pointsColumnVisible)
    {
        // WinForms sorts by Pts/race when that column exists, otherwise by Activations. Both descending.
        if (pointsColumnVisible)
        {
            return rows.OrderByDescending(row => row.PointsPerRace).ToList();
        }

        return rows.OrderByDescending(row => row.ActivationCount).ToList();
    }

    private static List<TeamTrialResult> CopyTrials(TeamTrialBatch batch)
    {
        var trials = new List<TeamTrialResult>(batch.Trials.Count);
        foreach (TeamTrialResult trial in batch.Trials.Values)
        {
            trials.Add(trial);
        }

        return trials;
    }
}
