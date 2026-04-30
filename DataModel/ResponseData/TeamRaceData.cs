using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UmaBlobber.DataModel.ResponseData
{
    // Team Race (TT)
    public class TeamRaceData
    {
        [JsonPropertyName("use_item_id_array")] public List<int> UseItemIdArray { get; set; } = new();
        [JsonPropertyName("race_start_params_array")] public List<RaceStartParams> RaceStartParamsArray { get; set; } = new();
        [JsonPropertyName("race_result_array")] public List<RaceResult> RaceResultArray { get; set;} = new();
        // rp_info
        // item_info_array
        // is_include_usupported_race
        // winning_reward_info_array
        // winning_reward_guarantee_status
        // last_checked_round
        [JsonPropertyName("support_card_bonus")] public int SupportCardBonus { get; set; }
        // user_team_data_array_copy
        // user_trained_chara_array_copy
        // opponent_info_copy
        // opponent_chara_info_array_latest_copy
    }

    // Career Race
    public class CareerRaceData
    {
        [JsonPropertyName("race_start_info")] public RaceStartInfo RaceStartInfo { get; set; } = new();
    }

    // Practice Race Result
    public class PracticeRaceData
    {
        [JsonPropertyName("trained_chara_array")] public List<TrainedChara> TrainedCharaArray { get; set; } = new();
        [JsonPropertyName("practice_race_id")] public int PracticeRaceId { get; set; }
        [JsonPropertyName("state")] public int State { get; set; }
        // ... practice_partner_owner_info_array etc.
    }

    // Room Match Result
    public class RoomMatchData
    {
        [JsonPropertyName("race_scenario")] public string RaceScenario { get; set; } = string.Empty; // base64
        [JsonPropertyName("trained_chara_array")] public List<TrainedChara> TrainedCharaArray { get; set; } = new();
        [JsonPropertyName("random_seed")] public long RandomSeed { get; set; }
        [JsonPropertyName("season")] public int Season { get; set; }
        [JsonPropertyName("weather")] public int Weather { get; set; }
        [JsonPropertyName("ground_condition")] public int GroundCondition { get; set; }
        [JsonPropertyName("start_time_type")] public int StartTimeType { get; set; }
    }
}
