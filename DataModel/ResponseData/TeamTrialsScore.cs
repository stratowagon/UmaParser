namespace UmaBlobber.DataModel.ResponseData
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;

    /// <summary>
    /// Data structure for both team and individual score buckets in team trials.
    /// </summary>
    public class TeamTrialsScore
    {
        /// <summary>
        /// Internal ID for scoring type (text_data category 140)
        /// </summary>
        [JsonPropertyName("raw_score_id")]
        public int RawScoreId { get; set; }

        /// <summary>
        /// Unknown
        /// </summary>
        [JsonPropertyName("num")]
        public int Num { get; set; }

        /// <summary>
        /// Total displayed score for this score bucket.
        /// Bonuses are already included.
        /// </summary>
        [JsonPropertyName("score")]
        public int Score { get; set; }

        /// <summary>
        /// Unknown
        /// </summary>
        [JsonPropertyName("bonus_num")]
        public int BonusNum { get; set; }

        /// <summary>
        /// Details of team bonus modifiers (opponent bonus, support bonus, streak bonus)
        /// </summary>
        [JsonPropertyName("bonus_array")]
        public List<ScoreBonus> BonusArray { get; set; } = new();

        /// <summary>
        /// Calculated base score by subtracting all bonuses from Score.
        /// </summary>
        public int BaseScore
        {
            get
            {
                int totalBonus = 0;
                foreach (var bonus in BonusArray)
                {
                    totalBonus += bonus.BonusScore;
                }
                return Score - totalBonus;
            }
        }
    }
}
