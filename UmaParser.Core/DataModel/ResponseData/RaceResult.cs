using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UmaParser.DataModel.ResponseData
{
    /// <summary>
    /// Data structure for results of one race in a team trials match.
    /// </summary>
    public class RaceResult
    {
        /// <summary>
        /// Enum code for distance (1 = sprint, 2 = mile, 3 = medium, 4 = long, 5 = dirt)
        /// </summary>
        [JsonPropertyName("distance_type")]
        public int DistanceType { get; set; }

        /// <summary>
        /// Base64-encoded, zipped, binary blob for race simulation data.
        /// </summary>
        [JsonPropertyName("race_scenario")]
        public string RaceScenario { get; set; } = string.Empty; // base64

        /// <summary>
        /// Chronological round order.
        /// </summary>
        [JsonPropertyName("round")]
        public int Round { get; set; }

        /// <summary>
        /// Total score for the player's team in this round.
        /// </summary>
        [JsonPropertyName("team_total_score")]
        public int TeamTotalScore { get; set; }

        /// <summary>
        /// Team score details (only teamwide, not per-character).
        /// Only present if the team has some kind of bonuses beyond individual scores.
        /// </summary>
        [JsonPropertyName("team_score_array")]
        public List<TeamTrialsScore> TeamScoreArray { get; set; } = new();

        /// <summary>
        /// (possibly) Enum code for winning? 1 seems to be a win, 2 seems to be a loss.  Draw might be possible?
        /// </summary>
        [JsonPropertyName("win_type")]
        public int WinType { get; set; }

        /// <summary>
        /// Consecutive wins (including this one). Resets to 0 on a loss.
        /// </summary>
        [JsonPropertyName("current_consecutive_win_count")]
        public int CurrentConsecutiveWinCount { get; set; }

        /// <summary>
        /// (probably) the bonus applied to the NEXT round IF it is win.  Value seems to be 100x (so 200 = 2%).
        /// Value is (current_consecutive_win_count + 1) * 100 if this round was a win, otherwise 0.
        /// </summary>
        [JsonPropertyName("bonus_rate_by_next_win")]
        public int BonusRateByNextWin { get; set; }

        /// <summary>
        /// Array of individual team member results.  Includes all umas, but only player's umas actually have scores.
        /// </summary>
        [JsonPropertyName("chara_result_array")]
        public List<CharaResult> CharaResultArray { get; set; } = new();
    }
}
