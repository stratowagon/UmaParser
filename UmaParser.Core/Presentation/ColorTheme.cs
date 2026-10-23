namespace UmaParser.Presentation;

/// <summary>
/// Light or dark appearance. The same two values WinForms stores in settings.json.
/// Anything other than <see cref="ColorThemeSetting.Dark"/> is light, including null and "System".
/// </summary>
public enum ColorTheme
{
    Light = 0,
    Dark = 1,
}

/// <summary>
/// The <c>ColorMode</c> string in the shared settings file. WinForms writes the same words.
/// </summary>
public static class ColorThemeSetting
{
    public const string Light = "Light";

    public const string Dark = "Dark";

    public static ColorTheme Parse(string? saved) =>
        saved == Dark ? ColorTheme.Dark : ColorTheme.Light;

    public static string Format(ColorTheme theme) =>
        theme == ColorTheme.Dark ? Dark : Light;
}
