using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using UmaBlobber.Data;
using UmaBlobber.DataModel.RaceScenario;
using UmaBlobber.DataModel.ResponseData;

namespace UmaBlobber.ObjectModel
{
    /// <summary>
    /// Complete wrapper for a Team Trials result api response.
    /// </summary>
    public class TeamTrialResult : UmaApiResponse
    {
        //*************************************************
        // UmaApiResponse requirements
        //*************************************************

        public new TeamRaceData? Data { get; set; }

        public static Type? DetermineType(JsonElement dataElement)
        {
            if (dataElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (dataElement.TryGetProperty("race_result_array", out _))
            {
                return typeof(TeamTrialResult);
            }
            return null;
        }

        public TeamTrialResult(TeamRaceData data)
        {
            Data = data;
            foreach (var race in Data.RaceResultArray)
            {
                var raceData = RaceScenarioParser.Parse(race.RaceScenario);
                SimulationData.Add(raceData);
                Debug.Print(raceData.GetSummary());
            }
        }

        //*************************************************
        // Data helpers and convenience properties
        //*************************************************

        /// <summary>
        /// Decoded simulation data for each race.
        /// </summary>
        public List<RaceScenarioData> SimulationData { get; set; } = new();

        /// <summary>
        /// Searches for the race result that corresponts to a given uma.
        /// </summary>
        public RaceResult? FindRaceResultByUma(RaceHorseData uma)
        {
            if (Data == null) return null;
            RaceResult? race = Data?.RaceResultArray.FirstOrDefault(r => r.CharaResultArray.Any(c => c.TeamId == uma.TeamId && c.TrainedCharaId == uma.TrainedCharaId));
            return race;
        }

        /// <summary>
        /// Dictionary of player's umas sorted into team and member order.
        /// Key is trained_chara_id, value is the RaceHorseData object.
        /// Sorted by distance type (sprint, mile, med, long, dirt) and then team member id.
        /// </summary>
        public Dictionary<int, RaceHorseData> RaceRoster
        {
            get
            {
                if (field == null && Data != null)
                {
                    field = new();
                    var umas = Data.RaceStartParamsArray
                        .SelectMany(raceStart => raceStart.RaceHorseDataArray
                            .Where(uma => uma.TeamId == 1))
                        .OrderBy(uma => FindRaceResultByUma(uma).DistanceType)
                        .ThenBy(uma => uma.TeamMemberId)
                        .ToList();
                    foreach (var uma in umas)
                    {
                        field.Add(uma.TrainedCharaId, uma);
                    }
                }
                return field ?? new();
            }
        }

        /// <summary>
        /// Names of the umas in RaceRoster order.
        /// </summary>
        public List<string> RosterNames { get { return RaceRoster.Select(uma => DBData.ShortNames[uma.Value.CharaId]).ToList(); } }

        /// <summary>
        /// Dictionary of chara_result objects filtered by player's umas.
        /// Key is trained_chara_id, value is the CharaResult object.
        /// </summary>
        public Dictionary<int, CharaResult> Results
        {
            get
            {
                if (field == null && Data != null)
                {
                    field = new();
                    var umas = Data.RaceResultArray
                        .SelectMany(raceResult => raceResult.CharaResultArray)
                        .Where(uma => uma.TeamId == 1)
                        .ToList();
                    foreach(var uma in umas)
                    {
                        field.Add(uma.TrainedCharaId, uma);
                    }
                }
                return field ?? new();
            }
        }

        /// <summary>
        /// Opponent bonus as a multiplier.  It must be derived from looking at bonus scores, so it could be slightly innacurate.
        /// </summary>
        public float OpponentMultiplier
        {
            get
            {
                if (Data == null)
                {
                    return 0;
                }

                // Some online sources say opponent bonus formula is OppRating / (YourRating + (200000 - OppRating))
                // This doesn't seem accurate.

                // We have to guess at the opponent bonus.  For greatest accuracy, we will take the largest opponent bonus we can find (ScoreBonusId 2)
                // and divide it by the base score of the same score bucket to get the multiplier.
                List<TeamTrialsScore> scores = Data.RaceResultArray.SelectMany(result => result.TeamScoreArray).ToList();
                scores.AddRange(Data.RaceResultArray.SelectMany(result => result.CharaResultArray.SelectMany(chara => chara.ScoreArray)));

                TeamTrialsScore? biggest = scores.MaxBy(score => score.BonusArray.FirstOrDefault(bonus => bonus.ScoreBonusId == 2)?.BonusScore ?? 0);
                if (biggest != null)
                {
                    ScoreBonus? bonusScore = biggest.BonusArray.FirstOrDefault(bonus => bonus.ScoreBonusId == 2);
                    if (bonusScore != null)
                    {
                        return (float)bonusScore.BonusScore / (float)biggest.BaseScore;
                    }
                }
                return 0;
            }
        }

        /// <summary>
        /// Direct support card bonus.  This is 10000 times the decimal multiplier (ie 1200 == 0.12 or 12%)
        /// </summary>
        public int SupportCardBonus { get { return Data?.SupportCardBonus ?? 0; } }

        /// <summary>
        /// Support card bonus in decimal multiplier form.  This is the value that should be used for calculations.
        /// </summary>
        public float SupportCardMultiplier { get { return SupportCardBonus / 10000f; } }

        /// <summary>
        /// Calculated match bonus.  Only applies if 3 or more of the races were wins.  Opponent bonus is applied, but not support card bonus.
        /// </summary>
        public int MatchBonus
        {
            get
            {
                int matchBonus = 0;
                bool won = Data?.RaceResultArray.Count(race => race.WinType == 1) >= 3;
                if (won)
                {
                    matchBonus = 10000 + (int)(10000f * OpponentMultiplier);
                }
                return matchBonus;
            }
        }

        /// <summary>
        /// Calculated total points for the match.  Sum of all race scores plus calculated match bonus.
        /// </summary>
        public int TotalScore
        {
            get
            {
                int raceScores = Data?.RaceResultArray.Sum(race => race.TeamTotalScore) ?? 0;
                raceScores += MatchBonus;
                return raceScores;
            }
        }

        public int UmaScore(RaceHorseData uma)
        {
            return UmaScore(uma.TrainedCharaId);
        }

        public int UmaScore(int trainedCharaId)
        {
            if (Data == null) return 0;
            return Results[trainedCharaId].ScoreArray.Sum(score => score.Score);
        }

        public List<int> RosterScores
        {
            get
            {
                if (field == null && Data != null)
                {
                    field = new();
                    foreach (var uma in RaceRoster)
                    {
                        field.Add(Results[uma.Value.TrainedCharaId].ScoreArray.Sum(score => score.Score));
                    }
                }
                return field ?? new();
            }
        }
    }
}
