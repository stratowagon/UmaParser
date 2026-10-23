namespace UmaParser.Analysis;

/// <summary>
/// Running-style labels. 1 Front, 2 Pace, 3 Late, 4 End.
/// Roster text and analysis text use different fallbacks for an unknown value.
/// </summary>
public static class RunningStyleNames
{
    public static bool TryGetName(int style, out string name)
    {
        switch (style)
        {
            case 1:
                name = "Front";
                return true;
            case 2:
                name = "Pace";
                return true;
            case 3:
                name = "Late";
                return true;
            case 4:
                name = "End";
                return true;
            default:
                name = string.Empty;
                return false;
        }
    }

    /// <summary>Roster display. Unknown styles are the raw number.</summary>
    public static string FormatRoster(int style) =>
        TryGetName(style, out string name) ? name : style.ToString();

    /// <summary>Analysis rows. Unknown styles stay "#N" so existing reports do not change.</summary>
    public static string FormatAnalysis(int style) =>
        TryGetName(style, out string name) ? name : $"#{style}";
}
