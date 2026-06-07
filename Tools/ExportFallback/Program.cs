using System.Text;
using Microsoft.Data.Sqlite;

string dbPath = args.Length > 0
    ? args[0]
    : Path.Combine(
        Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))!,
        "LocalLow", "Cygames", "Umamusume", "master", "master.mdb");

string outPath = args.Length > 1
    ? args[1]
    : Path.Combine(
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
};

using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
connection.Open();

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
sb.AppendLine("        catalog.MergeSkillEntries(Skills);");
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
        string lotKind = lot == 1 ? "WitLottery" : "Unconditional";
        sb.AppendLine(
            $"        {{ {skillId}, new SkillMasterEntry({ToLiteral(name)}, SkillActivateLotKind.{lotKind}) }},");
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
}