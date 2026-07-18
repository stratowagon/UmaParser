using System.Text.Json;
using UmaParser.DataModel.RaceScenario;
using UmaParser.DataModel.ResponseData;
using UmaParser.MasterData;
using UmaParser.ObjectModel;

namespace UmaParser.Import;

/// <summary>
/// Helpers to pull participant / ownership information out of the normalized JSON
/// from Champions Meeting, Room Match, and Practice Room captures.
///
/// These dumps are much richer than TT responses and contain explicit signals
/// (PlayerTeamMemberArray + npc_type + _isPlayer) for which horses belong to the local player.
///
/// Property names vary by HorseACT version:
/// - Older dumps: compiler backing fields (<c>&lt;PlayerTeamMemberArray&gt;k__BackingField</c>),
///   cleaned to PascalCase by <see cref="CaptureDecoder.NormalizeBackingFieldJson"/>.
/// - HorseACT ~1.1.3+: clean camelCase (<c>playerTeamMemberArray</c>, <c>responseHorseData</c>).
/// Structural lookups are therefore case-insensitive.
/// </summary>
public static class SingleRaceParticipantExtractor
{
    /// <summary>
    /// Attempts to extract all horses that belong to the local player from a single-race capture.
    /// Returns UmaIdentity + the raw horse element (so callers can pull stats/skills/frame_order easily)
    /// plus the pre-parsed simulation if the SimDataBase64 was available.
    /// </summary>
    public static SingleRaceParticipants ExtractLocalPlayerParticipants(string normalizedJson, string? simDataBase64)
    {
        var result = new SingleRaceParticipants
        {
            Simulation = !string.IsNullOrWhiteSpace(simDataBase64)
                ? RaceScenarioParser.Parse(simDataBase64)
                : new RaceScenarioData()
        };

        try
        {
            using var doc = JsonDocument.Parse(normalizedJson);
            var root = doc.RootElement;

            // Extract course/track info while the document is alive (for Tracks tab)
            if (TryGetPropertyCI(root, "RaceCourseSet", out var rcs) && rcs.ValueKind == JsonValueKind.Object)
            {
                result.Distance = GetIntCI(rcs, "Distance");
                int trackId = GetIntCI(rcs, "RaceTrackId");

                var catalog = GameMasterService.Current.Catalog;
                string trackName = $"Track {trackId}";
                if (catalog.TryGet(MasterTextCategory.RaceTrackName, trackId, out var realName) &&
                    !string.IsNullOrWhiteSpace(realName))
                {
                    trackName = realName;
                }
                result.TrackName = trackName;
            }
            else if (TryGetPropertyCI(root, "Distance", out var dEl) && dEl.TryGetInt32(out int d))
            {
                result.Distance = d;
            }

            // The authoritative source for the *current race's* local player horses is inside
            // PlayerTeamMemberArray. Each entry contains the full current "_responseHorseData"
            // (or "responseHorseData" after any further normalization) for that horse.
            // This avoids pulling in hundreds of historical TrainedChara objects from the
            // massive embedded career/succession data in these capture files.
            // Collect local owner names from PlayerTeamMemberArray so we can later
            // find additional player-owned umas in practice rooms (where the player
            // can bring more than 3 of their own umas, and PlayerTeamMemberArray
            // may only list the "main" / team-1 equivalents).
            var localOwnerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (TryGetPropertyCI(root, "PlayerTeamMemberArray", out var pta) && pta.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in pta.EnumerateArray())
                {
                    // The actual horse data is usually under "_responseHorseData" / "responseHorseData"
                    JsonElement horse = entry;
                    if (TryGetPropertyCI(entry, "responseHorseData", out var resp) && resp.ValueKind == JsonValueKind.Object)
                        horse = resp;
                    else if (TryGetPropertyCI(entry, "_responseHorseData", out var resp2) && resp2.ValueKind == JsonValueKind.Object)
                        horse = resp2;

                    // Verify it looks like a current race horse
                    if (!horse.TryGetProperty("trained_chara_id", out _) ||
                        !horse.TryGetProperty("frame_order", out _))
                        continue;

                    // Try to get rank score from both the entry (PlayerTeamMemberArray item) and the response horse data,
                    // because in practice room / CM captures the TrainedCharaData with _rankScore can be at either level.
                    int rankScore = ExtractRankScore(entry) ?? ExtractRankScore(horse) ?? 0;

                    var identity = BuildIdentityFromHorse(horse, rankScore);
                    if (identity != null)
                    {
                        // Mark as local player since it came from PlayerTeamMemberArray
                        var localId = identity with { IsLocalPlayer = true };
                        var mappedHorse = MapCaptureHorseToRaceHorseData(horse);
                        result.LocalPlayerHorses.Add((localId, mappedHorse));

                        if (!string.IsNullOrWhiteSpace(localId.OwnerName))
                            localOwnerNames.Add(localId.OwnerName);
                    }
                }
            }

            // For Practice rooms (and to a lesser extent Room Matches), the player
            // can bring more umas than fit in a "team". PlayerTeamMemberArray typically
            // only lists the "main" ones (first three, treated as team 1 by the game).
            // We therefore also include any other current-race horses that share the
            // same trainer/owner name as the ones from PlayerTeamMemberArray.
            // Guests from friends list will have a different owner name and will be
            // excluded (they will only appear if we decide to support non-local umas later).
            // NPCs usually have no trainer_name or npc_type == 0.
            if (localOwnerNames.Count > 0)
            {
                var currentRaceHorses = FindCurrentRaceHorseObjects(root);
                foreach (var horse in currentRaceHorses)
                {
                    string trainer = horse.TryGetProperty("trainer_name", out var tn) ? (tn.GetString() ?? "") : "";
                    if (string.IsNullOrWhiteSpace(trainer) || !localOwnerNames.Contains(trainer))
                        continue;

                    // Avoid duplicates already added from PlayerTeamMemberArray
                    var candidate = BuildIdentityFromHorse(horse);
                    if (candidate == null) continue;

                    bool alreadyAdded = result.LocalPlayerHorses.Any(x => x.Identity.IsSameUma(candidate));
                    if (alreadyAdded) continue;

                    var localId = candidate with { IsLocalPlayer = true };
                    var mappedHorse = MapCaptureHorseToRaceHorseData(horse);
                    result.LocalPlayerHorses.Add((localId, mappedHorse));
                }
            }

            // Final fallback: if we still have nothing (e.g. very old capture format
            // with no usable PlayerTeamMemberArray), fall back to explicit _isPlayer
            // markers. Do NOT treat npc_type == 11 alone as local — in CM/Room Match
            // every real trainer horse uses npc_type 11, so that would pull in opponents
            // and explode the Skills/Tracks dropdowns.
            if (result.LocalPlayerHorses.Count == 0)
            {
                var horseElements = FindCurrentRaceHorseObjects(root);
                var markedPlayer = new List<JsonElement>();
                var npcType11 = new List<JsonElement>();

                foreach (var horse in horseElements)
                {
                    bool isMarkedPlayer =
                        (TryGetPropertyCI(horse, "_isPlayer", out var ip) && ip.ValueKind == JsonValueKind.True) ||
                        (TryGetPropertyCI(horse, "isPlayer", out var ip2) && ip2.ValueKind == JsonValueKind.True);

                    if (isMarkedPlayer)
                    {
                        markedPlayer.Add(horse);
                        continue;
                    }

                    int npcType = horse.TryGetProperty("npc_type", out var nt) && nt.TryGetInt32(out int n) ? n : 0;
                    if (npcType == 11)
                        npcType11.Add(horse);
                }

                // Prefer explicit player markers. Only use npc_type==11 when it does not look like
                // a full multi-trainer lobby (all opponents also have 11).
                IEnumerable<JsonElement> fallbackHorses = markedPlayer;
                if (markedPlayer.Count == 0 && npcType11.Count > 0)
                {
                    var trainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var h in npcType11)
                    {
                        string t = h.TryGetProperty("trainer_name", out var tn) ? (tn.GetString() ?? "") : "";
                        if (!string.IsNullOrWhiteSpace(t))
                            trainers.Add(t);
                    }

                    // Single trainer (or no names) → safe to treat as local. Multiple trainers means
                    // CM/Room opponents and must be skipped rather than polluting the dropdown.
                    if (trainers.Count <= 1)
                        fallbackHorses = npcType11;
                }

                foreach (var horse in fallbackHorses)
                {
                    var identity = BuildIdentityFromHorse(horse);
                    if (identity != null)
                    {
                        var mappedHorse = MapCaptureHorseToRaceHorseData(horse);
                        result.LocalPlayerHorses.Add((identity with { IsLocalPlayer = true }, mappedHorse));
                    }
                }
            }
        }
        catch
        {
            // Best effort — return whatever we got (simulation at minimum).
        }

        return result;
    }

    /// <summary>
    /// Fallback collector that only looks for current-race level horse objects
    /// (those with skill_array + frame_order at the response level). This avoids
    /// the deep historical data embedded in TrainedCharaData / race_result_list etc.
    /// </summary>
    private static List<JsonElement> FindCurrentRaceHorseObjects(JsonElement root)
    {
        var horses = new List<JsonElement>();

        void Walk(JsonElement el, int depth)
        {
            if (depth > 6) return; // don't go too deep into history

            if (el.ValueKind == JsonValueKind.Object)
            {
                if (LooksLikeCurrentRaceHorse(el))
                {
                    horses.Add(el);
                }

                foreach (var prop in el.EnumerateObject())
                    Walk(prop.Value, depth + 1);
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                    Walk(item, depth + 1);
            }
        }

        Walk(root, 0);
        return horses;
    }

    private static bool LooksLikeCurrentRaceHorse(JsonElement el)
    {
        // Current race horses have the live skill_array + frame_order + trainer_name
        // at this level. Historical entries usually don't match all of these together.
        return el.TryGetProperty("trained_chara_id", out _) &&
               el.TryGetProperty("frame_order", out _) &&
               el.TryGetProperty("skill_array", out var sa) && sa.ValueKind == JsonValueKind.Array &&
               el.TryGetProperty("trainer_name", out _);
    }

    private static UmaIdentity? BuildIdentityFromHorse(JsonElement horse, int? forcedRankScore = null)
    {
        int trained = horse.TryGetProperty("trained_chara_id", out var t) && t.TryGetInt32(out int tv) ? tv : 0;
        int chara = horse.TryGetProperty("chara_id", out var c) && c.TryGetInt32(out int cv) ? cv : 0;
        int card = horse.TryGetProperty("card_id", out var cd) && cd.TryGetInt32(out int cdv) ? cdv : 0;
        string trainer = horse.TryGetProperty("trainer_name", out var tn) ? (tn.GetString() ?? "") : "";

        int rankScore = forcedRankScore ?? ExtractRankScore(horse) ?? 0;

        int npc = horse.TryGetProperty("npc_type", out var n) && n.TryGetInt32(out int nv) ? nv : 0;

        if (chara == 0) return null;

        return new UmaIdentity(
            OwnerViewerId: 0, // usually stripped in these captures
            OwnerName: trainer,
            TrainedCharaId: trained,
            CharaId: chara,
            CardId: card,
            RankScore: rankScore,
            IsNpc: npc == 0,
            IsLocalPlayer: true);
    }

    /// <summary>
    /// Robustly extracts rank score from a horse/response element or its direct children.
    /// Looks for the common keys used in these capture dumps.
    /// Returns null if not found.
    /// </summary>
    private static int? ExtractRankScore(JsonElement el)
    {
        int rs;

        // Direct on this element
        if (TryGetIntCI(el, "rankScore", out rs)) return rs;
        if (TryGetIntCI(el, "_rankScore", out rs)) return rs;
        if (TryGetIntCI(el, "rank_score", out rs)) return rs;

        // Inside TrainedCharaData (common nesting; camelCase in newer HorseACT dumps)
        if (TryGetPropertyCI(el, "TrainedCharaData", out var tcd) ||
            TryGetPropertyCI(el, "_TrainedCharaData", out tcd))
        {
            if (TryGetIntCI(tcd, "rankScore", out rs)) return rs;
            if (TryGetIntCI(tcd, "_rankScore", out rs)) return rs;
        }

        // One level deeper search (for cases where the TrainedCharaData is a sibling on the PlayerTeamMemberArray entry)
        foreach (var prop in el.EnumerateObject())
        {
            var child = prop.Value;
            if (child.ValueKind == JsonValueKind.Object)
            {
                if (TryGetIntCI(child, "_rankScore", out rs) || TryGetIntCI(child, "rankScore", out rs))
                    return rs;

                // Also check inside TrainedCharaData at this level
                if (TryGetPropertyCI(child, "TrainedCharaData", out var nestedTcd) ||
                    TryGetPropertyCI(child, "_TrainedCharaData", out nestedTcd))
                {
                    if (TryGetIntCI(nestedTcd, "rankScore", out rs)) return rs;
                    if (TryGetIntCI(nestedTcd, "_rankScore", out rs)) return rs;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Case-insensitive property lookup. Needed because older HorseACT dumps use PascalCase
    /// (after backing-field cleanup) while newer ones serialize camelCase.
    /// Prefer exact match first for speed on the common path.
    /// </summary>
    private static bool TryGetPropertyCI(JsonElement el, string name, out JsonElement value)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (el.TryGetProperty(name, out value))
            return true;

        foreach (var prop in el.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetIntCI(JsonElement el, string name, out int value)
    {
        if (TryGetPropertyCI(el, name, out var p) && p.TryGetInt32(out int v))
        {
            value = v;
            return true;
        }
        value = 0;
        return false;
    }

    private static int GetIntCI(JsonElement el, string name)
        => TryGetIntCI(el, name, out int v) ? v : 0;

    /// <summary>
    /// Maps a horse object from a single-race capture (normalized) to the common RaceHorseData model
    /// used by the analysis engines.
    /// </summary>
    public static RaceHorseData MapCaptureHorseToRaceHorseData(JsonElement el)
    {
        var horse = new RaceHorseData();

        horse.FrameOrder = GetInt(el, "frame_order");
        horse.TrainedCharaId = GetInt(el, "trained_chara_id");
        horse.CharaId = GetInt(el, "chara_id");
        horse.CardId = GetInt(el, "card_id");
        horse.TrainerName = GetString(el, "trainer_name") ?? "";
        horse.NpcType = GetInt(el, "npc_type");
        horse.SingleModeCharaId = GetInt(el, "single_mode_chara_id");

        horse.Speed = GetInt(el, "speed");
        horse.Stamina = GetInt(el, "stamina");
        horse.Power = GetInt(el, "pow");
        horse.Guts = GetInt(el, "guts");
        horse.Wiz = GetInt(el, "wiz");
        horse.RunningStyle = GetInt(el, "running_style");

        // Skill array
        if (el.TryGetProperty("skill_array", out var sa) && sa.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in sa.EnumerateArray())
            {
                horse.SkillArray.Add(new Skill
                {
                    SkillId = GetInt(s, "skill_id"),
                    Level = GetInt(s, "level")
                });
            }
        }

        // Propers (best effort)
        horse.ProperDistanceShort = GetInt(el, "proper_distance_short");
        horse.ProperDistanceMile = GetInt(el, "proper_distance_mile");
        horse.ProperDistanceMiddle = GetInt(el, "proper_distance_middle");
        horse.ProperDistanceLong = GetInt(el, "proper_distance_long");

        horse.ProperNige = GetInt(el, "proper_running_style_nige");
        horse.ProperSenko = GetInt(el, "proper_running_style_senko");
        horse.ProperSashi = GetInt(el, "proper_running_style_sashi");
        horse.ProperOikomi = GetInt(el, "proper_running_style_oikomi");

        horse.ProperTurf = GetInt(el, "proper_ground_turf");
        horse.ProperDirt = GetInt(el, "proper_ground_dirt");

        horse.Motivation = GetInt(el, "motivation");

        return horse;
    }

    private static int GetInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.TryGetInt32(out int v) ? v : 0;

    private static string GetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) ? p.GetString() ?? "" : "";
}

public sealed class SingleRaceParticipants
{
    public RaceScenarioData Simulation { get; set; } = new();

    /// <summary>
    /// Local player horses for this race, fully materialized (no JsonElement / JsonDocument references).
    /// This prevents ObjectDisposedException when the original document is disposed after extraction.
    /// </summary>
    public List<(UmaIdentity Identity, RaceHorseData Horse)> LocalPlayerHorses { get; } = new();

    public int Distance { get; set; }
    public string TrackName { get; set; } = "Unknown";
}
