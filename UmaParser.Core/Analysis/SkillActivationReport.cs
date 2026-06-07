namespace UmaBlobber.Analysis;

public sealed class SkillActivationReport
{
    public int TrainedCharaId { get; init; }
    public string UmaName { get; init; } = string.Empty;
    public int TrialCount { get; init; }
    public int RaceCount { get; init; }
    public int AverageWit { get; init; }
    public double ExpectedWitPercentForUma { get; init; }
    public bool HasSkillLotMetadata { get; init; }
    public IReadOnlyList<SkillActivationRow> Rows { get; init; } = Array.Empty<SkillActivationRow>();
}