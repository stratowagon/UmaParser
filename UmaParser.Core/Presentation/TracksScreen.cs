using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.ObjectModel;
using UmaParser.Settings;

namespace UmaParser.Presentation;

/// <summary>One entry in the Tracks uma selector.</summary>
public sealed class TracksUmaOption
{
    public required int TrainedCharaId { get; init; }
    public required string Name { get; init; }

    /// <summary>Set for Champions Meet, Room Match, and Practice. Null for Team Trials.</summary>
    public UmaIdentity? Identity { get; init; }

    /// <summary>The selector shows this. A combo box with no item template falls back to it.</summary>
    public override string ToString() => Name;
}

/// <summary>
/// The Tracks tab after a drop: which umas are in the selector, which one is selected,
/// the grid rows (including the total row), the visible columns, and the summary line.
/// The WinForms form and the WPF <c>TracksViewModel</c> both display this. They do not
/// decide the list themselves.
/// A roster mismatch keeps the "consistent uma(s)" summary until the user picks a
/// different uma. Champions Meet, Room Match, and Practice show the per-uma report
/// line immediately. That is what the WinForms Tracks tab does. Skills pins a sentence
/// for those single-race files too. Tracks does not.
/// </summary>
public sealed class TracksScreen
{
    private readonly IReadOnlyList<TeamTrialResult> _trials;
    private readonly IReadOnlyList<SingleRaceParticipants> _races;
    private readonly GridColumnGridPreferences _preferences;
    private readonly bool _teamTrial;

    private TracksScreen(
        IReadOnlyList<TracksUmaOption> umas,
        int selectedIndex,
        string summary,
        IReadOnlyList<TracksRow> rows,
        IReadOnlyList<TracksGridRow> gridRows,
        IReadOnlyList<GridColumnDefinition<TracksRow>> columns,
        TracksReport? report,
        TracksTotals? footer,
        IReadOnlyList<TeamTrialResult> trials,
        IReadOnlyList<SingleRaceParticipants> races,
        GridColumnGridPreferences preferences,
        bool teamTrial)
    {
        Umas = umas;
        SelectedIndex = selectedIndex;
        Summary = summary;
        Rows = rows;
        GridRows = gridRows;
        Columns = columns;
        Report = report;
        Footer = footer;
        _trials = trials;
        _races = races;
        _preferences = preferences;
        _teamTrial = teamTrial;
    }

    public IReadOnlyList<TracksUmaOption> Umas { get; }
    public int SelectedIndex { get; }
    public string Summary { get; }

    /// <summary>Track rows in analyzer order: course name, then distance. The total row is not included.</summary>
    public IReadOnlyList<TracksRow> Rows { get; }

    /// <summary>
    /// <see cref="Rows"/> formatted for the grid, with the total row last.
    /// Empty when there is nothing to total.
    /// </summary>
    public IReadOnlyList<TracksGridRow> GridRows { get; }

    public IReadOnlyList<GridColumnDefinition<TracksRow>> Columns { get; }
    public TracksReport? Report { get; }

    /// <summary>Race-weighted totals. Null when <see cref="Rows"/> is empty, so the grid has no total row.</summary>
    public TracksTotals? Footer { get; }

    /// <summary>True when the drop is Team Trials, including a roster mismatch.</summary>
    public bool IsTeamTrial => _teamTrial;

    public static TracksScreen Empty { get; } = new(
        Array.Empty<TracksUmaOption>(),
        selectedIndex: -1,
        summary: "",
        rows: Array.Empty<TracksRow>(),
        gridRows: Array.Empty<TracksGridRow>(),
        columns: Array.Empty<GridColumnDefinition<TracksRow>>(),
        report: null,
        footer: null,
        trials: Array.Empty<TeamTrialResult>(),
        races: Array.Empty<SingleRaceParticipants>(),
        preferences: new GridColumnGridPreferences(),
        teamTrial: false);

    /// <summary>
    /// Builds the Tracks selector, grid, and summary for a drop.
    /// </summary>
    /// <param name="singleRaceLoad">
    /// Pass the load the caller already built for a mixed drop so the sim blobs are not parsed twice.
    /// Null collects them here.
    /// </param>
    public static TracksScreen Load(
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

                List<TracksUmaOption> umas = RosterListing.FromTrial(trials[0])
                    .Select(entry => new TracksUmaOption
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
                List<TracksUmaOption> umas = consistency.StableCore
                    .Select(uma => new TracksUmaOption
                    {
                        TrainedCharaId = uma.TrainedCharaId,
                        Name = uma.DisplayName,
                    })
                    .ToList();
                string pinned = TrackCellText.ConsistentUmas(umas.Count);
                int index = umas.Count > 0 ? 0 : -1;
                return Build(umas, index, trials, Array.Empty<SingleRaceParticipants>(), preferences, teamTrial: true, pinnedSummary: pinned);
            }
            case TeamTrialBatchKind.MixedOrNonTT:
            {
                NonTeamTrialLoad load = singleRaceLoad ?? NonTeamTrialLoad.Collect(batch.SingleRaceImports);
                List<TracksUmaOption> umas = load.Identities
                    .Select(identity => new TracksUmaOption
                    {
                        TrainedCharaId = identity.TrainedCharaId,
                        Name = identity.GetDisplayName(),
                        Identity = identity,
                    })
                    .ToList();
                int index = umas.Count > 0 ? 0 : -1;
                return Build(
                    umas,
                    index,
                    Array.Empty<TeamTrialResult>(),
                    load.Races,
                    preferences,
                    teamTrial: false,
                    pinnedSummary: null);
            }
            default:
                return Empty;
        }
    }

    /// <summary>
    /// Another uma from the same drop. The summary switches to that uma's track report.
    /// The mismatch sentence stays only until this is called.
    /// </summary>
    public TracksScreen Select(int index)
    {
        if (index < 0 || index >= Umas.Count || index == SelectedIndex)
        {
            return this;
        }

        return Build(Umas, index, _trials, _races, _preferences, _teamTrial, pinnedSummary: null);
    }

    /// <summary>
    /// Same drop and selected uma, with a new column order. When a report is showing, the
    /// summary becomes that report line, which is what the WinForms grid rebind writes.
    /// A selection with no report keeps its summary.
    /// </summary>
    public TracksScreen WithPreferences(GridColumnGridPreferences preferences)
    {
        if (Report == null)
        {
            return new TracksScreen(
                Umas,
                SelectedIndex,
                Summary,
                Rows,
                GridRows,
                Columns,
                Report,
                Footer,
                _trials,
                _races,
                preferences,
                _teamTrial);
        }

        var context = new GridColumnContext { IsTeamTrial = _teamTrial };
        IReadOnlyList<GridColumnDefinition<TracksRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(TracksColumnSet.All, preferences, context);
        TracksTotals? footer = Report.Rows.Count > 0
            ? TracksTotals.Compute(Report.Rows, _teamTrial)
            : null;
        IReadOnlyList<TracksGridRow> gridRows = TracksGridRows.Build(Report.Rows, _teamTrial);
        return new TracksScreen(
            Umas,
            SelectedIndex,
            TrackCellText.Report(Report),
            Report.Rows,
            gridRows,
            columns,
            Report,
            footer,
            _trials,
            _races,
            preferences,
            _teamTrial);
    }

    private static TracksScreen Build(
        IReadOnlyList<TracksUmaOption> umas,
        int index,
        IReadOnlyList<TeamTrialResult> trials,
        IReadOnlyList<SingleRaceParticipants> races,
        GridColumnGridPreferences preferences,
        bool teamTrial,
        string? pinnedSummary)
    {
        if (index < 0 || index >= umas.Count)
        {
            return new TracksScreen(
                umas,
                -1,
                pinnedSummary ?? "",
                Array.Empty<TracksRow>(),
                Array.Empty<TracksGridRow>(),
                Array.Empty<GridColumnDefinition<TracksRow>>(),
                null,
                null,
                trials,
                races,
                preferences,
                teamTrial);
        }

        TracksUmaOption uma = umas[index];
        TracksReport? report = TryAnalyze(uma, trials, races, teamTrial);
        if (report == null)
        {
            return new TracksScreen(
                umas,
                index,
                pinnedSummary ?? UnavailableSummary(uma, races),
                Array.Empty<TracksRow>(),
                Array.Empty<TracksGridRow>(),
                Array.Empty<GridColumnDefinition<TracksRow>>(),
                null,
                null,
                trials,
                races,
                preferences,
                teamTrial);
        }

        var context = new GridColumnContext { IsTeamTrial = teamTrial };
        IReadOnlyList<GridColumnDefinition<TracksRow>> columns =
            GridColumnLayout.ResolveEffectiveColumns(TracksColumnSet.All, preferences, context);
        TracksTotals? footer = report.Rows.Count > 0
            ? TracksTotals.Compute(report.Rows, teamTrial)
            : null;
        IReadOnlyList<TracksGridRow> gridRows = TracksGridRows.Build(report.Rows, teamTrial);

        return new TracksScreen(
            umas,
            index,
            pinnedSummary ?? TrackCellText.Report(report),
            report.Rows,
            gridRows,
            columns,
            report,
            footer,
            trials,
            races,
            preferences,
            teamTrial);
    }

    private static TracksReport? TryAnalyze(
        TracksUmaOption uma,
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

            return TracksAnalyzer.Analyze(trials, uma.TrainedCharaId, uma.Name);
        }

        if (uma.Identity == null || races.Count == 0)
        {
            return null;
        }

        List<(RaceAppearance Appearance, int Distance, string TrackName)> appearances =
            RaceAppearanceBuilder.ForTracks(races, uma.Identity);
        if (appearances.Count == 0)
        {
            return null;
        }

        return TracksAnalyzer.Analyze(appearances, uma.Name);
    }

    private static string UnavailableSummary(TracksUmaOption uma, IReadOnlyList<SingleRaceParticipants> races)
    {
        if (uma.Identity != null && races.Count > 0)
        {
            return TrackCellText.NoAppearances(uma.Name);
        }

        return TrackCellText.Unavailable(uma.Name);
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
