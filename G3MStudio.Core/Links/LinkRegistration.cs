using System.Diagnostics;

namespace G3MStudio.Core.Links;

/// <summary>
/// Registers the g3mstudio:// scheme for the current user on every start, so other programs can open files in G3MStudio through a link. It changes only the
/// user's own settings: the user's file classes on Windows, one .desktop file under ~/.local/share/applications on Linux, the Info.plist of the app bundle on macOS.
/// The same .desktop file also lists G3MStudio under "Open with" for the data file types (<see cref="FileAssociations.OpenWith"/>).
/// </summary>
public static class LinkRegistration
{
    public const string DesktopFile = "g3mstudio.desktop";

    private const string Scheme = "g3mstudio";

    public static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    /// <summary>Registers the scheme for the program at <paramref name="exePath"/>.</summary>
    public static void Register(string exePath, Places places)
    {
        CheckProgram(exePath);
        if (OperatingSystem.IsWindows())
        {
            places.Classes.Set(Scheme, null, "URL:G3MStudio link");
            places.Classes.Set(Scheme, "URL Protocol", "");
            places.Classes.Set($@"{Scheme}\shell\open\command", null, $"\"{exePath}\" \"%1\"");
            places.Classes.Set($@"{Scheme}\DefaultIcon", null, $"\"{exePath}\",0");
        }
        else if (OperatingSystem.IsLinux())
        {
            WriteDesktopFile(exePath, places);
            Run(places, "xdg-mime", "default", DesktopFile, "x-scheme-handler/" + Scheme);
        }
        else if (OperatingSystem.IsMacOS())
        {
            MacBundle.Declare(exePath, places.RunTools);
        }
        else
        {
            throw new PlatformNotSupportedException("Registering the link scheme is not supported on this system.");
        }
    }

    /// <summary>
    /// Refuses a path that must not be registered: a missing file, the dotnet host of a "dotnet G3MStudio.dll" run (its path is dotnet.exe), or a build output
    /// folder that disappears with the next clean build.
    /// </summary>
    internal static void CheckProgram(string exePath)
    {
        if (!File.Exists(exePath)) throw new FileNotFoundException("The program to register was not found.", exePath);
        if (!Path.GetFileNameWithoutExtension(exePath).Equals("G3MStudio", StringComparison.OrdinalIgnoreCase)) throw new IOException($"'{exePath}' is not the G3MStudio program (a run through the dotnet host cannot be registered).");
        if (exePath.Replace('\\', '/').Contains("/bin/Debug/", StringComparison.OrdinalIgnoreCase)) throw new IOException($"'{exePath}' is a debug build output and is not registered.");
    }

    /// <summary>
    /// The automatic registration of every start: the link scheme and the "Open with" entries for the data files. Nothing is written when it is all in place already,
    /// and a place that cannot be written (read-only profile, locked key) is skipped: this is a convenience that must never stop the program.
    /// </summary>
    public static void Ensure(string exePath, Places places)
    {
        try
        {
            if (OperatingSystem.IsLinux() && IsMountedImage(exePath)) return; // an AppImage path vanishes when it exits; a launcher that names it would be dead
            if (IsCurrent(exePath, places)) return;
            Register(exePath, places);
            FileAssociations.OpenWith(exePath, places);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Security.SecurityException)
        {
            // Settings > Files shows the state; the next start tries again.
        }
    }

    /// <summary>True for a program inside an AppImage mount (/tmp/.mount_xxxx/...), which exists only while that run lasts.</summary>
    internal static bool IsMountedImage(string exePath) => exePath.Replace('\\', '/').Split('/').Any(part => part.StartsWith(".mount_", StringComparison.Ordinal));

    /// <summary>True when the scheme already opens the program at <paramref name="exePath"/>, so a start that finds everything in place writes nothing.</summary>
    public static bool IsCurrent(string exePath, Places places)
    {
        if (OperatingSystem.IsWindows())
            return places.Classes.Get($@"{Scheme}\shell\open\command", null) == $"\"{exePath}\" \"%1\"" && places.Classes.Get($@"{FileAssociations.ProgId}\shell\open\command", null) == $"\"{exePath}\" \"%1\""
                && places.Classes.Get(".win\\OpenWithProgids", FileAssociations.ProgId) is not null;
        if (OperatingSystem.IsLinux())
            return File.Exists(Path.Combine(places.Applications, DesktopFile)) && File.ReadAllText(Path.Combine(places.Applications, DesktopFile)) == DesktopEntry(exePath)
                && File.Exists(FileAssociations.MimeFile(places)) && File.Exists(FileAssociations.IconFile(places)); // a start that stopped after the .desktop file tries again
        return false;
    }

    /// <summary>The Linux launcher: it receives a file or the whole link as its argument, and names the scheme and the data file type.</summary>
    public static string DesktopEntry(string exePath) =>
        string.Join('\n', "[Desktop Entry]", "Type=Application", "Name=G3MStudio", $"Exec=\"{ExecQuote(exePath)}\" %U", $"Icon={StudioIcon.Name}", "Categories=Development;",
            $"MimeType=x-scheme-handler/{Scheme};{FileAssociations.MimeType};") + "\n";

    /// <summary>A path inside the quotes of an Exec line: the desktop entry spec escapes " ` $ and \ twice (once for the line, once for the string) and doubles %.</summary>
    internal static string ExecQuote(string path) =>
        path.Replace("\\", "\\\\\\\\", StringComparison.Ordinal).Replace("\"", "\\\\\"", StringComparison.Ordinal).Replace("`", "\\\\`", StringComparison.Ordinal)
            .Replace("$", "\\\\$", StringComparison.Ordinal).Replace("%", "%%", StringComparison.Ordinal);

    internal static void WriteDesktopFile(string exePath, Places places)
    {
        Directory.CreateDirectory(places.Applications);
        File.WriteAllText(Path.Combine(places.Applications, DesktopFile), DesktopEntry(exePath));
        File.Delete(Path.Combine(places.Applications, "g3mstudio-links.desktop")); // the scheme-only launcher of older versions
    }

    internal static void Run(Places places, params string[] command)
    {
        if (!places.RunTools) return;
        try
        {
            Wait(new ProcessStartInfo(command[0], command[1..]), 5000);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // the tool is not installed: the files are written and the desktop picks them up when it can
        }
    }

    /// <summary>Runs the tool with its output discarded (a full pipe would stall it) and ends it with its children when it takes longer than <paramref name="milliseconds"/>. True when it finished in time.</summary>
    internal static bool Wait(ProcessStartInfo start, int milliseconds)
    {
        start.UseShellExecute = false;
        start.RedirectStandardError = start.RedirectStandardOutput = true;
        using var process = Process.Start(start);
        if (process is null) return true;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (process.WaitForExit(milliseconds)) return true;
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // it ended on its own just now
        }

        return false;
    }
}
