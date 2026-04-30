namespace UmaBlobber.DataModel.ResponseData
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;

    /// <summary>
    /// Data structure for each race in a TT match.
    /// </summary>
    public class RaceStartParams
    {
        /// <summary>
        /// Chronological round order.
        /// </summary>
        [JsonPropertyName("round")]
        public int Round { get; set; }

        [JsonPropertyName("race_instance_id")]
        public int RaceInstanceId { get; set; }

        /// <summary>
        /// Season code (don't know where these are defined yet, I've seen 5)
        /// </summary>
        [JsonPropertyName("season")]
        public int Season { get; set; }

        /// <summary>
        /// Weather code
        /// </summary>
        [JsonPropertyName("weather")]
        public int Weather { get; set; }

        /// <summary>
        /// Ground condition code
        /// </summary>
        [JsonPropertyName("ground_condition")]
        public int GroundCondition { get; set; }

        /// <summary>
        /// Race seed
        /// </summary>
        [JsonPropertyName("random_seed")]
        public long RandomSeed { get; set; }

        /// <summary>
        /// Umas participating in the race
        /// </summary>
        [JsonPropertyName("race_horse_data_array")]
        public List<RaceHorseData> RaceHorseDataArray { get; set; } = new();

        /// <summary>
        /// Team evaluation score for your team
        /// </summary>
        [JsonPropertyName("self_evaluate")]
        public int SelfEvaluate { get; set; }

        /// <summary>
        /// Team evaluation score for the opponent team.
        /// </summary>
        [JsonPropertyName("opponent_evaluate")]
        public int OpponentEvaluate { get; set; }
    }
}
