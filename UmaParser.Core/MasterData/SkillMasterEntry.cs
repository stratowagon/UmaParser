namespace UmaParser.MasterData;

/// <summary>Per-skill master data beyond a display name (e.g. pre-race wit activation chance).</summary>
internal readonly record struct SkillMasterEntry(string Name, SkillActivateLotKind ActivateLot)
{
    public static SkillMasterEntry NameOnly(string name) => new(name, SkillActivateLotKind.Unknown);
}