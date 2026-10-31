using System.Diagnostics;

namespace G3MStudio.Core.Links;

/// <summary>
/// Registers the g3mstudio:// scheme for the current user, so other programs can open files in G3MStudio through a link. It changes only the user's own
/// settings (the current user part of the registry on Windows, a .desktop file under ~/.local/share/applications on Linux) and runs only when the user asks.
/// shortcut: macOS is not handled, a scheme there lives in the Info.plist of the app bundle, which a portable zip does not have; add it with the bundle.
/// </summary>
public static class LinkRegistration
{
    public const string RegistryKey = @"Software\Classes\g3mstudio";

    public const string DesktopFile = "g3mstudio-links.desktop";

    public static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    /// <summary>Registers the scheme for the program at <paramref name="exePath"/>. <paramref name="registryKey"/> and <paramref name="applications"/> are for tests; the defaults are the real places.</summary>
    public static void Register(string exePath, string registryKey = RegistryKey, string? applications = null)
    {
        if (!File.Exists(exePath)) throw new FileNotFoundException("The program to register was not found.", exePath);
        if (OperatingSystem.IsWindows())
        {
            WriteWindows(exePath, registryKey);
        }
        else if (OperatingSystem.IsLinux())
        {
            var folder = applications ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, DesktopFile), DesktopEntry(exePath));
            if (applications is null) Run("xdg-mime", "default", DesktopFile, "x-scheme-handler/g3mstudio");
        }
        else
        {
            throw new PlatformNotSupportedException("Registering the link scheme is not supported on this system.");
        }
    }

    /// <summary>The Linux launcher: it receives the whole link as its argument.</summary>
    public static string DesktopEntry(string exePath) =>
        string.Join('\n', "[Desktop Entry]", "Type=Application", "Name=G3MStudio", $"Exec=\"{exePath}\" %u", "NoDisplay=true", "MimeType=x-scheme-handler/g3mstudio;") + "\n";

    private static void WriteWindows(string exePath, string key)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var root = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(key);
        root.SetValue("", "URL:G3MStudio link");
        root.SetValue("URL Protocol", "");
        using var command = root.CreateSubKey(@"shell\open\command");
        command.SetValue("", $"\"{exePath}\" \"%1\"");
    }

    private static void Run(params string[] command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(command[0], command[1..]) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true });
            process?.WaitForExit(5000);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // xdg-mime is not installed: the .desktop file is written and the desktop picks it up when it can.
        }
    }
}
