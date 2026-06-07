namespace UmaBlobber.MasterData;

internal sealed class MasterCacheFile
{
    public string? SourcePath { get; set; }
    public DateTime? SourceLastWriteUtc { get; set; }
    public DateTime ExportedUtc { get; set; }
    public Dictionary<string, Dictionary<int, string>> Sections { get; set; } = new();

    public Dictionary<string, CachedSkillEntry>? SkillEntries { get; set; }

    /// <summary>Legacy cache format: skill id → <see cref="SkillActivateLotKind"/> name.</summary>
    public Dictionary<string, string>? SkillActivateLot { get; set; }

    /// <summary>team_stadium_raw_score id (as string) → base score value.</summary>
    public Dictionary<string, int>? TeamTrialsRawScores { get; set; }
}

internal sealed class CachedSkillEntry
{
    public string Name { get; set; } = string.Empty;
    public string ActivateLot { get; set; } = nameof(SkillActivateLotKind.Unknown);
}