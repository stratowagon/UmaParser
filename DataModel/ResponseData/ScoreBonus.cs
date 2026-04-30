namespace UmaBlobber.DataModel.ResponseData
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Struct for team-based bonus details.
    /// </summary>
    public class ScoreBonus
    {
        /// <summary>
        /// Type of bonus modifier (text_data category 148)
        /// </summary>
        [JsonPropertyName("score_bonus_id")]
        public int ScoreBonusId { get; set; }

        /// <summary>
        /// Points for this modifier (calculated from base)
        /// </summary>
        [JsonPropertyName("bonus_score")]
        public int BonusScore { get; set; }

        /// <summary>
        /// unknown
        /// </summary>
        [JsonPropertyName("condition_type")]
        public int ConditionType { get; set; }

        /// <summary>
        /// unknown
        /// </summary>
        [JsonPropertyName("condition_value_1")]
        public int ConditionValue1 { get; set; }

        /// <summary>
        /// unknown
        /// </summary>
        [JsonPropertyName("condition_value_2")]
        public int ConditionValue2 { get; set; }

        /// <summary>
        /// unknown
        /// </summary>
        [JsonPropertyName("score_rate")]
        public int ScoreRate { get; set; }
    }
}
