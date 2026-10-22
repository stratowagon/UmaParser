using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace UmaParser.Core.Tests.TestSupport;

/// <summary>
/// Hand-rolled golden master. A snapshot test turns a result into JSON and compares it to a
/// file that was checked in after a human looked at it.
///
/// Files live in tests/UmaParser.Core.Tests/Snapshots/{Class}.{Test}[.{suffix}].verified.json.
/// Doubles are rounded to 6 decimal places so tiny cross-platform floating-point noise does not
/// fail the test. A missing snapshot never passes silently: the test fails and writes a sibling
/// .received.json.
///
/// To accept the current output as the new golden master, re-run with UPDATE_SNAPSHOTS=1.
/// That overwrites each .verified.json that is missing or different, and the test passes.
/// Review the git diff before committing. The first file you accept is the spec.
/// </summary>
internal static class Snapshot
{
    static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public static string Canonicalize(object value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        json = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!json.EndsWith('\n'))
        {
            json += "\n";
        }

        return json;
    }

    public static void Match(
        object value,
        string? suffix = null,
        [CallerFilePath] string sourceFile = "",
        [CallerMemberName] string testName = "")
    {
        string actual = Canonicalize(value);
        string verifiedPath = VerifiedPath(sourceFile, testName, suffix);
        string receivedPath = verifiedPath.Replace(".verified.json", ".received.json", StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(verifiedPath)!);

        if (!File.Exists(verifiedPath))
        {
            if (Accepting)
            {
                Write(verifiedPath, actual);
                Console.WriteLine($"Wrote new snapshot {verifiedPath}");
                return;
            }

            Write(receivedPath, actual);
            Assert.Fail(
                $"Snapshot missing: {Path.GetFileName(verifiedPath)}{Environment.NewLine}" +
                $"The received output was written to {receivedPath}.{Environment.NewLine}" +
                "If this output is correct, re-run with UPDATE_SNAPSHOTS=1 and commit the verified file.");
        }

        string expected = File.ReadAllText(verifiedPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (expected == actual)
        {
            if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath);
            }

            return;
        }

        if (Accepting)
        {
            Write(verifiedPath, actual);
            if (File.Exists(receivedPath))
            {
                File.Delete(receivedPath);
            }

            Console.WriteLine($"Updated snapshot {verifiedPath}");
            return;
        }

        Write(receivedPath, actual);
        Assert.Fail(
            $"Snapshot differs: {Path.GetFileName(verifiedPath)}{Environment.NewLine}" +
            $"Received output: {receivedPath}{Environment.NewLine}" +
            "If the change is intended, re-run with UPDATE_SNAPSHOTS=1 and commit the verified file." +
            Environment.NewLine + Environment.NewLine +
            UnifiedDiff(expected, actual));
    }

    public static void AssertSame(object expected, object actual, string because)
    {
        string left = Canonicalize(expected);
        string right = Canonicalize(actual);
        if (left == right)
        {
            return;
        }

        Assert.Fail(because + Environment.NewLine + UnifiedDiff(left, right));
    }

    static bool Accepting =>
        string.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "1", StringComparison.Ordinal);

    static string VerifiedPath(string sourceFile, string testName, string? suffix)
    {
        string projectDir = FindProjectDir(sourceFile);
        string className = Path.GetFileNameWithoutExtension(sourceFile);
        string extra = string.IsNullOrEmpty(suffix) ? "" : "." + suffix;
        return Path.Combine(projectDir, "Snapshots", $"{className}.{testName}{extra}.verified.json");
    }

    static string FindProjectDir(string sourceFile)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not find the test project from {sourceFile}.");
    }

    static void Write(string path, string text)
    {
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    static string UnifiedDiff(string expected, string actual, int maxLines = 80)
    {
        string[] oldLines = expected.Split('\n');
        string[] newLines = actual.Split('\n');
        var diff = new StringBuilder();
        diff.AppendLine("--- verified");
        diff.AppendLine("+++ received");
        int i = 0;
        int j = 0;
        int shown = 0;
        while ((i < oldLines.Length || j < newLines.Length) && shown < maxLines)
        {
            if (i < oldLines.Length && j < newLines.Length && oldLines[i] == newLines[j])
            {
                i++;
                j++;
                continue;
            }

            int syncOld = -1;
            int syncNew = -1;
            const int window = 40;
            for (int deltaOld = 0; deltaOld <= window && i + deltaOld < oldLines.Length && syncOld < 0; deltaOld++)
            {
                for (int deltaNew = 0; deltaNew <= window && j + deltaNew < newLines.Length; deltaNew++)
                {
                    if (oldLines[i + deltaOld].Length > 0 && oldLines[i + deltaOld] == newLines[j + deltaNew])
                    {
                        syncOld = i + deltaOld;
                        syncNew = j + deltaNew;
                        break;
                    }
                }
            }

            int endOld = syncOld < 0 ? Math.Min(oldLines.Length, i + 20) : syncOld;
            int endNew = syncNew < 0 ? Math.Min(newLines.Length, j + 20) : syncNew;
            diff.AppendLine($"@@ -{i + 1} +{j + 1} @@");
            for (int line = i; line < endOld && shown < maxLines; line++)
            {
                diff.AppendLine("-" + oldLines[line]);
                shown++;
            }

            for (int line = j; line < endNew && shown < maxLines; line++)
            {
                diff.AppendLine("+" + newLines[line]);
                shown++;
            }

            if (syncOld < 0)
            {
                break;
            }

            i = syncOld;
            j = syncNew;
        }

        if (shown >= maxLines)
        {
            diff.AppendLine("... diff truncated");
        }

        return diff.ToString();
    }

    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };
        options.Converters.Add(new RoundedDoubleConverter());
        options.Converters.Add(new RoundedFloatConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    sealed class RoundedDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            WriteRounded(writer, value);
        }
    }

    sealed class RoundedFloatConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetSingle();

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            WriteRounded(writer, value);
        }
    }

    static void WriteRounded(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value))
        {
            writer.WriteRawValue("NaN");
            return;
        }

        if (double.IsPositiveInfinity(value))
        {
            writer.WriteRawValue("Infinity");
            return;
        }

        if (double.IsNegativeInfinity(value))
        {
            writer.WriteRawValue("-Infinity");
            return;
        }

        double rounded = Math.Round(value, 6, MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            rounded = 0;
        }

        writer.WriteRawValue(rounded.ToString("0.######", CultureInfo.InvariantCulture));
    }
}
