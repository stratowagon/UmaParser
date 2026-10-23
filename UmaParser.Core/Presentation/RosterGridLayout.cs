using UmaParser.Analysis;

namespace UmaParser.Presentation;

/// <summary>
/// Row rules for the 15-uma Results and Team Analysis grids.
/// Ace is the first uma of each 3-uma team. A darker line sits above each later team,
/// and above the Results total row.
/// </summary>
public static class RosterGridLayout
{
    public const int UmaPerTeam = 3;

    public static int RosterRowCount => RosterConsistencyAnalyzer.SlotCount;

    public static int TotalRowIndex => RosterRowCount;

    public static bool IsAceRow(int rowIndex) =>
        rowIndex >= 0 && rowIndex < RosterRowCount && rowIndex % UmaPerTeam == 0;

    /// <summary>Rows 3, 6, 9, and 12. The total row is separate: it is index 15.</summary>
    public static bool IsDefaultTeamBoundary(int rowIndex) =>
        rowIndex is 3 or 6 or 9 or 12;
}
