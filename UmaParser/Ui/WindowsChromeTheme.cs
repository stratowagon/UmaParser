using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace UmaParser.Ui;

/// <summary>
/// Applies OS-level dark/light chrome that WinForms cannot set via BackColor alone
/// (title bar, scrollbars, combo dropdown button, etc.).
/// </summary>
internal static class WindowsChromeTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20h1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? pszSubAppName, string? pszSubIdList);

    public static void ApplyTitleBar(Form form, bool dark)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        void ApplyToHandle()
        {
            if (!form.IsHandleCreated)
            {
                return;
            }

            int useDark = dark ? 1 : 0;
            if (DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeBefore20h1, ref useDark, sizeof(int));
            }
        }

        ApplyToHandle();
        form.HandleCreated += (_, _) => ApplyToHandle();
        form.Shown += (_, _) => ApplyToHandle();
    }

    public static void ApplyNativeControlTheme(Control control, bool dark)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        void ApplyToHandle()
        {
            if (!control.IsHandleCreated)
            {
                return;
            }

            if (dark)
            {
                SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            }
            else
            {
                SetWindowTheme(control.Handle, null, null);
            }
        }

        ApplyToHandle();
        control.HandleCreated += (_, _) => ApplyToHandle();
    }
}