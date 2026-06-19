using System.Text.Encodings.Web;
using System.Text.Json;
using UmaParser.DataModel.ResponseData;

namespace UmaParser.Import;

internal static class UmaCaptureJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new UmaApiResponseConverter() },
    };
}