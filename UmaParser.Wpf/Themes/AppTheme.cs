using System.Windows;
using UmaParser.MasterData;
using UmaParser.Presentation;

namespace UmaParser.Themes;

/// <summary>
/// Swaps the merged color dictionary and saves <c>ColorMode</c> in the shared settings file.
/// WinForms reads that same value and still restarts to apply it.
/// </summary>
internal static class AppTheme
{
    public static event EventHandler? Changed;

    public static ColorTheme Current { get; private set; } = ColorTheme.Light;

    public static bool IsDark => Current == ColorTheme.Dark;

    public static void Initialize()
    {
        Apply(ColorThemeSetting.Parse(GameMasterSettings.Load().ColorMode), persist: false);
    }

    public static void Apply(ColorTheme theme, bool persist)
    {
        Current = theme;
        SwapColors(theme);
        CellBandBrushSource.Apply(theme);
        if (persist)
        {
            GameMasterSettings settings = GameMasterSettings.Load();
            settings.ColorMode = ColorThemeSetting.Format(theme);
            settings.Save();
        }

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
