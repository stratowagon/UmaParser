using System.Windows.Forms;
using UmaParser.MasterData;
using UmaParser.Presentation;

namespace UmaParser.Ui;

internal static class AppColorMode
{
    public const string System = ColorModeSetting.System;
    public const string Light = ColorModeSetting.Light;
    public const string Dark = ColorModeSetting.Dark;

    private static readonly CachedWindowsTheme WindowsTheme = new();

    public static string Normalize(string? saved) =>
        ColorModeSetting.Format(ColorModeSetting.Parse(saved));

    public static void SaveAndRestart(string mode)
    {
        var settings = GameMasterSettings.Load();
        settings.ColorMode = Normalize(mode);
        settings.Save();
        Application.Restart();
    }

    /// <summary>
    /// System is whatever Windows was set to the first time this was read.
    /// WinForms still restarts to apply a change; it does not follow Windows live.
    /// </summary>
    public static bool IsDarkMode =>
        SystemColorTheme.Resolve(ColorModeSetting.Parse(GameMasterSettings.Load().ColorMode), WindowsTheme) == ColorTheme.Dark;

    private sealed class CachedWindowsTheme : IWindowsAppThemeReader
    {
        private int? _appsUseLightTheme;
        private bool _read;

        public int? AppsUseLightTheme
        {
            get
            {
                if (!_read)
                {
                    _appsUseLightTheme = new RegistryWindowsAppThemeReader().AppsUseLightTheme;
                    _read = true;
                }

                return _appsUseLightTheme;
            }
        }
    }
}
