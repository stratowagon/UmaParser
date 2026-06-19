namespace UmaParser.MasterData;

/// <summary>
/// Master metadata for a <c>team_stadium_raw_score</c> id: base value plus text_data 140/141 labels.
/// Distinct from <see cref="DataModel.ResponseData.TeamTrialsScore"/>, which is a score bucket in capture JSON.
/// </summary>
internal readonly record struct TeamTrialsScoreEntry(int Id, string Name, string Description, int BaseScore)
{
    public static Dictionary<int, TeamTrialsScoreEntry> Merge(
        IReadOnlyDictionary<int, string> names,
        IReadOnlyDictionary<int, string> descriptions,
        IReadOnlyDictionary<int, int> baseScores)
    {
        var result = new Dictionary<int, TeamTrialsScoreEntry>(baseScores.Count);
        foreach (var (id, baseScore) in baseScores)
        {
            result[id] = new TeamTrialsScoreEntry(
                id,
                names.GetValueOrDefault(id, string.Empty),
                descriptions.GetValueOrDefault(id, string.Empty),
                baseScore);
        }

        return result;
    }
}