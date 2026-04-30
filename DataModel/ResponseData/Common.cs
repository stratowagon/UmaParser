using System.Text.Json.Serialization;

namespace UmaBlobber.DataModel.ResponseData
{

    /// <summary>
    /// Fallback class for unidentified API responses.
    /// </summary>
    public class FallbackUmaApiResponse : UmaApiResponse
    {
        // Uses the base 'object?' Data; no further specialization needed.
    }

    public class RaceData
    {
        [JsonPropertyName("use_item_id_array")]
        public List<int> UseItemIdArray { get; set; } = new();

        [JsonPropertyName("race_start_params_array")]
        public List<RaceStartParams> RaceStartParamsArray { get; set; } = new();
    }

    public class RaceStartInfo
    {
        [JsonPropertyName("program_id")]
        public int ProgramId { get; set; }

        [JsonPropertyName("random_seed")]
        public int RandomSeed { get; set; }

        [JsonPropertyName("weather")]
        public int Season { get; set; }

        [JsonPropertyName("ground_condition")]
        public int GroundCondition { get; set; }

        [JsonPropertyName("race_horse_data")]
        public RaceHorseData RaceHorseData { get; set; } = new();

        [JsonPropertyName("continue_num")]
        public int ContinueNum { get; set; }
    }

    public class Skill
    {
        [JsonPropertyName("skill_id")]
        public int SkillId { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }
    }
}
