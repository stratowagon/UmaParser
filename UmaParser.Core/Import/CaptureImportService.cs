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
        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(filePath);

        try
        {
            CaptureImportResult result = extension.ToLowerInvariant() switch
            {
                ".json" => TryImport(fileName, jsonText: File.ReadAllText(filePath)),
                ".bin" => TryImport(fileName, binData: File.ReadAllBytes(filePath)),
                _ => UnsupportedExtension(fileName, extension),
            };

            return result.WithFileTimestamp(lastWriteUtc);
        }
        catch (Exception ex)
        {
            return Failed(fileName, ImportStatus.InvalidFormat, ex.Message).WithFileTimestamp(lastWriteUtc);
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

        // First, try the normal Uma API response path (covers Team Trials and headered captures).
        UmaApiResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<UmaApiResponse>(json, UmaCaptureJson.Options);
        }
        catch (Exception ex)
        {
            return Failed(fileName, ImportStatus.InvalidFormat, ex.Message);
        }

        if (response != null && response is not FallbackUmaApiResponse)
        {
            string typeName = response.GetType().Name;
            return new CaptureImportResult
            {
                FileName = fileName,
                Status = ImportStatus.Success,
                Response = response,
                ResponseTypeName = typeName,
            };
        }

        // Not a recognized Uma API / Team Trials shape. Check for single-race capture dumps
        // (Champions Meeting, Room Match, Practice Room etc.). These often use compiler-generated
        // backing field names and contain exactly one race's worth of data + a SimDataBase64 blob.
        bool looksLikeSingleRace =
            json.Contains("SimDataBase64", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("<SimDataBase64>", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("\"RaceType\"", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("<RaceType>", StringComparison.OrdinalIgnoreCase);

        if (looksLikeSingleRace)
        {
            string normalized = CaptureDecoder.NormalizeBackingFieldJson(json);

            // Lightweight extraction of the bits we actually need (the huge history objects in these
            // dumps make a full POCO model painful; we only care about the sim blob + horse roster + conditions).
            string? simBase64 = TryGetSimDataBase64(normalized);
            string? raceType = TryGetSingleRaceType(normalized);

            if (!string.IsNullOrWhiteSpace(simBase64))
            {
                return new CaptureImportResult
                {
                    FileName = fileName,
                    Status = ImportStatus.Success,
                    IsSingleRace = true,
                    SingleRaceNormalizedJson = normalized,
                    SingleRaceSimDataBase64 = simBase64,
                    SingleRaceType = raceType ?? "SingleRace",
                    ResponseTypeName = "SingleRaceCapture",
                };
            }

            // We saw the shape but couldn't find the required sim blob — treat as unsupported for now
            // so the user gets feedback instead of a silent skip.
            return new CaptureImportResult
            {
                FileName = fileName,
                Status = ImportStatus.UnsupportedType,
                SingleRaceNormalizedJson = normalized,
                ErrorMessage = "Single-race capture shape detected but no usable SimDataBase64 / race scenario found.",
                ResponseTypeName = "SingleRaceCapture (no scenario)",
            };
        }

        // Truly unknown / fallback.
        string fallbackType = response?.GetType().Name ?? "unknown";
        return new CaptureImportResult
        {
            FileName = fileName,
            Status = ImportStatus.UnsupportedType,
            Response = response,
            ResponseTypeName = fallbackType,
            ErrorMessage = "Unrecognized API response shape.",
        };
    }

    private static string? TryGetSimDataBase64(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            // After normalization the property is "SimDataBase64" (or still the original if normalize didn't touch it).
            if (TryGetStringPropertyRecursive(doc.RootElement, "SimDataBase64", out string? val) && !string.IsNullOrWhiteSpace(val))
                return val;

            // Fallback: the TT-style name in case a future capture mixes shapes
            if (TryGetStringPropertyRecursive(doc.RootElement, "race_scenario", out string? val2) && !string.IsNullOrWhiteSpace(val2))
                return val2;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetSingleRaceType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (TryGetStringPropertyRecursive(doc.RootElement, "RaceType", out string? t) && !string.IsNullOrWhiteSpace(t))
                return t;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetStringPropertyRecursive(JsonElement element, string name, out string? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        value = prop.Value.GetString();
                        return true;
                    }
                }

                if (TryGetStringPropertyRecursive(prop.Value, name, out value))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryGetStringPropertyRecursive(item, name, out value))
                    return true;
            }
        }
        return false;
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