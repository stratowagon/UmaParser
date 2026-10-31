namespace UmaParser.DataModel.ResponseData
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;

    /// <summary>
    /// Uma data detailed enough for racing, but not as detailed as trained_chara.
    /// Found in race prep and result responses.
    /// </summary>
    public class RaceHorseData
    {
        /// <summary>
        /// The gate number assigned to this uma for the race.
        /// </summary>
        [JsonPropertyName("frame_order")]
        public int FrameOrder { get; set; }

        /// <summary>
        /// Account ID of the owning player.  0 for NPCs.
        /// HorseACT hook strips account IDs, so this is empty for them.
        /// </summary>
        [JsonPropertyName("viewer_id")]
        public long ViewerId { get; set; } = 0;

        /// <summary>
        /// The npc "type" enum for the uma.
        /// The player's own umas are type 11.  The opponent's are type 20.
        /// Actual NPCs are type 0.
        /// </summary>
        [JsonPropertyName("npc_type")]
        public int NpcType { get; set; } = 0;

        /// <summary>
        /// Owning player's display name.  Empty for NPCs.
        /// </summary>
        [JsonPropertyName("trainer_name")]
        public string TrainerName { get; set; } = string.Empty;

        /// <summary>
        /// Seems to be the same as trained_chara_id, which does not match what the veteran uma list shows.
        /// </summary>
        [JsonPropertyName("single_mode_chara_id")]
        public int SingleModeCharaId { get; set; }

        /// <summary>
        /// Local ID for the veteran uma.  This should match the trained_chara_id in the veteran uma list.
        /// </summary>
        [JsonPropertyName("trained_chara_id")]
        public int TrainedCharaId { get; set; }

        /// <summary>
        /// 4-digit character ID (no variants).  ie 1001 = Special Week.
        /// </summary>
        [JsonPropertyName("chara_id")]
        public int CharaId { get; set; }

        /// <summary>
        /// Portrait icon ID.  6-digit ID for character + variant.
        /// </summary>
        [JsonPropertyName("card_id")]
        public int CardId { get; set; }

        /// <summary>
        /// Uma's star level
        /// </summary>
        [JsonPropertyName("rarity")]
        public int Rarity { get; set; }

        /// <summary>
        /// (probably) Uma's potential level, 1-5.  NPCs are 1.
        /// </summary>
        [JsonPropertyName("talent_level")]
        public int TalentLevel { get; set; }

        /// <summary>
        /// Skills this uma has trained.  Skill IDs (text_data category 47) and skill levels.
        /// </summary>
        [JsonPropertyName("skill_array")]
        public List<Skill> SkillArray { get; set; } = new();

        [JsonPropertyName("stamina")]
        public int Stamina { get; set; }

        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        [JsonPropertyName("pow")]
        public int Power { get; set; }

        [JsonPropertyName("guts")]
        public int Guts { get; set; }

        [JsonPropertyName("wiz")]
        public int Wiz { get; set; }

        [JsonPropertyName("running_style")]
        public int RunningStyle { get; set; }

        // Proper distance / ground / style ratings
        [JsonPropertyName("proper_distance_short")] public int ProperDistanceShort { get; set; }
        [JsonPropertyName("proper_distance_mile")] public int ProperDistanceMile { get; set; }
        [JsonPropertyName("proper_distance_middle")] public int ProperDistanceMiddle { get; set; }
        [JsonPropertyName("proper_distance_long")] public int ProperDistanceLong { get; set; }

        [JsonPropertyName("proper_running_style_nige")] public int ProperNige { get; set; }
        [JsonPropertyName("proper_running_style_senko")] public int ProperSenko { get; set; }
        [JsonPropertyName("proper_running_style_sashi")] public int ProperSashi { get; set; }
        [JsonPropertyName("proper_running_style_oikomi")] public int ProperOikomi { get; set; }

        [JsonPropertyName("proper_ground_turf")] public int ProperTurf { get; set; }
        [JsonPropertyName("proper_ground_dirt")] public int ProperDirt { get; set; }

        /// <summary>
        /// Current mood, 1 (awful) to 5 (great)
        /// </summary>
        [JsonPropertyName("motivation")]
        public int Motivation { get; set; }

        /// <summary>
        /// (possibly) letter rank
        /// </summary>
        [JsonPropertyName("final_grade")]
        public int FinalGrade { get; set; }

        /// <summary>
        /// "favorite" rank for this race
        /// </summary>
        [JsonPropertyName("popularity")]
        public int Popularity { get; set; }

        /// <summary>
        /// Array of the 3 favorite rank marks (circle, triangle, etc) shown before the race.
        /// </summary>
        [JsonPropertyName("popularity_mark_rank_array")]
        public List<int> PopularityMarkRankArray { get; set; } = new();

        /// <summary>
        /// Used to group umas into teams for team-type races like TT and CM.
        /// Player's umas seem to always be on team 1, while opponents are on team 2.  NPCs are on team 0.
        /// </summary>
        [JsonPropertyName("team_id")]
        public int TeamId { get; set; }

        /// <summary>
        /// If on a team, this is the position within the team.  0 for no team (or NPCs).
        /// In races with an "Ace" team position, the ace will be #1.
        /// </summary>
        [JsonPropertyName("team_member_id")]
        public int TeamMemberId { get; set; }

        /// <summary>
        /// Unknown.  (possibly used in unity?)
        /// </summary>
        [JsonPropertyName("team_rank")]
        public int TeamRank { get; set; }

        /// <summary>
        /// Career wins for the uma
        /// </summary>
        [JsonPropertyName("single_mode_win_count")]
        public int SingleModeWinCount { get; set; }

        /// <summary>
        /// (possibly) marks if mood has been changed by an item (like a clock or snack).
        /// </summary>
        [JsonPropertyName("motivation_change")]
        public int MotivationChange { get; set; }

        /// <summary>
        /// (possibly) marks if gate order has been changed by an item.
        /// </summary>
        [JsonPropertyName("frame_order_change_flag")]
        public int FrameOrderChangeFlag { get; set; }
    }
}
