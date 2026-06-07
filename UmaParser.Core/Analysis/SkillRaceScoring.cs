using UmaBlobber.DataModel.RaceScenario;

namespace UmaBlobber.Analysis;

/// <summary>
/// Team Trials skill points recorded in <see cref="SimulateEventType.Score"/> events.
/// Params: [horseIndex0, team_stadium_raw_score.id, points].
/// </summary>
internal static class SkillRaceScoring
{
    /// <summary><c>team_stadium_raw_score</c> ids for white, gold, and unique skill activations (condition_type 8).</summary>
    private static readonly HashSet<int> SkillActivationRawScoreIds = BuildSkillActivationRawScoreIds();

    public static IEnumerable<(int SkillId, int Points)> ScoreActivations(RaceAppearance appearance)
    {
        int horseIndex = appearance.Horse.FrameOrder - 1;
        if (horseIndex < 0)
        {
            yield break;
        }

        var scoresByTime = appearance.Simulation.Events
            .Where(e => e.Type == SimulateEventType.Score
                && e.Params.Count >= 3
                && e.Params[0] == horseIndex
                && SkillActivationRawScoreIds.Contains(e.Params[1]))
            .GroupBy(e => e.FrameTime)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Params[2]));

        foreach (var ev in appearance.Simulation.Events)
        {
            if (ev.Type != SimulateEventType.Skill
                || ev.Params.Count < 2
                || ev.Params[0] != horseIndex)
            {
                continue;
            }

            if (!scoresByTime.TryGetValue(ev.FrameTime, out int points))
            {
                continue;
            }

            yield return (ev.Params[1], points);
        }
    }

    private static HashSet<int> BuildSkillActivationRawScoreIds()
    {
        var ids = new HashSet<int>();
        for (int id = 26; id <= 57; id++)
        {
            ids.Add(id);
        }

        return ids;
    }
}