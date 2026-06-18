namespace UmaBlobber.Import;

public static class CapturePaths
{
    /// <summary>
    /// Default HorseACT / saved-capture folder for Team Trials JSON files under Documents.
    /// </summary>
    public static string DefaultTeamTrialsSaveFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Saved races",
            "Team Trials");
}