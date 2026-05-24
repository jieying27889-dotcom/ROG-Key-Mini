using Microsoft.Win32;
using System.IO;

namespace RogKeyMini.Tray;

public static class AutoStartHelper
{
    private const string KeyName = "RogKeyMini";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            if (key is null) return false;
            var value = key.GetValue(KeyName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key is null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(KeyName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(KeyName, false);
            }
        }
        catch
        {
            // ignore
        }
    }
}
