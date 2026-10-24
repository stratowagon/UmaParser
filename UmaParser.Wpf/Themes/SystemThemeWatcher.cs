using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace UmaParser.Themes;

/// <summary>
/// Listens for Windows app-theme changes and asks <see cref="AppTheme"/> to re-read
/// AppsUseLightTheme. The handler is static and is removed on exit so the process
/// is not held by <see cref="SystemEvents.UserPreferenceChanged"/>.
/// </summary>
internal static class SystemThemeWatcher
{
    private static bool _listening;

    public static void Start()
    {
        if (_listening || !OperatingSystem.IsWindows())
        {
            return;
        }

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _listening = true;
    }

    public static void Stop()
    {
        if (!_listening)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _listening = false;
    }

    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
        {
            return;
        }

        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            AppTheme.RefreshFromWindows();
            return;
        }

        _ = dispatcher.InvokeAsync(AppTheme.RefreshFromWindows);
    }
}
