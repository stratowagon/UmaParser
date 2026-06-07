using System.Text.Json;
using UmaBlobber.DataModel.ResponseData;
using UmaBlobber.ObjectModel;

namespace UmaBlobber.Import;

public static class CaptureImportService
{
    public static CaptureImportResult TryImportPath(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string extension = Path.GetExtension(filePath);

        try
        {
            if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return TryImport(fileName, jsonText: File.ReadAllText(filePath));
            }

            if (extension.Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                return TryImport(fileName, binData: File.ReadAllBytes(filePath));
            }

            return UnsupportedExtension(fileName, extension);
        }
        catch (Exception ex)
        {
            return Failed(fileName, ImportStatus.InvalidFormat, ex.Message);
        }
    }

    public static CaptureImportResult TryImport(
        string fileName,
        string? jsonText = null,
        byte[]? binData = null)
    {
        if (string.IsNullOrWhiteSpace(jsonText) && (binData == null || binData.Length == 0))
        {
            return Failed(fileName, ImportStatus.EmptyContent, "No JSON or binary content provided.");
        }

        if (!CaptureDecoder.TryDecodeToJson(fileName, jsonText, binData, out string json, out string? decodeError))
        {
            return Failed(fileName, ImportStatus.InvalidFormat, decodeError);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Failed(fileName, ImportStatus.EmptyContent, "Decoded JSON was empty.");
        }

        UmaApiResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<UmaApiResponse>(json, UmaCaptureJson.Options);
        }
        catch (Exception ex)
        {
            return Failed(fileName, ImportStatus.InvalidFormat, ex.Message);
        }

        if (response == null)
        {
            return Failed(fileName, ImportStatus.InvalidFormat, "Deserializer returned null.");
        }

        string typeName = response.GetType().Name;
        if (response is FallbackUmaApiResponse)
        {
            return new CaptureImportResult
            {
                FileName = fileName,
                Status = ImportStatus.UnsupportedType,
                Response = response,
                ResponseTypeName = typeName,
                ErrorMessage = "Unrecognized API response shape.",
            };
        }

        return new CaptureImportResult
        {
            FileName = fileName,
            Status = ImportStatus.Success,
            Response = response,
            ResponseTypeName = typeName,
        };
    }

    private static CaptureImportResult UnsupportedExtension(string fileName, string extension) =>
        Failed(fileName, ImportStatus.UnsupportedExtension, $"Unsupported file extension: {extension}");

    private static CaptureImportResult Failed(string fileName, ImportStatus status, string? message) =>
        new()
        {
            FileName = fileName,
            Status = status,
            ErrorMessage = message,
        };
}