using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using UmaBlobber.Analysis;
using UmaBlobber.DataModel.RaceScenario;
using UmaBlobber.DataModel.ResponseData;
using UmaBlobber.MasterData;

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
#if DEBUG
                // Uncomment for exploring race data (very verbose)
                //Debug.Print(raceData.GetSummary());
#endif
            }
        }

        //*************************************************
        // Data helpers and convenience properties
        //*************************************************

        /// <summary>
        /// Decoded simulation data for each race.
        /// </summary>
        public List<RaceScenarioData> SimulationData { get; set; } = new();

        private Dictionary<int, RaceHorseData>? _raceRoster;
        private Dictionary<int, CharaResult>? _results;

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
                if (_raceRoster == null && Data != null)
                {
                    _raceRoster = new();
                    var umas = Data.RaceStartParamsArray
                        .SelectMany(raceStart => raceStart.RaceHorseDataArray
                            .Where(uma => uma.TeamId == 1))
                        .OrderBy(uma => FindRaceResultByUma(uma)?.DistanceType)
                        .ThenBy(uma => uma.TeamMemberId)
                        .ToList();
                    foreach (var uma in umas)
                    {
                        _raceRoster.Add(uma.TrainedCharaId, uma);
                    }
                }
                return _raceRoster ?? new();
            }
        }

        /// <summary>
        /// Names of the umas in RaceRoster order.
        /// </summary>
        public List<string> RosterNames =>
            RaceRoster.Select(uma => GameMasterService.Current.Catalog.FormatCharaShortName(uma.Value.CharaId)).ToList();

        /// <summary>
        /// Races in this trial where the given roster uma ran (team 1), with parsed scenario data.
        /// </summary>
        public IEnumerable<RaceAppearance> GetAppearances(int trainedCharaId)
        {
            if (Data == null)
            {
                yield break;
            }

            for (int i = 0; i < Data.RaceResultArray.Count; i++)
            {
                if (i >= SimulationData.Count)
                {
                    break;
                }

                var result = Data.RaceResultArray[i];
                var start = Data.RaceStartParamsArray.FirstOrDefault(s => s.Round == result.Round);
                if (start == null)
                {
                    continue;
                }

                var horse = start.RaceHorseDataArray.FirstOrDefault(
                    h => h.TeamId == 1 && h.TrainedCharaId == trainedCharaId);
                if (horse == null)
                {
                    continue;
                }

                yield return new RaceAppearance(result, SimulationData[i], horse);
            }
        }

        /// <summary>
        /// Dictionary of chara_result objects filtered by player's umas.
        /// Key is trained_chara_id, value is the CharaResult object.
        /// </summary>
        public Dictionary<int, CharaResult> Results
        {
            get
            {
                if (_results == null && Data != null)
                {
                    _results = new();
                    var umas = Data.RaceResultArray
                        .SelectMany(raceResult => raceResult.CharaResultArray)
                        .Where(uma => uma.TeamId == 1)
                        .ToList();
                    foreach(var uma in umas)
                    {
                        _results.Add(uma.TrainedCharaId, uma);
                    }
                }
                return _results ?? new();
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

                TeamTrialsScore? biggest = scores.MaxBy(score => score.BonusArray.FirstOrDefault(bonus => bonus.ScoreBonusId == ScoreBonusType.OpponentRating)?.BonusScore ?? 0);
                if (biggest != null)
                {
                    ScoreBonus? bonusScore = biggest.BonusArray.FirstOrDefault(bonus => bonus.ScoreBonusId == ScoreBonusType.OpponentRating);
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

        public int UmaBaseScore(int trainedCharaId)
        {
            if (Data == null) return 0;
            return Results[trainedCharaId].ScoreArray.Sum(score => score.BaseScore);
        }

        public List<int> RosterBaseScores
        {
            get
            {
                if (rosterBaseScores == null && Data != null)
                {
                    rosterBaseScores = new();
                    foreach (var uma in RaceRoster)
                    {
                        rosterBaseScores.Add(UmaBaseScore(uma.Value.TrainedCharaId));
                    }
                }
                return rosterBaseScores ?? new();
            }
        }

        public List<int> RosterScores
        {
            get
            {
                if (rosterScores == null && Data != null)
                {
                    rosterScores = new();
                    foreach (var uma in RaceRoster)
                    {
                        rosterScores.Add(UmaScore(uma.Value.TrainedCharaId));
                    }
                }
                return rosterScores ?? new();
            }
        }

        private List<int>? rosterScores;
        private List<int>? rosterBaseScores;
    }
}
