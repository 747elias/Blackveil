using Microsoft.Win32;

namespace Blackveil;

internal static class Startup
{
    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("Blackveil") is string value && !string.IsNullOrWhiteSpace(value); }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (value) key.SetValue("Blackveil", $"\"{Environment.ProcessPath}\" --autostart", RegistryValueKind.String);
            else key.DeleteValue("Blackveil", false);
        }
    }
}
