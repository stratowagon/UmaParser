namespace UmaParser.Presentation;

/// <summary>
/// The <c>ColorMode</c> value in the shared settings file.
/// <see cref="System"/> follows the Windows app theme. Light and Dark are fixed.
/// </summary>
public enum ColorMode
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>
/// Reads and writes the <c>ColorMode</c> string. An unsaved or unknown value is
/// <see cref="ColorMode.System"/>, so a newer word does not get forced to Light or Dark.
/// </summary>
public static class ColorModeSetting
{
    public const string System = "System";

    public const string Light = "Light";

    public const string Dark = "Dark";

    public static ColorMode Parse(string? saved) => saved switch
    {
        Light => ColorMode.Light,
        Dark => ColorMode.Dark,
        _ => ColorMode.System,
    };

    public static string Format(ColorMode mode) => mode switch
    {
        ColorMode.Light => Light,
        ColorMode.Dark => Dark,
        _ => System,
    };
}

/// <summary>
/// The Windows app theme, read from
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme</c>.
/// Null means the value is missing. The read is read-only.
/// </summary>
public interface IWindowsAppThemeReader
{
    int? AppsUseLightTheme { get; }
}

/// <summary>
/// Turns a saved <see cref="ColorMode"/> into the light or dark appearance.
/// A missing AppsUseLightTheme value is light, matching Windows.
/// </summary>
public static class SystemColorTheme
{
    public static bool IsLight(int? appsUseLightTheme) => appsUseLightTheme is not 0;

    public static ColorTheme Resolve(ColorMode mode, IWindowsAppThemeReader windows) => mode switch
    {
        ColorMode.Dark => ColorTheme.Dark,
        ColorMode.Light => ColorTheme.Light,
        _ => IsLight(windows.AppsUseLightTheme) ? ColorTheme.Light : ColorTheme.Dark,
    };
}
