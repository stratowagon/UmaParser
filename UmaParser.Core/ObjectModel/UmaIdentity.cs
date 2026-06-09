using UmaBlobber.MasterData;

namespace UmaBlobber.ObjectModel;

/// <summary>
/// Stable identity for a specific veteran uma (trained character) across multiple races and capture files.
/// 
/// Internal keying prioritizes (OwnerViewerId, TrainedCharaId) when available.
/// This is sufficient to uniquely identify one specific build/variant owned by one trainer.
/// 
/// Display names favor in-game friendly identifiers (especially RankScore) per user preference.
/// </summary>
public sealed record UmaIdentity(
    long OwnerViewerId,
    string OwnerName,
    int TrainedCharaId,
    int CharaId,
    int CardId,
    int RankScore,
    string? Nickname = null,
    bool IsNpc = false,
    bool IsLocalPlayer = false)
{
    /// <summary>
    /// Returns true if this identity represents the same physical veteran uma as the other.
    /// 
    /// Primary: (OwnerViewerId, TrainedCharaId) when viewer ids are present.
    /// For the common case in these capture files (viewer ids stripped): owner name + TrainedCharaId
    /// is treated as sufficient, per user confirmation. This correctly distinguishes different
    /// builds/variants of the same character (different trained_chara_id or card).
    /// </summary>
    public bool IsSameUma(UmaIdentity? other)
    {
        if (other is null) return false;

        if (OwnerViewerId != 0 && other.OwnerViewerId != 0)
        {
            return OwnerViewerId == other.OwnerViewerId && TrainedCharaId == other.TrainedCharaId;
        }

        // Fallback for stripped captures (the normal case for CM/Room/Practice dumps).
        // Owner + TrainedCharaId is the stable key for a specific veteran uma.
        if (TrainedCharaId != 0 && other.TrainedCharaId != 0)
        {
            return string.Equals(OwnerName, other.OwnerName, StringComparison.OrdinalIgnoreCase)
                   && TrainedCharaId == other.TrainedCharaId;
        }

        // Last resort composite (different owners or very old data)
        return string.Equals(OwnerName, other.OwnerName, StringComparison.OrdinalIgnoreCase)
               && CharaId == other.CharaId
               && CardId == other.CardId
               && RankScore == other.RankScore;
    }

    /// <summary>
    /// Human-friendly name suitable for dropdowns (Skills/Tracks selectors).
    /// Examples:
    ///   Local:   "Oguri Cap [RS 17855]"
    ///   Other:   "Oguri Cap (Bojak [RS 12345])"   (only if includeOwner)
    ///   Compact: "Oguri [17855]" (for tighter spaces)
    /// </summary>
    public string GetDisplayName(bool includeOwner = false, bool compact = false)
    {
        var catalog = GameMasterService.Current.Catalog;
        string shortName = catalog.FormatCharaShortName(CharaId);

        string disambig = "";
        if (RankScore > 0)
        {
            // Preferred: rank score in parentheses, no "RS" prefix
            disambig = $"({RankScore})";
        }
        else if (CardId > 0)
        {
            // Fallback differentiator (card variant) - only if no rank score
            disambig = compact ? $"({CardId % 100:00})" : $"(var {CardId % 100:00})";
        }

        if (compact)
        {
            return string.IsNullOrEmpty(disambig) ? shortName : $"{shortName} {disambig}";
        }

        if (includeOwner && !string.IsNullOrWhiteSpace(OwnerName) && !IsLocalPlayer)
        {
            // Brackets reserved for future owner name support, e.g. "Oguri Cap (Bojak) [16340]"
            return $"{shortName} ({OwnerName}) {disambig}".Trim();
        }

        return $"{shortName} {disambig}".Trim();
    }

    /// <summary>
    /// Short name only (no disambiguation). Useful when context (grid position, etc.) already provides identity.
    /// </summary>
    public string ShortName => GameMasterService.Current.Catalog.FormatCharaShortName(CharaId);

    public override string ToString() => GetDisplayName(includeOwner: !IsLocalPlayer);
}
