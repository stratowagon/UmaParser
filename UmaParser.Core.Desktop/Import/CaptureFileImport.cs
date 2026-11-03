namespace UmaParser.Import;

/// <summary>
/// Path-based capture import for a desktop shell. Reads the file, then calls
/// <see cref="CaptureImportService.TryImport"/>. A browser host passes the JSON text
/// to <see cref="CaptureImportService.TryImport"/> and does not use this type.
/// </summary>
public static class CaptureFileImport
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
                ".json" => CaptureImportService.TryImport(fileName, jsonText: File.ReadAllText(filePath)),
                _ => UnsupportedExtension(fileName, extension),
            };

            return result.WithFileTimestamp(lastWriteUtc);
        }
        catch (Exception ex)
        {
            return Failed(fileName, ImportStatus.InvalidFormat, ex.Message).WithFileTimestamp(lastWriteUtc);
        }
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
