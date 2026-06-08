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

    /// <summary>
    /// Returns true if the effective color mode is dark (including when "System" follows OS dark mode).
    /// </summary>
    public static bool IsDarkMode =>
        Normalize(GameMasterSettings.Load().ColorMode) switch
        {
            Dark => true,
            Light => false,
            _ => DetectSystemDarkMode()
        };

    private static bool DetectSystemDarkMode()
    {
        // When following system, inspect a system color's luminance.
        // This works after Application.SetColorMode(SystemColorMode.System) has adjusted the palette.
        var window = SystemColors.Window;
        // Perceived luminance (Rec. 601)
        double luminance = (0.299 * window.R + 0.587 * window.G + 0.114 * window.B);
        return luminance < 128;
    }
}