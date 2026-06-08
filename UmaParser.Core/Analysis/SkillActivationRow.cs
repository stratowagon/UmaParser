using UmaBlobber.MasterData;

namespace UmaBlobber.Analysis;

public sealed class SkillActivationRow
{
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public int ActivationCount { get; init; }
    /// <summary>
    /// Total attributed points from matched skill activation score events (raw_score 26-57 / condition_type 8)
    /// across all races. Each individual score point value is paired 1:1 with a co-timed skill proc
    /// (in event log order within the same FrameTime). No multiplication or even-splitting occurs.
    /// White activations contribute their recorded 500, gold 1200, uniques the variable amount (level + rarity).
    /// </summary>
    public double TotalPointsEarned { get; init; }

    /// <summary>
    /// Observed team-trial skill points per race (TotalPointsEarned ÷ RaceCount).
    /// Because the scenario log uses 1 Hz ticks for most of the race, multiple procs can share a FrameTime;
    /// points are assigned via 1:1 consumption of the actual per-activation score values rather than summed+split.
    /// </summary>
    public double PointsPerRace { get; init; }

    public int RaceCount { get; init; }

    /// <summary>Total activations ÷ races (can exceed 100% if multiple procs per race).</summary>
    public double ActivationRatePercent { get; init; }

    /// <summary>Races with at least one activation ÷ races (0–100%).</summary>
    public double PerRaceActivationRatePercent { get; init; }

    public SkillActivateLotKind ActivateLot { get; init; }

    /// <summary>Pre-race wit activation chance when <see cref="ActivateLot"/> is <see cref="SkillActivateLotKind.Wit"/>.</summary>
    public double? ExpectedWitPercent { get; init; }

    /// <summary><see cref="PerRaceActivationRatePercent"/> − <see cref="ExpectedWitPercent"/> when wit-gated.</summary>
    public double? WitDeltaPercent { get; init; }

    public bool IsLowActivation { get; init; }

    /// <summary>Per-race rate materially below wit activation expectation.</summary>
    public bool IsUnderperformingVsWit { get; init; }
}