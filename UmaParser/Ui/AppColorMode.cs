using System.Windows.Forms;
using UmaBlobber.MasterData;

namespace UmaBlobber.Ui;

internal static class AppColorMode
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static string Normalize(string? saved) =>
        saved switch
        {
            Dark => Dark,
            Light => Light,
            _ => System,
        };

    public static SystemColorMode ToSystemColorMode(string? saved) =>
        Normalize(saved) switch
        {
            Dark => SystemColorMode.Dark,
            Light => SystemColorMode.Classic,
            _ => SystemColorMode.System,
        };

    public static void ApplyStartupPreference()
    {
        Application.SetColorMode(ToSystemColorMode(GameMasterSettings.Load().ColorMode));
    }

    public static void SaveAndRestart(string mode)
    {
        var settings = GameMasterSettings.Load();
        settings.ColorMode = Normalize(mode);
        settings.Save();
        Application.Restart();
    }
}