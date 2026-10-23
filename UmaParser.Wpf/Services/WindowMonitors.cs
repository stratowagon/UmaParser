using System.Runtime.InteropServices;
using UmaParser.Settings;

namespace UmaParser.Services;

/// <summary>
/// Monitor rectangles from the same Win32 calls WinForms uses to decide which screen a window is on.
/// </summary>
internal static class WindowMonitors
{
    public static IReadOnlyList<MonitorArea> All()
    {
        var monitors = new List<MonitorArea>();
        bool Callback(IntPtr hMonitor, IntPtr hdc, ref RectNative rect, IntPtr data)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new MonitorArea(ToRect(info.Monitor), ToRect(info.Work)));
            }

            return true;
        }

        bool enumerated = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);

        return enumerated ? monitors : Array.Empty<MonitorArea>();
    }

    private static ScreenRect ToRect(RectNative rect) =>
        new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RectNative rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public RectNative Monitor;
        public RectNative Work;
        public uint Flags;
    }
}
