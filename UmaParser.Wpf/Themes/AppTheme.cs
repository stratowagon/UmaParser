using System.Windows;
using UmaParser.MasterData;
using UmaParser.Presentation;

namespace UmaParser.Themes;

/// <summary>
/// Swaps the merged color dictionary and saves <c>ColorMode</c> in the shared settings file.
/// The saved value is the preference (System, Light, or Dark). <see cref="Current"/> is the
/// light or dark appearance after System has been resolved. WinForms reads the same file
/// and still restarts to apply it.
/// </summary>
internal static class AppTheme
{
    private static readonly IWindowsAppThemeReader WindowsTheme = new RegistryWindowsAppThemeReader();
    private static bool _applied;

    public static event EventHandler? Changed;

    public static ColorMode Preference { get; private set; } = ColorMode.System;

    public static ColorTheme Current { get; private set; } = ColorTheme.Light;

    public static bool IsDark => Current == ColorTheme.Dark;

    public static void Initialize()
    {
        Preference = ColorModeSetting.Parse(GameMasterSettings.Load().ColorMode);
        ApplyResolved();
        SystemThemeWatcher.Start();
    }

    public static void Stop() => SystemThemeWatcher.Stop();

    /// <summary>View menu. Saves the preference, including System, and repaints when the appearance changes.</summary>
    public static void SetPreference(ColorMode mode)
    {
        if (_applied && Preference == mode)
        {
            return;
        }

        Preference = mode;
        GameMasterSettings settings = GameMasterSettings.Load();
        settings.ColorMode = ColorModeSetting.Format(mode);
        settings.Save();
        ApplyResolved();
    }

    /// <summary>Re-read Windows when the preference is System. Light and Dark stay put.</summary>
    public static void RefreshFromWindows()
    {
        if (Preference != ColorMode.System)
        {
            return;
        }

        ApplyResolved();
    }

    private static void ApplyResolved()
    {
        ColorTheme theme = SystemColorTheme.Resolve(Preference, WindowsTheme);
        if (_applied && theme == Current)
        {
            return;
        }

        Current = theme;
        _applied = true;
        SwapColors(theme);
        CellBandBrushSource.Apply(theme);

        if (Application.Current != null)
        {
            foreach (Window window in Application.Current.Windows)
            {
                WindowChromeTheme.Apply(window);
            }
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void SwapColors(ColorTheme theme)
    {
        if (Application.Current == null)
        {
            return;
        }

        var next = new ResourceDictionary
        {
            Source = new Uri(theme == ColorTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative),
        };
        System.Collections.ObjectModel.Collection<ResourceDictionary> merged = Application.Current.Resources.MergedDictionaries;
        for (int i = 0; i < merged.Count; i++)
        {
            if (merged[i].Contains("Window.Background"))
            {
                merged.RemoveAt(i);
                merged.Insert(i, next);
                return;
            }
        }

        merged.Insert(0, next);
    }
}
