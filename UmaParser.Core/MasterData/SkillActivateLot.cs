namespace UmaParser.MasterData;

/// <summary>
/// Whether a skill requires a wit check (<c>skill_data.activate_lot</c>).
/// </summary>
public enum SkillActivateLotKind
{
    Unknown,
    /// <summary>No wit check; skill is always eligible (race conditions still apply).</summary>
    Unconditional,
    /// <summary>Wit check chance calculated pre-race: max(20, 100 − 9000/baseWit)%.</summary>
    Wit,
}