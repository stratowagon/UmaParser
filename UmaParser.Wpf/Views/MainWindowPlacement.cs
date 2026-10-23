using System.Windows;
using UmaParser.MasterData;
using UmaParser.Services;
using UmaParser.Settings;

namespace UmaParser.Views;

/// <summary>
/// Reads and writes the same window rectangle WinForms stores in settings.json.
/// A saved position that would sit off every monitor is moved onto the nearest work area.
/// </summary>
internal static class MainWindowPlacement
{
    public static void Restore(Window window)
    {
        GameMasterSettings settings = GameMasterSettings.Load();
        if (!WindowPlacement.TryReadSize(settings, out int width, out int height))
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Width = width;
        window.Height = height;

        if (WindowPlacement.TryReadPosition(settings, out int x, out int y))
        {
            (x, y) = WindowPlacement.ClampToVisible(x, y, width, height, ReadMonitors());
            window.Left = x;
            window.Top = y;
        }

        if (WindowPlacement.IsMaximized(settings))
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    public static void Save(Window window)
    {
        GameMasterSettings settings = GameMasterSettings.Load();
        bool maximized = window.WindowState == WindowState.Maximized;
        Rect bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;
        WindowPlacement.Write(
            settings,
            Round(bounds.X),
            Round(bounds.Y),
            Round(bounds.Width),
            Round(bounds.Height),
            maximized);
        settings.Save();
    }

    private static IReadOnlyList<MonitorArea> ReadMonitors()
    {
        try
        {
            return WindowMonitors.All();
        }
        catch (DllNotFoundException)
        {
            return Array.Empty<MonitorArea>();
        }
        catch (EntryPointNotFoundException)
        {
            return Array.Empty<MonitorArea>();
        }
    }

    private static int Round(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
