using System.Text.Encodings.Web;
using System.Text.Json;
using MessagePack;

namespace UmaBlobber.Import;

/// <summary>
/// Normalizes capture files to a JSON string (in memory). Supports .json text and legacy .bin MessagePack.
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
}