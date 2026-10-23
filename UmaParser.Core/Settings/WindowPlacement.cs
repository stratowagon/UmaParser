using UmaParser.MasterData;

namespace UmaParser.Settings;

/// <summary>A rectangle in pixels. <see cref="Right"/> and <see cref="Bottom"/> are exclusive.</summary>
internal readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>One monitor. Bounds pick the screen. WorkingArea is where a restored window must fit.</summary>
internal readonly record struct MonitorArea(ScreenRect Bounds, ScreenRect WorkingArea);

/// <summary>
/// Save and restore the main window. WinForms and WPF both store the rectangle and
/// the Maximized/Normal flag on <see cref="GameMasterSettings"/>.
/// </summary>
internal static class WindowPlacement
{
    public const int MinWidth = 640;
    public const int MinHeight = 480;

    /// <summary>Same string WinForms writes for <c>FormWindowState.Maximized</c>.</summary>
    public const string Maximized = "Maximized";

    /// <summary>Same string WinForms writes for <c>FormWindowState.Normal</c>.</summary>
    public const string Normal = "Normal";

    public static bool TryReadSize(GameMasterSettings settings, out int width, out int height)
    {
        if (settings.WindowWidth is int savedWidth && settings.WindowHeight is int savedHeight
            && savedWidth >= MinWidth && savedHeight >= MinHeight)
        {
            width = savedWidth;
            height = savedHeight;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    public static bool TryReadPosition(GameMasterSettings settings, out int x, out int y)
    {
        if (settings.WindowX is int savedX && settings.WindowY is int savedY)
        {
            x = savedX;
            y = savedY;
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    public static bool IsMaximized(GameMasterSettings settings) =>
        settings.WindowState == Maximized;

    public static void Write(GameMasterSettings settings, int x, int y, int width, int height, bool maximized)
    {
        settings.WindowX = x;
        settings.WindowY = y;
        settings.WindowWidth = width;
        settings.WindowHeight = height;
        settings.WindowState = maximized ? Maximized : Normal;
    }

    /// <summary>
    /// Slide <paramref name="x"/> and <paramref name="y"/> so the window sits inside one work area.
    /// A window larger than the area is pinned to the area's top-left, matching the WinForms clamp.
    /// </summary>
    public static (int X, int Y) ClampToWorkArea(int x, int y, int width, int height, ScreenRect area)
    {
        if (width <= area.Width)
        {
            x = Math.Clamp(x, area.X, area.Right - width);
        }
        else
        {
            x = area.X;
        }

        if (height <= area.Height)
        {
            y = Math.Clamp(y, area.Y, area.Bottom - height);
        }
        else
        {
            y = area.Y;
        }

        return (x, y);
    }

    /// <summary>
    /// Pick the monitor that contains the largest part of the window, or the nearest monitor
    /// when the window misses every screen, then clamp to that monitor's work area.
    /// An empty monitor list leaves the point where it is.
    /// </summary>
    public static (int X, int Y) ClampToVisible(
        int x,
        int y,
        int width,
        int height,
        IReadOnlyList<MonitorArea> monitors)
    {
        if (monitors.Count == 0)
        {
            return (x, y);
        }

        var window = new ScreenRect(x, y, width, height);
        MonitorArea monitor = PickMonitor(window, monitors);
        return ClampToWorkArea(x, y, width, height, monitor.WorkingArea);
    }

    private static MonitorArea PickMonitor(ScreenRect window, IReadOnlyList<MonitorArea> monitors)
    {
        MonitorArea best = monitors[0];
        long bestArea = -1;
        foreach (MonitorArea monitor in monitors)
        {
            long area = IntersectionArea(window, monitor.Bounds);
            if (area > bestArea)
            {
                bestArea = area;
                best = monitor;
            }
        }

        if (bestArea > 0)
        {
            return best;
        }

        MonitorArea nearest = monitors[0];
        long bestDistance = long.MaxValue;
        foreach (MonitorArea monitor in monitors)
        {
            long distance = GapDistanceSquared(window, monitor.Bounds);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = monitor;
            }
        }

        return nearest;
    }

    private static long IntersectionArea(ScreenRect a, ScreenRect b)
    {
        int left = Math.Max(a.X, b.X);
        int top = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.Right, b.Right);
        int bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= left || bottom <= top)
        {
            return 0;
        }

        return (long)(right - left) * (bottom - top);
    }

    private static long GapDistanceSquared(ScreenRect a, ScreenRect b)
    {
        long dx = 0;
        if (a.Right < b.X)
        {
            dx = b.X - a.Right;
        }
        else if (b.Right < a.X)
        {
            dx = a.X - b.Right;
        }

        long dy = 0;
        if (a.Bottom < b.Y)
        {
            dy = b.Y - a.Bottom;
        }
        else if (b.Bottom < a.Y)
        {
            dy = a.Y - b.Bottom;
        }

        return (dx * dx) + (dy * dy);
    }
}
