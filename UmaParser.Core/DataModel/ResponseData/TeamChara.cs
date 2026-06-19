using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UmaParser.DataModel.ResponseData
{
    /// <summary>
    /// Data model for a trained character.  Primarily used for player's veteran umas, but can appear in other contexts,
    /// including those with other players' trained umas.
    /// </summary>
    public class TrainedChara
    {
        /// <summary>
        /// Seems to always be the account ID of the player.
        /// </summary>
        [JsonPropertyName("viewer_id")]
        public long ViewerId { get; set; }

        /// <summary>
        /// Unique identifier for trained umas for this particular owner.  Not globally fixed, but seems to be consistent for
        /// a given veteran uma for a given trainer ID.  Other data objects often refer to this ID instead of an absolute
        /// fixed character ID.  Some responses (like Team Trials) only include this ID and do not include a trained_chara_array,
        /// so they must be cross-referenced with other responses (the veteran uma list is the best source).
        /// Seems to be incremental, but not directly corrolated with the career run number.
        /// </summary>
        [JsonPropertyName("trained_chara_id")]
        public int TrainedCharaId { get; set; }

        /// <summary>
        /// In contexts that include other players' umas, this is the account ID of the owner.  Otherwise this is always 0.
        /// </summary>
        [JsonPropertyName("owner_viewer_id")]
        public long OwnerViewerId { get; set; }

        /// <summary>
        /// In contexts where other players' umas are used, this will be their trained_chara_id.  Otherwise this is always 0.
        /// </summary>
        [JsonPropertyName("owned_trained_chara_id")]
        public int OwnedTrainedCharaId { get; set; }

        /// <summary>
        /// Possibly the incremental ID of the career run that made this uma, starting at 1(?).
        /// Not verified yet, but it closely correlates with the number of career runs I have played.
        /// </summary>
        [JsonPropertyName("single_mode_chara_id")]
        public int SingleModeCharaId { get; set; }

        /// <summary>
        /// (speculation) possibly the random seed used for this uma's career events.
        /// </summary>
        [JsonPropertyName("chara_seed")]
        public int CharaSeed { get; set; }

        /// <summary>
        /// 6-digit ID most likely for the character's portrait icon.  It matches the format of a 4-digit base ID
        /// plus a 2-digit variation.
        /// </summary>
        [JsonPropertyName("card_id)")]
        public int CardId { get; set; }

        /// <summary>
        /// trained_chara_id of the veteran uma in parent slot 1.  This ID may not exist in the list if it was transferred
        /// or was a guest parent.
        /// </summary>
        [JsonPropertyName("succession_trained_chara_id_1")]
        public int SuccessionTrainedCharaId1 { get; set; }

        /// <summary>
        /// trained_chara_id of the veteran uma in parent slot 2.  This ID may not exist in the list if it was transferred
        /// or was a guest parent.
        /// </summary>
        [JsonPropertyName("succession_trained_chara_id_2")]
        public int SuccessionTrainedCharaId2 { get; set; }

        /// <summary>
        /// unknown
        /// </summary>
        [JsonPropertyName("use_type")]
        public int UseType { get; set; }

        /// <summary>
        /// Speed stat
        /// </summary>
        [JsonPropertyName("speed")]
        public int Speed { get; set; }

        /// <summary>
        /// Stamina stat
        /// </summary>
        [JsonPropertyName("stamina")]
        public int Stamina { get; set; }

        /// <summary>
        /// Power stat
        /// </summary>
        [JsonPropertyName("power")]
        public int Power { get; set; }

        /// <summary>
        /// Wits stat
        /// </summary>
        [JsonPropertyName("wiz")]
        public int Wiz { get; set; }

        /// <summary>
        /// Guts stat
        /// </summary>
        [JsonPropertyName("guts")]
        public int Guts { get; set; }

        /// <summary>
        /// Final fan count from the career.
        /// </summary>
        [JsonPropertyName("fans")]
        public int Fans { get; set; }

        /// <summary>
        /// Uma's rank score, visible under the portrait in many UI scenes.
        /// </summary>
        [JsonPropertyName("rank_score")]
        public int RankScore { get; set; }

        /// <summary>
        /// (probably) the enumerated rank tier of the uma (S, S+, SS, UG and so on)
        /// </summary>
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        /// <summary>
        /// Enum id for the career scenario.  1 = URA, 2 = Unity, 4 = Trackblazer
        /// </summary>
        [JsonPropertyName("scenario_id")]
        public int ScenarioId { get; set; }

        /// <summary>
        /// Turf affinity enum (G to S)
        /// </summary>
        [JsonPropertyName("proper_ground_turf")]
        public int ProperGroundTurf { get; set; }

        /// <summary>
        /// Dirt affinity enum (G to S)
        /// </summary>
        [JsonPropertyName("proper_ground_dirt")]
        public int ProperGroundDirt { get; set; }

        /// <summary>
        /// Sprint affinity enum (G to S)
        /// </summary>
        [JsonPropertyName("proper_distance_short")]
        public int ProperDistanceShort { get; set; }

        /// <summary>
        /// Mile affinity enum (G to S)
        /// </summary>
        [JsonPropertyName("proper_distance_mile")]
        public int ProperDistanceMile { get; set; }

        /// <summary>
        /// Medium affinity enum (G to S)
        /// </summary>
        [JsonPropertyName("proper_distance_middle")]
        public int ProperDistanceMiddle { get; set; }

        /// <summary>
        /// Long affinity enum (G to S)
        /// </summary>
        [JsonPropertyName("proper_distance_long")]
        public int ProperDistanceLong { get; set; }

        /// <summary>
        /// Front Runner affinity enum (G to S)
        /// a.k.a. nige, escaper
        /// </summary>
        [JsonPropertyName("proper_running_style_nige")]
        public int ProperRunningStyleNige { get; set; }

        /// <summary>
        /// Pace Chaser affinity enum (G to S)
        /// a.k.a. senko, leader
        /// </summary>
        [JsonPropertyName("proper_running_style_senko")]
        public int ProperRunningStyleSenko { get; set; }

        /// <summary>
        /// Late Surger affinity enum (G to S)
        /// a.k.a. sashi, betweener
        /// </summary>
        [JsonPropertyName("proper_running_style_sashi")]
        public int ProperRunningStyleSashi { get; set; }

        /// <summary>
        /// End Closer affinity enum (G to S)
        /// a.k.a. oikomi, closer
        /// </summary>
        [JsonPropertyName("proper_running_style_oikomi")]
        public int ProperRunningStyleOikomi { get; set; }

        /// <summary>
        /// (probably) total number of "inspirations" using this uma, including as a borrowed parent.
        /// </summary>
        [JsonPropertyName("succession_num")]
        public int SuccessionNum { get; set; }

        /// <summary>
        /// (probably) star count of the uma.
        /// </summary>
        [JsonPropertyName("rarity")]
        public int Rarity { get; set; }

        /// <summary>
        /// (probably) the level of the uma's unique talent.
        /// </summary>
        [JsonPropertyName("talent_level")]
        public int TalentLevel { get; set; }

        /// <summary>
        /// 6-digit Outfit ID for the 3D model.  Not the same as card ID.
        /// </summary>
        [JsonPropertyName("race_cloth_id")]
        public int RaceClothId { get; set; }

        /// <summary>
        /// (speculation) possibly the fan-count based tier of the uma.
        /// </summary>
        [JsonPropertyName("chara_grade")]
        public int CharaGrade { get; set; }

        /// <summary>
        /// Running style enum (1 to 4) that this uma defaults to.  Seems to be whichever style was being used at the end of the career,
        /// which might not be their best style.
        /// </summary>
        [JsonPropertyName("running_style")]
        public int RunningStyle { get; set; }

        /// <summary>
        /// (probably) the ID of the nickname currently being used (user can change).
        /// </summary>
        [JsonPropertyName("nickname_id")]
        public int NicknameId { get; set; }

        /// <summary>
        /// Win count in career mode.
        /// </summary>
        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        /// <summary>
        /// Real-world time stamp of when the uma was created.  Seems to be the same as create_time.
        /// </summary>
        [JsonPropertyName("register_time")]
        public string RegisterTime { get; set; } = string.Empty;

        /// <summary>
        /// Real-world time stamp of when the uma was created.  Seems to be the same as register_time.
        /// </summary>
        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; } = string.Empty;

        /// <summary>
        /// Array of skills this uma has, which are just a skill_id and a level.
        /// </summary>
        [JsonPropertyName("skill_array")]
        public List<Skill> SkillArray { get; set; } = new();

        /// <summary>
        /// Array of support cards used for this uma's career.
        /// </summary>
        //[JsonPropertyName("support_card_list")]
        //public List<SupportCard> SupportCardList { get; set; } = new();

        /// <summary>
        /// Detailed list of the races and results from this uma's career.
        /// </summary>
        //[JsonPropertyName("race_result_list")]
        //public List<RaceResult> RaceResultList { get; set; } = new();

        /// <summary>
        /// unknown
        /// </summary>
        [JsonPropertyName("win_saddle_id_array")]
        public List<int> WinSaddleIdArray { get; set; } = new();

        /// <summary>
        /// (probably) list of all nicknames unlocked for this uma.  User can pick from these to use as the active nickname.
        /// </summary>
        [JsonPropertyName("nickname_id_array")]
        public List<int> NicknameIdArray { get; set; } = new();

        /// <summary>
        /// List of sparks this uma provides as a parent.  Last two digits is spark level (01 to 03).
        /// Skill names are in text_data category 147.
        /// </summary>
        [JsonPropertyName("factor_id_array")]
        public List<int> FactorIdArray { get; set; } = new();

        /// <summary>
        /// Array of "detailed" information on each factor.  In reality, appears to just be the same factor ID along with
        /// a "level" but the level is always 0.
        /// </summary>
        //[JsonPropertyName("factor_info_array")]
        //public List<FactorInfo> FactorInfoArray { get; set; } = new();

        /// <summary>
        /// List of the parents and grandparents (always 6)
        /// </summary>
        [JsonPropertyName("succession_chara_array")]
        public List<TrainedChara> SuccessionCharaArray { get; set; } = new();

        /// <summary>
        /// (probably) list of umas inspired by this uma (including guests).  Limited to last 100.
        /// </summary>
        //[JsonPropertyName("succession_history_array")]
        //public List<SuccessionHistory> SuccessionHistoryArray { get; set; } = new();
    }

}
