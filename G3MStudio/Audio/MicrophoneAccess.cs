using Microsoft.Win32;

namespace G3MStudio.Audio;

/// <summary>
/// Whether Windows privacy settings block this program from the microphone. With the setting "Let desktop apps access your microphone" off, a capture opens fine and delivers only silence, so the
/// recorder reads the setting first and says so. Other systems answer false: they ask the user themselves or refuse the open.
/// </summary>
internal static class MicrophoneAccess
{
    private const string Store = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    public static bool IsDenied()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var program = (Environment.ProcessPath ?? "").Replace('\\', '#');
            string[] keys = [Store, Store + @"\NonPackaged", program.Length == 0 ? Store : Store + @"\NonPackaged\" + program];
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                foreach (var key in keys)
                {
                    using var sub = hive.OpenSubKey(key);
                    if (sub?.GetValue("Value") is string value && value.Equals("Deny", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }
        catch (Exception e) when (e is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
