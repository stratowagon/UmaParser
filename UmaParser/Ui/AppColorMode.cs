using System.Windows.Forms;
using UmaParser.MasterData;

namespace UmaParser.Ui;

internal static class AppColorMode
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static string Normalize(string? saved) =>
        saved == Dark ? Dark : Light;

    public static void SaveAndRestart(string mode)
    {
        var settings = GameMasterSettings.Load();
        settings.ColorMode = Normalize(mode);
        settings.Save();
        Application.Restart();
    }

    public static bool IsDarkMode =>
        Normalize(GameMasterSettings.Load().ColorMode) == Dark;
}