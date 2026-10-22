using System.Text.Json;
using System.Text.RegularExpressions;
using UmaParser.Core.Tests.TestSupport;
using Xunit;

namespace UmaParser.Core.Tests.Privacy;

public sealed class FixturePrivacyTests
{
    static readonly Regex TrainerName = new(@"^(?:Player|Trainer\d{2,})$", RegexOptions.CultureInvariant);

    [Fact]
    public void Committed_fixtures_have_no_trainer_identity()
    {
        var failures = new List<string>();
        foreach (FixtureEntry entry in FixtureCatalog.All)
        {
            string text = FixtureCatalog.ReadText(entry);
            if (text.Contains("Bojak", StringComparison.Ordinal))
            {
                failures.Add($"{entry.RelativePath}: contains Bojak");
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                Walk(document.RootElement, entry.RelativePath, failures);
            }
            catch (JsonException ex)
            {
                failures.Add($"{entry.RelativePath}: {ex.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    static void Walk(JsonElement element, string relativePath, List<string> failures)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string normalized = Norm(property.Name);
                    if (normalized is "viewerid" or "ownerviewerid")
                    {
                        CheckId(relativePath, property, failures);
                    }
                    else if (normalized is "sid" or "username" or "circlename")
                    {
                        CheckBlank(relativePath, property, failures);
                    }
                    else if (normalized is "trainername" or "ownertrainername")
                    {
                        CheckTrainer(relativePath, property, failures);
                    }
                    else
                    {
                        Walk(property.Value, relativePath, failures);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Walk(item, relativePath, failures);
                }

                break;
        }
    }

    static void CheckId(string relativePath, JsonProperty property, List<string> failures)
    {
        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out long id))
        {
            if (id != 0)
            {
                failures.Add($"{relativePath}: {property.Name} is {id}");
            }

            return;
        }

        if (property.Value.ValueKind is JsonValueKind.Null)
        {
            return;
        }

        failures.Add($"{relativePath}: {property.Name} is not integer 0 ({property.Value.ValueKind})");
    }

    static void CheckBlank(string relativePath, JsonProperty property, List<string> failures)
    {
        if (property.Value.ValueKind == JsonValueKind.String)
        {
            string? value = property.Value.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                failures.Add($"{relativePath}: {property.Name} is not empty");
            }

            return;
        }

        if (property.Value.ValueKind is JsonValueKind.Null)
        {
            return;
        }

        failures.Add($"{relativePath}: {property.Name} is not an empty string ({property.Value.ValueKind})");
    }

    static void CheckTrainer(string relativePath, JsonProperty property, List<string> failures)
    {
        if (property.Value.ValueKind != JsonValueKind.String)
        {
            if (property.Value.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"{relativePath}: {property.Name} is not a string");
            }

            return;
        }

        string value = property.Value.GetString() ?? "";
        if (value.Length == 0)
        {
            return;
        }

        if (!TrainerName.IsMatch(value))
        {
            failures.Add($"{relativePath}: {property.Name} is \"{value}\"");
        }
    }

    static string Norm(string key)
    {
        Match match = BackingField.Match(key);
        if (match.Success)
        {
            key = match.Groups[1].Value;
        }

        return key.TrimStart('_').Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }

    static readonly Regex BackingField = new(
        @"^<(.*)>k__BackingField$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
