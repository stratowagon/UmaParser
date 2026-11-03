using Microsoft.Win32;

namespace UmaParser.Presentation;

/// <summary>
/// Reads AppsUseLightTheme once per call and closes the key. 0 is dark. A missing
/// value, or a machine without that key, is reported as null so the caller treats it as light.
/// </summary>
public sealed class RegistryWindowsAppThemeReader : IWindowsAppThemeReader
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public int? AppsUseLightTheme
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return null;
            }

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
                return key?.GetValue("AppsUseLightTheme") as int?;
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or System.IO.IOException
                or ObjectDisposedException)
            {
                return null;
            }
        }
    }
}
