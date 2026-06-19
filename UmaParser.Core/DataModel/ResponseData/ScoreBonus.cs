namespace UmaParser.DataModel.ResponseData
{
    using System.Text.Json.Serialization;

    public enum ScoreBonusType
    {
        None = 0,           // unusued
        Ace = 1,            // ace bonus
        OpponentRating = 2, // opponent rating bonus
        Unused3 = 3,        // unused bonus type 3
        Streak2 = 4,        // 2 win streak bonus
        Streak3 = 5,        // 3 win streak bonus
        Streak4 = 6,        // 4 win streak bonus
        Streak5 = 7,        // 5 win streak bonus
        Support = 8,        // support card bonus
    }

    /// <summary>
    /// Struct for team-based bonus details.
    /// </summary>
    public class ScoreBonus
    {
        /// <summary>
        /// Type of bonus modifier (text_data category 148)
        /// </summary>
        [JsonPropertyName("score_bonus_id")]
        public ScoreBonusType ScoreBonusId { get; set; }

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
