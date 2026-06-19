using Microsoft.Data.Sqlite;

namespace UmaParser.MasterData;

internal static class MasterDbReader
{
    /// <summary>Categories loaded from <c>master.mdb</c> on refresh. Extend when new features need more tables.</summary>
    public static readonly MasterTextCategory[] LoadedTextCategories =
    [
        MasterTextCategory.CharaShortName,
        MasterTextCategory.SkillName,
        MasterTextCategory.TeamTrialsScoreType,
        MasterTextCategory.TeamTrialsScoreDesc,
        MasterTextCategory.RaceTrackName,
        MasterTextCategory.FactorSkillName,
        MasterTextCategory.ScoreBonusType,
    ];

    public static bool TryLoad(string dbPath, GameMasterCatalog catalog, out string? error)
    {
        error = null;
        if (!File.Exists(dbPath))
        {
            error = "Master database file not found.";
            return false;
        }

        try
        {
            var sections = LoadTextSections(dbPath, LoadedTextCategories);
            foreach (var category in LoadedTextCategories)
            {
                catalog.SetSection(category, sections[category]);
            }

            ApplySkillMetadata(catalog, sections, LoadSkillActivateLot(dbPath));

            sections.TryGetValue(MasterTextCategory.TeamTrialsScoreType, out var scoreNames);
            sections.TryGetValue(MasterTextCategory.TeamTrialsScoreDesc, out var scoreDescriptions);
            var rawScores = LoadTeamTrialsRawScores(dbPath);
            catalog.SetTeamTrialsScores(TeamTrialsScoreEntry.Merge(
                scoreNames ?? new Dictionary<int, string>(),
                scoreDescriptions ?? new Dictionary<int, string>(),
                rawScores));

            var raceCourses = LoadRaceCourses(dbPath);
            catalog.SetRaceCourses(raceCourses);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static Dictionary<MasterTextCategory, Dictionary<int, string>> LoadTextSections(
        string dbPath,
        IReadOnlyList<MasterTextCategory> categories)
    {
        var result = categories.ToDictionary(c => c, _ => new Dictionary<int, string>());
        string categoryList = string.Join(", ", categories.Select(c => ((int)c).ToString()));

        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT category, [index], text FROM text_data WHERE category IN ({categoryList})";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int categoryId = reader.GetInt32(0);
            int index = reader.GetInt32(1);
            string text = reader.GetString(2);

            if (!Enum.IsDefined(typeof(MasterTextCategory), categoryId))
            {
                continue;
            }

            var category = (MasterTextCategory)categoryId;
            if (!result.TryGetValue(category, out var map))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(text))
            {
                map[index] = text;
            }
        }

        return result;
    }

    private static void ApplySkillMetadata(
        GameMasterCatalog catalog,
        Dictionary<MasterTextCategory, Dictionary<int, string>> sections,
        Dictionary<int, SkillActivateLotKind> lots)
    {
        sections.TryGetValue(MasterTextCategory.SkillName, out var names);
        names ??= new Dictionary<int, string>();

        var entries = new Dictionary<int, SkillMasterEntry>();
        foreach (int skillId in names.Keys.Union(lots.Keys))
        {
            entries[skillId] = new SkillMasterEntry(
                names.GetValueOrDefault(skillId, string.Empty),
                lots.GetValueOrDefault(skillId, SkillActivateLotKind.Unknown));
        }

        catalog.SetSkillEntries(entries);
    }

    internal static Dictionary<int, SkillActivateLotKind> LoadSkillActivateLot(string dbPath)
    {
        var result = new Dictionary<int, SkillActivateLotKind>();
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, activate_lot FROM skill_data";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            int lot = reader.GetInt32(1);
            result[id] = lot == 1 ? SkillActivateLotKind.Wit : SkillActivateLotKind.Unconditional;
        }

        return result;
    }

    private static Dictionary<int, int> LoadTeamTrialsRawScores(string dbPath)
    {
        var result = new Dictionary<int, int>();
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        // id and score (base amount) from team_stadium_raw_score.
        // Join with text_data category 140 (name) / 141 (desc) is done via MasterTextCategory.
        command.CommandText = "SELECT id, score FROM team_stadium_raw_score";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            int score = reader.GetInt32(1);
            result[id] = score;
        }

        return result;
    }

    /// <summary>
    /// Loads race course information by following the chain:
    /// race_instance.id (key for captures) → race_instance.race_id → race.id → race.course_set → race_course_set
    /// race_course_set.race_track_id → text_data (category 35) for the short track name.
    /// This produces a compressed view with the columns needed for performance tracking.
    /// </summary>
    private static Dictionary<int, RaceCourseInfo> LoadRaceCourses(string dbPath)
    {
        var result = new Dictionary<int, RaceCourseInfo>();
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        // One query doing the joins as described by the user.
        // Keyed by race_instance.id because that's what appears in capture data (RaceStartParams.race_instance_id).
        command.CommandText = @"
            SELECT 
                ri.id as instance_id,
                COALESCE(t.text, 'Track#' || CAST(rcs.race_track_id AS TEXT)) as track_name,
                rcs.distance,
                rcs.ground,
                rcs.turn,
                rcs.inout
            FROM race_instance ri
            JOIN race r ON r.id = ri.race_id
            JOIN race_course_set rcs ON rcs.id = r.course_set
            LEFT JOIN text_data t ON t.category = 35 AND t.[index] = rcs.race_track_id
            ORDER BY track_name, rcs.distance, rcs.ground";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            int instanceId = reader.GetInt32(0);
            string name = reader.GetString(1);
            int distance = reader.GetInt32(2);
            int ground = reader.GetInt32(3);
            int turn = reader.GetInt32(4);
            int inout = reader.GetInt32(5);

            result[instanceId] = new RaceCourseInfo(instanceId, name, distance, ground, turn, inout);
        }

        return result;
    }
}

/// <summary>
/// Compressed race course / track information resolved via the master joins.
/// Surface (Ground) is kept internally for uniqueness even if the UI often omits it.
/// </summary>
public sealed record RaceCourseInfo(
    int InstanceId,      // race_instance.id - the key that appears in capture data
    string Name,         // short name from text_data 35, e.g. "Chukyo"
    int Distance,
    int Ground,          // 1=Turf, 2=Dirt (typically)
    int Turn,            // 1=Right, 2=Left (typically)
    int InOut            // 1=Inner, 2=Outer (typically)
);