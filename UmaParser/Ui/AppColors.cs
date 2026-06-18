using System.Drawing;

namespace UmaBlobber.Ui;

/// <summary>
/// Centralized semantic color palette that adapts to the selected light/dark mode.
/// 
/// Use these for consistent severity / status backgrounds across tabs (Analysis, Skills, Performance, etc.).
/// Foreground is chosen for good contrast on the corresponding background.
/// </summary>
internal static class AppColors
{
    // ===== Light mode palette (current baseline values, tuned for light backgrounds) =====
    private static readonly Color LightStableBack = Color.FromArgb(46, 125, 50);      // green
    private static readonly Color LightNeedsWorkBack = Color.FromArgb(180, 134, 11);  // amber / olive
    private static readonly Color LightCriticalBack = Color.FromArgb(183, 28, 28);    // red
    private static readonly Color LightStableFore = Color.White;
    private static readonly Color LightNeedsWorkFore = Color.White;
    private static readonly Color LightCriticalFore = Color.White;

    // ===== Dark mode palette (brighter / higher-contrast versions for dark UI) =====
    private static readonly Color DarkStableBack = Color.FromArgb(67, 160, 71);       // slightly brighter green
    private static readonly Color DarkNeedsWorkBack = Color.FromArgb(255, 179, 0);    // vivid amber
    private static readonly Color DarkCriticalBack = Color.FromArgb(229, 57, 53);     // vivid red
    private static readonly Color DarkStableFore = Color.White;
    private static readonly Color DarkNeedsWorkFore = Color.Black;   // amber is light enough for dark text
    private static readonly Color DarkCriticalFore = Color.White;

    public static bool IsDark => AppColorMode.IsDarkMode;

    // ===== Shell / chrome palette =====

    public static Color WindowBack => IsDark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
    public static Color WindowFore => IsDark ? Color.FromArgb(241, 241, 241) : SystemColors.ControlText;
    public static Color MutedFore => IsDark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
    public static Color InputBack => IsDark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
    public static Color InputFore => IsDark ? Color.FromArgb(241, 241, 241) : SystemColors.WindowText;
    public static Color MenuBack => IsDark ? Color.FromArgb(45, 45, 48) : SystemColors.Menu;
    public static Color MenuFore => IsDark ? Color.FromArgb(241, 241, 241) : SystemColors.MenuText;
    public static Color TabInactiveBack => IsDark ? Color.FromArgb(55, 55, 58) : SystemColors.ControlLight;
    public static Color SplitterBack => IsDark ? Color.FromArgb(60, 60, 60) : SystemColors.ControlDark;

    public static Color GridBack => IsDark ? Color.FromArgb(32, 32, 32) : SystemColors.Window;
    public static Color GridFore => IsDark ? Color.FromArgb(241, 241, 241) : SystemColors.WindowText;
    public static Color GridHeaderBack => IsDark ? Color.FromArgb(55, 55, 58) : SystemColors.Control;
    public static Color GridHeaderFore => IsDark ? Color.FromArgb(241, 241, 241) : SystemColors.ControlText;
    public static Color GridLine => IsDark ? Color.FromArgb(70, 70, 74) : SystemColors.ControlDark;
    public static Color GridSelectionBack => IsDark ? Color.FromArgb(38, 79, 120) : SystemColors.Highlight;
    public static Color GridSelectionFore => IsDark ? Color.White : SystemColors.HighlightText;

    // ===== Public semantic colors (backgrounds for severity / status) =====

    public static Color SeverityStableBack => IsDark ? DarkStableBack : LightStableBack;
    public static Color SeverityNeedsWorkBack => IsDark ? DarkNeedsWorkBack : LightNeedsWorkBack;
    public static Color SeverityCriticalBack => IsDark ? DarkCriticalBack : LightCriticalBack;

    public static Color SeverityStableFore => IsDark ? DarkStableFore : LightStableFore;
    public static Color SeverityNeedsWorkFore => IsDark ? DarkNeedsWorkFore : LightNeedsWorkFore;
    public static Color SeverityCriticalFore => IsDark ? DarkCriticalFore : LightCriticalFore;

    /// <summary>
    /// Foreground that works on the given severity background in the current theme.
    /// (Convenience when you already picked the back color.)
    /// </summary>
    public static Color SeverityForeFor(Color backColor)
    {
        if (backColor == SeverityStableBack) return SeverityStableFore;
        if (backColor == SeverityNeedsWorkBack) return SeverityNeedsWorkFore;
        if (backColor == SeverityCriticalBack) return SeverityCriticalFore;

        // Fallback: pick based on luminance of the provided back
        double lum = (0.299 * backColor.R + 0.587 * backColor.G + 0.114 * backColor.B);
        return lum < 140 ? Color.White : Color.Black;
    }

    // ===== Other useful semantic colors (can be extended) =====

    /// <summary>Neutral / default background for "ok / stable" non-severity things.</summary>
    public static Color SuccessBack => SeverityStableBack;

    /// <summary>Warning / attention background.</summary>
    public static Color WarningBack => SeverityNeedsWorkBack;

    /// <summary>Error / critical background.</summary>
    public static Color ErrorBack => SeverityCriticalBack;

    /// <summary>Default foreground for the above success/warning/error backgrounds.</summary>
    public static Color OnSuccessFore => SeverityStableFore;
    public static Color OnWarningFore => SeverityNeedsWorkFore;
    public static Color OnErrorFore => SeverityCriticalFore;

    // ===== Skill rarity / type colors (for Skills tab "Skill" name column) =====
    // White = normal (no special color, default background)
    // Gold = rare skills (~1200 pts)
    // Unique = character unique skills (variable pts, often higher)

    public static Color SkillGoldBack => IsDark ? Color.FromArgb(255, 193, 7) : Color.FromArgb(255, 215, 0);
    public static Color SkillGoldFore => Color.Black;

    public static Color SkillUniqueBack => IsDark ? Color.FromArgb(138, 128, 175) : Color.FromArgb(176, 166, 210);
    public static Color SkillUniqueFore => IsDark ? Color.White : Color.Black;
}
