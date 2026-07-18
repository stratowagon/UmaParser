namespace UmaParser.Settings;

/// <summary>Runtime context that determines which optional grid columns are available.</summary>
public sealed class GridColumnContext
{
    public bool IsTeamTrial { get; init; }

    public bool HasSkillLotMetadata { get; init; }

    public static GridColumnContext Empty { get; } = new();
}