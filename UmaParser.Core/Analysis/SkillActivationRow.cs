using UmaBlobber.MasterData;

namespace UmaBlobber.Analysis;

public sealed class SkillActivationRow
{
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public int ActivationCount { get; init; }
    public int TotalPointsEarned { get; init; }

    /// <summary>Observed skill points ÷ races (from race score events).</summary>
    public double PointsPerRace { get; init; }

    public int RaceCount { get; init; }

    /// <summary>Total activations ÷ races (can exceed 100% if multiple procs per race).</summary>
    public double ActivationRatePercent { get; init; }

    /// <summary>Races with at least one activation ÷ races (0–100%).</summary>
    public double PerRaceActivationRatePercent { get; init; }

    public SkillActivateLotKind ActivateLot { get; init; }

    /// <summary>Pre-race wit lottery chance when <see cref="ActivateLot"/> is <see cref="SkillActivateLotKind.Wit"/>.</summary>
    public double? ExpectedWitPercent { get; init; }

    /// <summary><see cref="PerRaceActivationRatePercent"/> − <see cref="ExpectedWitPercent"/> when wit-gated.</summary>
    public double? WitDeltaPercent { get; init; }

    public bool IsLowActivation { get; init; }

    /// <summary>Per-race rate materially below wit lottery expectation.</summary>
    public bool IsUnderperformingVsWit { get; init; }
}