using Microsoft.Data.Sqlite;

namespace UmaBlobber.MasterData;

internal static class MasterDbReader
{
    /// <summary>Categories loaded from <c>master.mdb</c> on refresh. Extend when new features need more tables.</summary>
    public static readonly MasterTextCategory[] LoadedTextCategories =
    [
        MasterTextCategory.CharaShortName,
        MasterTextCategory.SkillName,
        MasterTextCategory.TeamTrialsScoreType,
        MasterTextCategory.TeamTrialsScoreDesc,
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

            var rawScores = LoadTeamTrialsRawScores(dbPath);
            catalog.SetTeamTrialsRawScores(rawScores);

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
}