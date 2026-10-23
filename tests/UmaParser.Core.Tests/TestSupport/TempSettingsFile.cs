namespace UmaParser.Core.Tests.TestSupport;

/// <summary>A settings.json that is not the real AppData file.</summary>
internal sealed class TempSettingsFile : IDisposable
{
    public TempSettingsFile()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "umaparser-settings-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(DirectoryPath);
        FilePath = Path.Combine(DirectoryPath, "settings.json");
    }

    public string DirectoryPath { get; }

    public string FilePath { get; }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
