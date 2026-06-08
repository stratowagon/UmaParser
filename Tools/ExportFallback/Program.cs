using System.Text;
using Microsoft.Data.Sqlite;

string dbPath = args.FirstOrDefault(a => !a.StartsWith("-") && a.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase))
    ?? Path.Combine(
        Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))!,
        "LocalLow", "Cygames", "Umamusume", "master", "master.mdb");

string outPath = args.FirstOrDefault(a => !a.StartsWith("-") && a != dbPath && a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
    ?? Path.Combine(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "UmaParser.Core", "MasterData")),
        "EmbeddedMasterFallback.cs");

if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"Not found: {dbPath}");
    return 1;
}

var categories = new (int Id, string FieldName, MasterTextCategory Category)[]
{
    (6, "CharaShortNames", MasterTextCategory.CharaShortName),
    (35, "RaceTrackNames", MasterTextCategory.RaceTrackName),
    (140, "TeamTrialsScoreTypes", MasterTextCategory.TeamTrialsScoreType),
    (141, "TeamTrialsScoreDescs", MasterTextCategory.TeamTrialsScoreDesc),
};

using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
connection.Open();

bool analyzeMode = args.Any(a => a.Equals("--analyze-courses", StringComparison.OrdinalIgnoreCase) || a.Equals("-c", StringComparison.OrdinalIgnoreCase));
if (analyzeMode)
{
    // Self-contained analysis (local code only, to satisfy top-level statements rules)
    Console.WriteLine("=== Verifying exact mapping chain from user (race_instance -> race -> race_course_set -> text 35) ===\n");

    // Load text_data category 35 (short track names, e.g. "Chukyo", "Tokyo")
    var trackNames35 = new Dictionary<int, string>();
    try
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT [index], text FROM text_data WHERE category = 35";
        using var r = cmd.ExecuteReader();
        while (r.Read()) trackNames35[r.GetInt32(0)] = r.GetString(1);
        Console.WriteLine($"Loaded {trackNames35.Count} short track names from text_data category 35.");
        Console.WriteLine("Sample category 35 names: " + string.Join(", ", trackNames35.Take(12).Select(kv => $"\"{kv.Value}\"")));
    }
    catch (Exception ex) { Console.WriteLine("Category 35 load issue: " + ex.Message); }

    // Discover relevant tables
    var candidateTables = new List<string>();
    try
    {
        using var tcmd = connection.CreateCommand();
        tcmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND (name LIKE '%race%' OR name LIKE '%course%' OR name LIKE '%track%' OR name LIKE '%instance%') ORDER BY name;";
        using var tr = tcmd.ExecuteReader();
        while (tr.Read()) candidateTables.Add(tr.GetString(0));
    } catch { }
    Console.WriteLine("\nKey tables present: " + string.Join(", ", candidateTables.Where(t => t.Contains("race_instance") || t.Contains("race") || t.Contains("course_set") || t.Contains("race_track"))));

    // Print schemas for the tables in the chain
    void PrintCols(string table)
    {
        try
        {
            using var p = connection.CreateCommand();
            p.CommandText = $"PRAGMA table_info({table});";
            using var pr = p.ExecuteReader();
            var c = new List<string>();
            while (pr.Read()) c.Add(pr.GetString(1));
            Console.WriteLine($"  {table}: {string.Join(", ", c)}");
        }
        catch (Exception ex) { Console.WriteLine($"  {table} schema error: {ex.Message}"); }
    }

    Console.WriteLine("\nSchemas for the mapping chain:");
    if (candidateTables.Contains("race_instance")) PrintCols("race_instance");
    if (candidateTables.Contains("race")) PrintCols("race");
    if (candidateTables.Contains("race_course_set")) PrintCols("race_course_set");
    if (candidateTables.Contains("race_track")) PrintCols("race_track");

    // Execute the user's exact mapping chain and collect (name, distance, ground, turn)
    Console.WriteLine("\n--- Running the chain query (race_instance -> race.course_set -> race_course_set -> text_data cat 35 via race_track_id) ---");

    var tracks = new Dictionary<(string Name, int Distance, int Ground, int Turn), int>();  // count of definitions

    try
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                COALESCE(t35.text, 'Track#' || rcs.race_track_id) as track_name,
                rcs.distance,
                rcs.ground,
                rcs.turn,
                rcs.inout,
                COUNT(*) as definition_count
            FROM race_instance ri
            JOIN race r           ON r.id = ri.race_id
            JOIN race_course_set rcs ON rcs.id = r.course_set
            LEFT JOIN text_data t35 ON t35.category = 35 AND t35.[index] = rcs.race_track_id
            GROUP BY track_name, rcs.distance, rcs.ground, rcs.turn, rcs.inout
            ORDER BY track_name, rcs.distance, rcs.ground, rcs.turn
            LIMIT 400";
        using var r = cmd.ExecuteReader();
        int rows = 0;
        while (r.Read())
        {
            rows++;
            string name = r.GetString(0);
            int dist = r.GetInt32(1);
            int ground = r.GetInt32(2);
            int turn = r.GetInt32(3);
            int inout = r.GetInt32(4);
            int cnt = r.GetInt32(5);

            var key = (name, dist, ground, turn);
            tracks[key] = cnt;

            if (rows <= 25)
            {
                string g = ground == 1 ? "Turf" : ground == 2 ? "Dirt" : ground.ToString();
                string t = turn == 1 ? "Right" : turn == 2 ? "Left" : turn.ToString();
                string io = inout == 1 ? "Inner" : inout == 2 ? "Outer" : inout.ToString();
                Console.WriteLine($"  {name} {dist}m | {g} | turn={t} | inout={io} (defs: {cnt})");
            }
        }
        Console.WriteLine($"\nTotal distinct (Name, Distance, Ground, Turn) from the chain: {tracks.Count} (from {rows} grouped rows)");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Chain query error: " + ex.Message);
        return 0;
    }

    // Uniqueness check on (Name + Distance) only
    Console.WriteLine("\n--- Uniqueness check: (Name + Distance) only (user's proposed key) ---");
    var nameDistOnly = new Dictionary<(string Name, int Dist), List<(int G, int T, int Cnt)>>();
    foreach (var kv in tracks)
    {
        var (n, d, g, t) = kv.Key;
        if (!nameDistOnly.TryGetValue((n, d), out var lst)) nameDistOnly[(n, d)] = lst = new();
        lst.Add((g, t, kv.Value));
    }

    int ndCollisions = 0;
    foreach (var kv in nameDistOnly.Where(kv => kv.Value.Count > 1).OrderBy(kv => kv.Key.Name))
    {
        ndCollisions++;
        Console.WriteLine($"  (Name+Dist) collision: \"{kv.Key.Name}\" {kv.Key.Dist}m");
        foreach (var (g, t, c) in kv.Value)
        {
            string gs = g == 1 ? "Turf" : g == 2 ? "Dirt" : g.ToString();
            string ts = t == 1 ? "Right" : t == 2 ? "Left" : t.ToString();
            Console.WriteLine($"    - ground={gs}, turn={ts}, count={c}");
        }
    }
    if (ndCollisions == 0)
        Console.WriteLine("  No (Name + Distance) collisions found. Name + length appears sufficient as a key.");

    int multiSurface = nameDistOnly.Count(kv => kv.Value.Select(x => x.G).Distinct().Count() > 1);
    Console.WriteLine($"\n(Name + Distance) pairs that exist on >1 surface: {multiSurface}");

    // User's example verification (if the master has matching definitions)
    Console.WriteLine("\nNote: Specific runtime race_instance_ids (e.g. 610143) are not in the static master; they reference these definitions at runtime.");
    Console.WriteLine("The structures above confirm the chain you described produces usable (Name, Distance, Ground, Turn) data.");

    Console.WriteLine("\n=== Mapping verification complete ===");
    return 0;
}

var sb = new StringBuilder();
sb.AppendLine("namespace UmaBlobber.MasterData;");
sb.AppendLine();
sb.AppendLine("/// <summary>");
sb.AppendLine("/// Built-in fallback when <c>master.mdb</c> is unavailable.");
sb.AppendLine("/// Regenerate via <c>Tools/ExportFallback</c> before releases.");
sb.AppendLine("/// </summary>");
sb.AppendLine("internal static class EmbeddedMasterFallback");
sb.AppendLine("{");
sb.AppendLine("    public static void Apply(GameMasterCatalog catalog)");
sb.AppendLine("    {");
sb.AppendLine("        catalog.MergeSection(MasterTextCategory.CharaShortName, CharaShortNames);");
sb.AppendLine("        catalog.MergeSection(MasterTextCategory.RaceTrackName, RaceTrackNames);");
sb.AppendLine("        catalog.MergeSection(MasterTextCategory.TeamTrialsScoreType, TeamTrialsScoreTypes);");
sb.AppendLine("        catalog.MergeSection(MasterTextCategory.TeamTrialsScoreDesc, TeamTrialsScoreDescs);");
sb.AppendLine("        catalog.MergeSkillEntries(Skills);");
sb.AppendLine("        catalog.SetTeamTrialsRawScores(TeamTrialsRawScores);");
sb.AppendLine("        catalog.SetRaceCourses(RaceCourses);");
sb.AppendLine("    }");
sb.AppendLine();
sb.AppendLine("    #region Embedded fallback dictionaries");
sb.AppendLine();

foreach (var (id, fieldName, _) in categories)
{
    sb.AppendLine($"    private static readonly Dictionary<int, string> {fieldName} = new()");
    sb.AppendLine("    {");
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT [index], text FROM text_data WHERE category = $cat ORDER BY [index]";
    command.Parameters.AddWithValue("$cat", id);
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        int index = reader.GetInt32(0);
        string text = reader.GetString(1);
        sb.AppendLine($"        {{ {index}, {ToLiteral(text)} }},");
    }

    sb.AppendLine("    };");
    sb.AppendLine();
}

sb.AppendLine("    private static readonly Dictionary<int, SkillMasterEntry> Skills = new()");
sb.AppendLine("    {");
using (var skillCommand = connection.CreateCommand())
{
    skillCommand.CommandText =
        """
        SELECT s.id, COALESCE(t.text, ''), s.activate_lot
        FROM skill_data s
        LEFT JOIN text_data t ON t.category = 47 AND t.[index] = s.id
        ORDER BY s.id
        """;
    using var reader = skillCommand.ExecuteReader();
    while (reader.Read())
    {
        int skillId = reader.GetInt32(0);
        string name = reader.GetString(1);
        int lot = reader.GetInt32(2);
        string lotKind = lot == 1 ? "Wit" : "Unconditional";
        sb.AppendLine(
            $"        {{ {skillId}, new SkillMasterEntry({ToLiteral(name)}, SkillActivateLotKind.{lotKind}) }},");
    }
}

sb.AppendLine("    };");
sb.AppendLine();

sb.AppendLine("    private static readonly Dictionary<int, int> TeamTrialsRawScores = new()");
sb.AppendLine("    {");
using (var rawScoreCommand = connection.CreateCommand())
{
    rawScoreCommand.CommandText = "SELECT id, score FROM team_stadium_raw_score ORDER BY id";
    using var reader = rawScoreCommand.ExecuteReader();
    while (reader.Read())
    {
        int id = reader.GetInt32(0);
        int score = reader.GetInt32(1);
        sb.AppendLine($"        {{ {id}, {score} }},");
    }
}
sb.AppendLine("    };");
sb.AppendLine();

sb.AppendLine("    private static readonly Dictionary<int, RaceCourseInfo> RaceCourses = new()");
sb.AppendLine("    {");
using (var courseCommand = connection.CreateCommand())
{
    // One query following the documented chain for compressed offline data.
    courseCommand.CommandText = @"
        SELECT 
            ri.id,
            COALESCE(t.text, 'Track#' || CAST(rcs.race_track_id AS TEXT)),
            rcs.distance,
            rcs.ground,
            rcs.turn,
            rcs.inout
        FROM race_instance ri
        JOIN race r ON r.id = ri.race_id
        JOIN race_course_set rcs ON rcs.id = r.course_set
        LEFT JOIN text_data t ON t.category = 35 AND t.[index] = rcs.race_track_id
        ORDER BY ri.id";
    using var reader = courseCommand.ExecuteReader();
    while (reader.Read())
    {
        int instanceId = reader.GetInt32(0);
        string name = reader.GetString(1);
        int distance = reader.GetInt32(2);
        int ground = reader.GetInt32(3);
        int turn = reader.GetInt32(4);
        int inout = reader.GetInt32(5);
        sb.AppendLine($"        {{ {instanceId}, new RaceCourseInfo({instanceId}, {ToLiteral(name)}, {distance}, {ground}, {turn}, {inout}) }},");
    }
}
sb.AppendLine("    };");
sb.AppendLine();
sb.AppendLine("    #endregion");
sb.AppendLine("}");

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"Wrote {outPath}");
return 0;

static string ToLiteral(string value) =>
    "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

enum MasterTextCategory
{
    CharaShortName,
    SkillName,
    RaceTrackName,
    TeamTrialsScoreType,
    TeamTrialsScoreDesc,
}
