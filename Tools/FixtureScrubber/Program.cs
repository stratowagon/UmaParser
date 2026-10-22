using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Scrubs race-capture JSON so it can be committed to this public repo.
// Reads a source folder and writes minified copies to a different destination.
// The source files are only opened for reading.
//
// Usage:
//   dotnet run --project Tools/FixtureScrubber -- <source> <destination> --owner <TrainerName>
//
// --owner is the in-game trainer name to replace with "Player" (matched exactly, per file).
// Every other non-empty trainer name in that file becomes Trainer01, Trainer02, ...
// in order of first appearance. Empty names stay empty.
// viewer_id / owner_viewer_id integers are set to 0.
// sid, user_name, and circle_name strings are cleared.
// sim blobs and every other field are left as they are.
//
// Key matching follows the same normalization as the original scrub:
// strip a leading <Name>k__BackingField, strip leading underscores, remove
// remaining underscores, then lower-case. So trainer_name, trainerName, and
// <TrainerName>k__BackingField all hit the same rule.

if (args.Length == 0 || args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintHelp();
    return args.Length == 0 ? 1 : 0;
}

string? sourceArg = null;
string? destArg = null;
string? owner = null;
for (int i = 0; i < args.Length; i++)
{
    string arg = args[i];
    if (arg == "--owner" && i + 1 < args.Length)
    {
        owner = args[++i];
        continue;
    }

    if (arg.StartsWith("--owner=", StringComparison.Ordinal))
    {
        owner = arg["--owner=".Length..];
        continue;
    }

    if (arg.StartsWith('-'))
    {
        Console.Error.WriteLine($"Unknown option: {arg}");
        PrintHelp();
        return 1;
    }

    if (sourceArg == null)
    {
        sourceArg = arg;
    }
    else if (destArg == null)
    {
        destArg = arg;
    }
    else
    {
        Console.Error.WriteLine("Too many arguments.");
        PrintHelp();
        return 1;
    }
}

if (string.IsNullOrWhiteSpace(sourceArg) || string.IsNullOrWhiteSpace(destArg) || string.IsNullOrWhiteSpace(owner))
{
    Console.Error.WriteLine("Source, destination, and --owner are required.");
    PrintHelp();
    return 1;
}

string source = Path.GetFullPath(sourceArg);
string destination = Path.GetFullPath(destArg);
if (!Directory.Exists(source))
{
    Console.Error.WriteLine($"Source folder not found: {source}");
    return 1;
}

if (PathsOverlap(source, destination))
{
    Console.Error.WriteLine("Refusing to write into the source folder.");
    Console.Error.WriteLine($"  source:      {source}");
    Console.Error.WriteLine($"  destination: {destination}");
    Console.Error.WriteLine("Pick a destination outside the source tree. This tool never modifies its input.");
    return 2;
}

if (File.Exists(destination))
{
    Console.Error.WriteLine($"Destination is a file, not a folder: {destination}");
    return 1;
}

Directory.CreateDirectory(destination);

var jsonOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = false,
};
var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

int filesWritten = 0;
int idsZeroed = 0;
int namesRewritten = 0;
int fieldsBlanked = 0;
int unchanged = 0;
int maxOtherNames = 0;
var failures = new List<string>();

foreach (string inputPath in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
{
    string relative = Path.GetRelativePath(source, inputPath);
    if (relative.StartsWith('_') || relative.Contains($"{Path.DirectorySeparatorChar}_", StringComparison.Ordinal))
    {
        continue;
    }

    string outputPath = Path.Combine(destination, relative);
    string original = File.ReadAllText(inputPath);
    JsonNode? root;
    try
    {
        root = JsonNode.Parse(original);
    }
    catch (JsonException ex)
    {
        failures.Add($"{relative}: {ex.Message}");
        continue;
    }

    if (root == null)
    {
        failures.Add($"{relative}: JSON was empty.");
        continue;
    }

    var names = new Dictionary<string, string>(StringComparer.Ordinal);
    var stats = new FileStats();
    Walk(root, names, owner, stats);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, root.ToJsonString(jsonOptions), utf8);

    filesWritten++;
    idsZeroed += stats.Ids;
    namesRewritten += stats.Names;
    fieldsBlanked += stats.Blanked;
    if (names.Count > maxOtherNames)
    {
        maxOtherNames = names.Count;
    }

    if (stats.Ids == 0 && stats.Names == 0 && stats.Blanked == 0)
    {
        unchanged++;
    }

    Console.WriteLine(
        $"{relative}  ids={stats.Ids} names={stats.Names} blanked={stats.Blanked} otherTrainers={names.Count}");
}

Console.WriteLine();
Console.WriteLine($"Wrote {filesWritten} file(s) to {destination}");
Console.WriteLine($"  viewer_id / owner_viewer_id set to 0: {idsZeroed}");
Console.WriteLine($"  trainer names rewritten: {namesRewritten}");
Console.WriteLine($"  sid / user_name / circle_name cleared: {fieldsBlanked}");
Console.WriteLine($"  files with no field changes (still minified): {unchanged}");
Console.WriteLine($"  most other trainers in one file: {maxOtherNames}");

foreach (string outputPath in Directory.EnumerateFiles(destination, "*.json", SearchOption.AllDirectories))
{
    string relative = Path.GetRelativePath(destination, outputPath);
    string text = File.ReadAllText(outputPath);
    if (text.Contains(owner, StringComparison.Ordinal))
    {
        failures.Add($"{relative}: still contains the owner name \"{owner}\".");
    }

    try
    {
        JsonNode? node = JsonNode.Parse(text);
        if (node != null)
        {
            CollectGateViolations(node, relative, failures);
        }
    }
    catch (JsonException ex)
    {
        failures.Add($"{relative}: scrubbed JSON did not parse ({ex.Message}).");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Privacy gate failed. Do not commit the destination.");
    foreach (string failure in failures)
    {
        Console.Error.WriteLine("  " + failure);
    }

    return 1;
}

Console.WriteLine("Privacy gate passed.");
return 0;

static void Walk(JsonNode node, Dictionary<string, string> names, string owner, FileStats stats)
{
    switch (node)
    {
        case JsonObject obj:
            foreach (string key in obj.Select(pair => pair.Key).ToList())
            {
                string normalized = Norm(key);
                JsonNode? child = obj[key];
                if (normalized is "viewerid" or "ownerviewerid"
                    && TryNonZeroInteger(child, out _))
                {
                    obj[key] = 0;
                    stats.Ids++;
                }
                else if (normalized is "trainername" or "ownertrainername"
                    && child is JsonValue nameValue
                    && nameValue.TryGetValue<string>(out string? trainer)
                    && !string.IsNullOrEmpty(trainer))
                {
                    obj[key] = Pseudonym(trainer, owner, names);
                    stats.Names++;
                }
                else if (normalized is "sid" or "username" or "circlename"
                    && child is JsonValue blankValue
                    && blankValue.TryGetValue<string>(out string? text)
                    && !string.IsNullOrEmpty(text))
                {
                    obj[key] = "";
                    stats.Blanked++;
                }
                else if (child != null)
                {
                    Walk(child, names, owner, stats);
                }
            }

            break;
        case JsonArray array:
            foreach (JsonNode? item in array)
            {
                if (item != null)
                {
                    Walk(item, names, owner, stats);
                }
            }

            break;
    }
}

static string Pseudonym(string trainer, string owner, Dictionary<string, string> names)
{
    if (string.Equals(trainer, owner, StringComparison.Ordinal))
    {
        return "Player";
    }

    if (names.TryGetValue(trainer, out string? existing))
    {
        return existing;
    }

    string pseudo = $"Trainer{names.Count + 1:D2}";
    names[trainer] = pseudo;
    return pseudo;
}

static bool TryNonZeroInteger(JsonNode? node, out long value)
{
    value = 0;
    return node is JsonValue json
        && json.TryGetValue<long>(out value)
        && value != 0;
}

static void CollectGateViolations(JsonNode node, string relative, List<string> failures)
{
    switch (node)
    {
        case JsonObject obj:
            foreach (KeyValuePair<string, JsonNode?> pair in obj)
            {
                string normalized = Norm(pair.Key);
                JsonNode? child = pair.Value;
                if (normalized is "viewerid" or "ownerviewerid" && TryNonZeroInteger(child, out long id))
                {
                    failures.Add($"{relative}: {pair.Key} is still {id}.");
                }
                else if (normalized is "sid" or "username" or "circlename"
                    && child is JsonValue blankValue
                    && blankValue.TryGetValue<string>(out string? text)
                    && !string.IsNullOrEmpty(text))
                {
                    failures.Add($"{relative}: {pair.Key} is not empty.");
                }
                else if (child != null)
                {
                    CollectGateViolations(child, relative, failures);
                }
            }

            break;
        case JsonArray array:
            foreach (JsonNode? item in array)
            {
                if (item != null)
                {
                    CollectGateViolations(item, relative, failures);
                }
            }

            break;
    }
}

static string Norm(string key)
{
    Match match = ScrubPatterns.BackingField.Match(key);
    if (match.Success)
    {
        key = match.Groups[1].Value;
    }

    return key.TrimStart('_').Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
}

static bool PathsOverlap(string left, string right)
{
    return IsSameOrChild(left, right) || IsSameOrChild(right, left);
}

static bool IsSameOrChild(string parent, string child)
{
    string parentFull = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    string childFull = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    return childFull.Equals(parentFull, StringComparison.OrdinalIgnoreCase)
        || childFull.StartsWith(parentFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}

static void PrintHelp()
{
    Console.WriteLine("""
        FixtureScrubber <source> <destination> --owner <TrainerName>

        Read race JSON from <source> and write scrubbed, minified copies to <destination>.
        Does not modify <source>. Refuses to write into the source folder.

          --owner <name>   In-game trainer name to replace with "Player".
                           Other names in the same file become Trainer01, Trainer02, ...
                           in order of first appearance. The match is exact.
                           The run fails if that name is still present afterward.
        """);
}

internal sealed class FileStats
{
    public int Ids { get; set; }
    public int Names { get; set; }
    public int Blanked { get; set; }
}

internal static class ScrubPatterns
{
    public static readonly Regex BackingField = new(
        @"^<(.*)>k__BackingField$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
