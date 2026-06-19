namespace UmaParser.DataModel.ResponseData
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;

    /// <summary>
    /// Represents results in a chara_result_array from team trials
    /// </summary>
    public class CharaResult
    {
        /// <summary>
        /// Gate number assigned to this uma
        /// </summary>
        [JsonPropertyName("frame_order")]
        public int FrameOrder { get; set; }

        /// <summary>
        /// Account ID of the owning player.  0 for NPCs.
        /// This is stripped in HorseACT captures.
        /// </summary>
        [JsonPropertyName("viewer_id")]
        public long ViewerId { get; set; }

        /// <summary>
        /// Team ID for the uma.  Player's umas are 1, opponent's are 2, NPCs are 0.
        /// </summary>
        [JsonPropertyName("team_id")]
        public int TeamId { get; set; } = 0;

        /// <summary>
        /// Matches local ID for veteran umas, if 
        /// </summary>
        [JsonPropertyName("trained_chara_id")]
        public int TrainedCharaId { get; set; }

        /// <summary>
        /// Actual place order
        /// </summary>
        [JsonPropertyName("finish_order")]
        public int? FinishOrder { get; set; }

        /// <summary>
        /// Elapsed race time, in 1/10000ths of a second.
        /// </summary>
        [JsonPropertyName("finish_time")]
        public long? FinishTime { get; set; }

        /// <summary>
        /// Array of all scoring buckets, with details, contributing to this uma's score.
        /// Only populated for player's umas.
        /// </summary>
        [JsonPropertyName("score_array")]
        public List<TeamTrialsScore> ScoreArray { get; set; } = new();
    }
}
