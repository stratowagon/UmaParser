namespace UmaParser.Presentation;

/// <summary>
/// The appearance actually painted. <see cref="ColorMode.System"/> resolves to one of these
/// from the Windows app theme. Palettes do not take a third value.
/// </summary>
public enum ColorTheme
{
    Light = 0,
    Dark = 1,
}
