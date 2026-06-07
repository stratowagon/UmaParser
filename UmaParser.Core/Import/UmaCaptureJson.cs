using System.Text.Encodings.Web;
using System.Text.Json;
using UmaBlobber.DataModel.ResponseData;

namespace UmaBlobber.Import;

internal static class UmaCaptureJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new UmaApiResponseConverter() },
    };
}