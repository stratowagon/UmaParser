using Microsoft.Data.Sqlite;

namespace UmaParser.MasterData;

/// <summary>
/// Opens <c>master.mdb</c> read-only only at startup or on an explicit refresh or browse,
/// reads it once, and closes it immediately. Never hold it open, and never write to it
/// or its folder. The game writes to <c>master.mdb</c> during updates.
/// </summary>
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

        // One short read of the original file. Pooling is off and the connection is
        // released in finally, including when the file is not a database, so the game
        // can replace master.mdb as soon as this method returns.
        SqliteConnection? connection = null;
        try
        {
            connection = new SqliteConnection(ReadOnlyConnectionString(dbPath));
            connection.Open();

            var sections = LoadTextSections(connection, LoadedTextCategories);
            foreach (var category in LoadedTextCategories)
            {
                catalog.SetSection(category, sections[category]);
            }

            ApplySkillMetadata(catalog, sections, LoadSkillActivateLot(connection));

            sections.TryGetValue(MasterTextCategory.TeamTrialsScoreType, out var scoreNames);
            sections.TryGetValue(MasterTextCategory.TeamTrialsScoreDesc, out var scoreDescriptions);
            var rawScores = LoadTeamTrialsRawScores(connection);
            catalog.SetTeamTrialsScores(TeamTrialsScoreEntry.Merge(
                scoreNames ?? new Dictionary<int, string>(),
                scoreDescriptions ?? new Dictionary<int, string>(),
                rawScores));

            catalog.SetRaceCourses(LoadRaceCourses(connection));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            if (connection != null)
            {
                Release(connection);
            }
        }
    }

    /// <summary>
    /// Read-only, unpooled. SQLite is not asked to write, so it does not create a
    /// journal, WAL, or other file next to <paramref name="dbPath"/>.
    /// </summary>
    private static string ReadOnlyConnectionString(string dbPath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
    }

    private static void Release(SqliteConnection connection)
    {
        try
        {
            connection.Close();
        }
        finally
        {
            SqliteConnection.ClearPool(connection);
            connection.Dispose();
        }
    }

    private static Dictionary<MasterTextCategory, Dictionary<int, string>> LoadTextSections(
        SqliteConnection connection,
        IReadOnlyList<MasterTextCategory> categories)
    {
        var result = categories.ToDictionary(c => c, _ => new Dictionary<int, string>());
        string categoryList = string.Join(", ", categories.Select(c => ((int)c).ToString()));

        ReadRows(
            connection,
            $"SELECT category, [index], text FROM text_data WHERE category IN ({categoryList})",
            reader =>
            {
                int categoryId = reader.GetInt32(0);
                int index = reader.GetInt32(1);
                string text = reader.GetString(2);

                if (!Enum.IsDefined(typeof(MasterTextCategory), categoryId))
                {
                    return;
                }

                var category = (MasterTextCategory)categoryId;
                if (!result.TryGetValue(category, out var map))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(text))
                {
                    map[index] = text;
                }
            });

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

    private static Dictionary<int, SkillActivateLotKind> LoadSkillActivateLot(SqliteConnection connection)
    {
        var result = new Dictionary<int, SkillActivateLotKind>();
        ReadRows(connection, "SELECT id, activate_lot FROM skill_data", reader =>
        {
            int id = reader.GetInt32(0);
            int lot = reader.GetInt32(1);
            result[id] = lot == 1 ? SkillActivateLotKind.Wit : SkillActivateLotKind.Unconditional;
        });
        return result;
    }

    private static Dictionary<int, int> LoadTeamTrialsRawScores(SqliteConnection connection)
    {
        var result = new Dictionary<int, int>();
        // id and score (base amount) from team_stadium_raw_score.
        // Join with text_data category 140 (name) / 141 (desc) is done via MasterTextCategory.
        ReadRows(connection, "SELECT id, score FROM team_stadium_raw_score", reader =>
        {
            result[reader.GetInt32(0)] = reader.GetInt32(1);
        });
        return result;
    }

    /// <summary>
    /// Loads race course information by following the chain:
    /// race_instance.id (key for captures) → race_instance.race_id → race.id → race.course_set → race_course_set
    /// race_course_set.race_track_id → text_data (category 35) for the short track name.
    /// This produces a compressed view with the columns needed for performance tracking.
    /// </summary>
    private static Dictionary<int, RaceCourseInfo> LoadRaceCourses(SqliteConnection connection)
    {
        var result = new Dictionary<int, RaceCourseInfo>();
        // Keyed by race_instance.id because that's what appears in capture data (RaceStartParams.race_instance_id).
        ReadRows(
            connection,
            """
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
            ORDER BY track_name, rcs.distance, rcs.ground
            """,
            reader =>
            {
                result[reader.GetInt32(0)] = new RaceCourseInfo(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5));
            });
        return result;
    }

    private static void ReadRows(SqliteConnection connection, string sql, Action<SqliteDataReader> read)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            read(reader);
        }
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
