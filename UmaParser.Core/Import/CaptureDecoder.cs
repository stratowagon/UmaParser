using System.Text.Encodings.Web;
using System.Text.Json;
using MessagePack;

namespace UmaParser.Import;

/// <summary>
/// Normalizes capture files to a JSON string (in memory). Supports .json text and legacy .bin MessagePack.
/// Also provides lightweight normalization for certain third-party / internal capture dumps that use
/// compiler-generated backing field property names (e.g. "&lt;RaceType&gt;k__BackingField").
/// </summary>
public static class CaptureDecoder
{
    public static bool TryDecodeToJson(
        string fileName,
        string? jsonText,
        byte[]? binData,
        out string json,
        out string? error)
    {
        json = string.Empty;
        error = null;

        if (!string.IsNullOrWhiteSpace(jsonText))
        {
            json = jsonText;
            return true;
        }

        if (binData is { Length: > 0 })
        {
            try
            {
                json = MsgPackToJson(binData);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        error = $"No content to decode for {fileName}.";
        return false;
    }

    public static string MsgPackToJson(byte[] binData)
    {
        object? obj = MessagePackSerializer.Deserialize<object>(binData, MessagePackSerializerOptions.Standard);

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = false,
            MaxDepth = 64,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    /// <summary>
    /// Detects capture dumps that use .NET backing-field serialized names (e.g. from certain race export tools)
    /// and rewrites property names of the form "&lt;Name&gt;k__BackingField" to the clean "Name".
    /// This is a lightweight recursive rewrite using JsonDocument so we do not need to define DTOs for
    /// every backing-field-heavy object in very large capture files (full career histories etc.).
    /// Only properties are renamed; values (including huge base64 scenario blobs) are preserved as-is.
    /// </summary>
    public static string NormalizeBackingFieldJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return rawJson;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var cleaned = CleanElement(doc.RootElement);
            return JsonSerializer.Serialize(cleaned, new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }
        catch
        {
            // If anything goes wrong (malformed, too deep, etc.) just return the original so the
            // normal TT/Fallback path can still run and report a useful error.
            return rawJson;
        }
    }

    private static object? CleanElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var prop in element.EnumerateObject())
                {
                    string cleanName = CleanPropertyName(prop.Name);
                    obj[cleanName] = CleanElement(prop.Value);
                }
                return obj;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(CleanElement(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out long l)) return l;
                if (element.TryGetDouble(out double d)) return d;
                return element.GetRawText();

            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            default:
                return null;
        }
    }

    private static string CleanPropertyName(string name)
    {
        // Common pattern from the provided samples: "<Foo>k__BackingField" or "<Bar>k__BackingField"
        if (name.StartsWith("<") && name.Contains(">k__BackingField"))
        {
            int end = name.IndexOf('>');
            if (end > 1)
                return name.Substring(1, end - 1);
        }
        return name;
    }
}