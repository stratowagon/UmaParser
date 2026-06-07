using System.Collections.Generic;
using UmaBlobber.DataModel.RaceScenario;
using UmaBlobber.MasterData;

namespace UmaBlobber.Analysis;

/// <summary>
/// Team Trials skill points recorded in <see cref="SimulateEventType.Score"/> events.
/// Params: [horseIndex0, team_stadium_raw_score.id, points].
///
/// The authoritative list of raw_score ids and their base amounts (500 white / 1200 gold /
/// variable for uniques) lives in the team_stadium_raw_score table (id + score columns).
/// Names via text_data category 140, descriptions via 141. Loaded into GameMasterCatalog
/// (with embedded fallback support) so the filter and base values are data-driven instead of
/// a magic 26-57 range.
/// </summary>
internal static class SkillRaceScoring
{
    // Legacy fallback range used only when no master data (or embedded fallback) has populated team_stadium_raw_score entries.
    private static readonly HashSet<int> LegacySkillActivationRawScoreIds = BuildLegacySkillActivationRawScoreIds();

    /// <summary>
    /// Yields (skillId, points) pairs by matching individual <see cref="SimulateEventType.Score"/> events
    /// (raw_score_ids 26-57, i.e. condition_type 8 white/gold/unique activations) to <see cref="SimulateEventType.Skill"/>
    /// events for the same horse at the exact same FrameTime.
    ///
    /// Because the bulk of the race scenario is recorded at 1 Hz ticks (start/end are higher rate),
    /// multiple unrelated skill procs can legitimately share the same FrameTime even if they did not
    /// trigger on the exact same simulation sub-frame.
    ///
    /// We preserve the order in which Score and Skill events appear in the event stream and do a 1:1
    /// consumption: each discrete score point value is attributed to at most one co-timed skill proc.
    /// This avoids both the previous full-bucket multiplication (every proc at T got the whole sum)
    /// and even splitting (a 500 + 1200 frame no longer gives ~850 to both a white and a gold).
    /// The actual point numbers recorded by the game (500 for whites, 1200 for golds, variable for uniques
    /// based on the proc's skill level + uma rarity) are used as-is.
    ///
    /// If there are more procs than score events at a coarse tick, the excess procs receive no points here
    /// (consistent with "no matching score event at that exact recorded time").
    /// </summary>
    public static IEnumerable<(int SkillId, double Points)> ScoreActivations(RaceAppearance appearance)
    {
        int horseIndex = appearance.Horse.FrameOrder - 1;
        if (horseIndex < 0)
        {
            yield break;
        }

        // Collect the individual point values from qualifying score events, in the order they appear
        // in the event stream, grouped by their FrameTime.
        // We prefer the set of ids loaded from team_stadium_raw_score (via GameMasterCatalog) so that
        // the exact set of TT score types (and their base amounts) come from master data (id + score columns),
        // with names from text_data 140 and descriptions from 141. Falls back to the old 26-57 range.
        var allowedRawScoreIds = GetSkillActivationRawScoreIds();
        var scoresByTime = new Dictionary<float, Queue<int>>();
        foreach (var e in appearance.Simulation.Events)
        {
            if (e.Type != SimulateEventType.Score || e.Params.Count < 3 || e.Params[0] != horseIndex)
            {
                continue;
            }
            int raw = e.Params[1];
            if (!allowedRawScoreIds.Contains(raw))
            {
                continue;
            }
            int points = e.Params[2];
            if (!scoresByTime.TryGetValue(e.FrameTime, out var q))
            {
                q = new Queue<int>();
                scoresByTime[e.FrameTime] = q;
            }
            q.Enqueue(points);
        }

        // Walk events in recorded order. For each Skill event belonging to this horse, consume
        // the next available score point value that was recorded at the exact same FrameTime (if any).
        // This gives a deterministic 1:1 pairing based on emit order within each coarse tick.
        foreach (var ev in appearance.Simulation.Events)
        {
            if (ev.Type != SimulateEventType.Skill || ev.Params.Count < 2 || ev.Params[0] != horseIndex)
            {
                continue;
            }

            float t = ev.FrameTime;
            int skillId = ev.Params[1];

            if (scoresByTime.TryGetValue(t, out var q) && q.Count > 0)
            {
                int points = q.Dequeue();
                yield return (skillId, points);
            }
            // No matching score event at this exact tick for this proc → it contributes 0 to the
            // "pts from condition 8 skill scores" (good-start bonuses are also excluded by the id range).
        }
    }

    private static HashSet<int> GetSkillActivationRawScoreIds()
    {
        var catalog = GameMasterService.Current?.Catalog;
        if (catalog != null)
        {
            var loaded = catalog.GetAllTeamTrialsRawScoreIds().ToList();
            if (loaded.Count > 0)
            {
                return new HashSet<int>(loaded);
            }
        }

        // No master data loaded (or embedded fallback has no raw scores yet) → legacy range.
        return new HashSet<int>(LegacySkillActivationRawScoreIds);
    }

    private static HashSet<int> BuildLegacySkillActivationRawScoreIds()
    {
        var ids = new HashSet<int>();
        for (int id = 26; id <= 57; id++)
        {
            ids.Add(id);
        }
        return ids;
    }
}