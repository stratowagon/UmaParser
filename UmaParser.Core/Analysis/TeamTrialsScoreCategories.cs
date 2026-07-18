namespace UmaParser.Analysis;

/// <summary>
/// Groups team_stadium_raw_score ids for exploratory score breakdowns on Team Analysis.
/// </summary>
public static class TeamTrialsScoreCategories
{
    public const int SkillActivationRawScoreIdMin = 26;
    public const int SkillActivationRawScoreIdMax = 57;

    public static bool IsSkillActivation(int rawScoreId) =>
        rawScoreId >= SkillActivationRawScoreIdMin && rawScoreId <= SkillActivationRawScoreIdMax;
}