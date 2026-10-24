using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UmaParser.Themes;

/// <summary>
/// Dark or light title bar via DWMWA_USE_IMMERSIVE_DARK_MODE. No extra packages.
/// Attribute 20 is Windows 10 20H1 and later. Earlier Win10 builds fail that call,
/// and 19 is the attribute they accept (since 1809). The call is skipped when the
/// process is not on Windows. When "Show accent color on title bars and window borders"
/// is on, Windows paints the accent color instead of this caption.
/// </summary>
internal static class WindowChromeTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20h1 = 19;

    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            window.SourceInitialized -= OnSourceInitialized;
            window.SourceInitialized += OnSourceInitialized;
            return;
        }

        ApplyHandle(handle, AppTheme.IsDark);
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.SourceInitialized -= OnSourceInitialized;
            Apply(window);
        }
    }

    private static void ApplyHandle(IntPtr handle, bool dark)
    {
        try
        {
            int useDark = dark ? 1 : 0;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
            {
                useDark = dark ? 1 : 0;
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20h1, ref useDark, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);
}
