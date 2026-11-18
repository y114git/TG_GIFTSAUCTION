using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Links;

/// <summary>
/// Windows, Linux and macOS integration of the GameMaker data file extensions, for the current user only (no administrator).
/// <see cref="OpenWith"/> makes G3MStudio appear in the "Open with" list of every data file and runs on every start; <see cref="Register"/> also makes it
/// the program that opens them and gives the files its icon, and runs only when the user asks (Getting started, Settings). Remove gives the types back.
/// shortcut: macOS can list the app in "Open With" (Info.plist document types) but a default program there needs the Launch Services API; the user sets it in Finder.
/// </summary>
public static class FileAssociations
{
    public const string ProgId = "G3MStudio.DataFile";

    /// <summary>The value under an extension key that keeps the user's own default program while G3MStudio is the default.</summary>
    private const string PreviousName = "G3MStudio.Previous";

    /// <summary>The data file extensions of the GameMaker targets. UndertaleModTool also claims .3ds, which is the 3D Studio model format and not a data file of G3MLib, so it is left out.</summary>
    public static readonly string[] Extensions = [".win", ".unx", ".ios", ".droid", ".symbian"];

    /// <summary>The Linux MIME type of the data files, defined by <see cref="MimeXml"/>.</summary>
    public const string MimeType = "application/x-gamemaker-data";

    public static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    private static string Open(string exePath) => $"\"{exePath}\" \"%1\"";

    /// <summary>The shared program class (command, run verbs, icon) that both the default and the Open with entries name.</summary>
    private static void WriteProgId(string exePath, Places places)
    {
        var classes = places.Classes;
        classes.Set(ProgId, null, "GameMaker data file (G3MStudio)");
        classes.Set($@"{ProgId}\shell\open\command", null, Open(exePath));
        // The Explorer menu entries that open the file and run the game at once (UTMT-RUN-06); the labels are fixed text in the registry.
        classes.Set($@"{ProgId}\shell\launch", null, "Run game with G3MStudio");
        classes.Set($@"{ProgId}\shell\launch\command", null, $"\"{exePath}\" --run \"%1\"");
        classes.Set($@"{ProgId}\shell\special_launch", null, "Run game with G3MStudio, pick a runtime");
        classes.Set($@"{ProgId}\shell\special_launch\command", null, $"\"{exePath}\" --run-with-runtime \"%1\"");
        classes.Set($@"{ProgId}\DefaultIcon", null, WriteIcon(places));
    }

    private static string WriteIcon(Places places)
    {
        var file = Path.Combine(places.Icons, "g3mstudio.ico");
        Directory.CreateDirectory(places.Icons);
        File.WriteAllBytes(file, StudioIcon.Ico());
        return file;
    }

    /// <summary>
    /// Lists G3MStudio under "Open with" for every data extension without taking the default: Windows OpenWithProgids of those extensions only, a .desktop
    /// file with the MIME type on Linux, document types in the Info.plist of the app bundle on macOS. Safe to run on every start.
    /// </summary>
    public static void OpenWith(string exePath, Places places)
    {
        CheckProgram(exePath);
        if (OperatingSystem.IsWindows())
        {
            WriteProgId(exePath, places);
            // An Applications entry makes Windows offer the program for every file type in the Open with chooser, whatever SupportedTypes lists; versions up to
            // 1.0 wrote one. It is kept (a default chosen through it still opens the file) but hidden from the chooser with NoOpenWith. Only the per-extension
            // OpenWithProgids entries list G3MStudio, and only for the data extensions.
            var legacy = $@"Applications\{Path.GetFileName(exePath)}";
            if (places.Classes.Get($@"{legacy}\shell\open\command", null) is not null) places.Classes.Set(legacy, "NoOpenWith", "");
            foreach (var extension in Extensions) places.Classes.Set($@"{extension}\OpenWithProgids", ProgId, "");

            places.Classes.Changed();
        }
        else if (OperatingSystem.IsLinux())
        {
            LinkRegistration.WriteDesktopFile(exePath, places);
            WriteMime(places);
        }
        else if (OperatingSystem.IsMacOS())
        {
            MacBundle.Declare(exePath, places.RunTools);
        }
    }

    /// <summary>Makes G3MStudio the program that opens the given data extensions and gives them its icon; an extension that is not a data extension is ignored.</summary>
    public static void Register(string exePath, IEnumerable<string> extensions, Places places)
    {
        CheckProgram(exePath);
        var wanted = extensions.Where(Extensions.Contains).ToList();
        OpenWith(exePath, places);
        if (OperatingSystem.IsWindows())
        {
            foreach (var extension in wanted)
            {
                // Remove puts the program the user had back; a second Register must not store G3MStudio as the previous one.
                if (places.Classes.Get(extension, null) is { Length: > 0 } previous && previous != ProgId) places.Classes.Set(extension, PreviousName, previous);
                places.Classes.Set(extension, null, ProgId);
            }

            places.Classes.Changed();
        }
        else if (OperatingSystem.IsLinux() && wanted.Count > 0)
        {
            LinkRegistration.Run(places, "xdg-mime", "default", LinkRegistration.DesktopFile, MimeType);
            SetDefaultLine(places, add: true);
        }
    }

    /// <summary>Register for the Settings page and Getting started: the error text instead of an exception, null when it worked.</summary>
    public static string? TryRegister(string exePath, Places places)
    {
        try
        {
            Register(exePath, Extensions, places);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Security.SecurityException)
        {
            return e.Message;
        }
    }

    /// <summary>Remove for the Settings page and Getting started: the error text instead of an exception, null when it worked.</summary>
    public static string? TryRemove(Places places)
    {
        try
        {
            Remove(places);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Security.SecurityException)
        {
            return e.Message;
        }
    }

    /// <summary>The extensions whose class entry names G3MStudio. A default the user chose in Windows settings overrides it and is not read.</summary>
    public static IReadOnlyList<string> Registered(Places places)
    {
        if (OperatingSystem.IsWindows()) return [.. Extensions.Where(e => places.Classes.Get(e, null) == ProgId)];
        if (OperatingSystem.IsLinux() && File.Exists(MimeApps(places)) && DefaultEntries(File.ReadAllLines(MimeApps(places))) is [DesktopEntryName, ..]) return Extensions;
        return [];
    }

    /// <summary>Gives up the extensions that still point to G3MStudio. The Open with entries stay: they take nothing from other programs.</summary>
    public static void Remove(Places places)
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var extension in Registered(places))
            {
                // Back to the program the user had; with none, the value goes, so the machine-wide default shows again (an empty value would hide it).
                if (places.Classes.Get(extension, PreviousName) is { Length: > 0 } previous) places.Classes.Set(extension, null, previous);
                else places.Classes.DeleteValue(extension, null);
                places.Classes.DeleteValue(extension, PreviousName);
            }

            places.Classes.Changed();
        }
        else if (OperatingSystem.IsLinux())
        {
            SetDefaultLine(places, add: false);
        }
    }

    // ~/.config/mimeapps.list is where xdg-mime keeps the default program of a MIME type, in the [Default Applications] section as a list: the first one opens it.
    private const string DesktopEntryName = LinkRegistration.DesktopFile;

    private const string DefaultSection = "[Default Applications]";

    private static string MimeApps(Places places) => Path.Combine(places.Config, "mimeapps.list");

    /// <summary>The line of <see cref="MimeType"/> inside [Default Applications] (-1 when there is none) and that section's header line (-1 when there is none).</summary>
    private static (int Header, int At) FindDefault(IReadOnlyList<string> lines)
    {
        var (header, at, inside) = (-1, -1, false);
        for (var i = 0; i < lines.Count && at < 0; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('['))
            {
                inside = line == DefaultSection;
                if (inside) header = i;
            }
            else if (inside && line.StartsWith(MimeType + "=", StringComparison.Ordinal))
            {
                at = i;
            }
        }

        return (header, at);
    }

    internal static List<string> DefaultEntries(IReadOnlyList<string> lines) =>
        FindDefault(lines).At is var at and >= 0 ? [.. lines[at].Split('=', 2)[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)] : [];

    /// <summary>Puts G3MStudio first in the list of the data file type (add) or takes it out of the list, leaving every other entry and section as it is. Writes only when the file changes.</summary>
    internal static void SetDefaultLine(Places places, bool add)
    {
        var file = MimeApps(places);
        var old = File.Exists(file) ? File.ReadAllLines(file) : [];
        var lines = old.ToList();
        var (header, at) = FindDefault(lines);
        var entries = DefaultEntries(lines).Where(e => e != DesktopEntryName).ToList();
        if (add) entries.Insert(0, DesktopEntryName);
        var text = entries.Count == 0 ? null : $"{MimeType}={string.Join(';', entries)};";
        if (at >= 0)
        {
            if (text is null) lines.RemoveAt(at);
            else lines[at] = text;
        }
        else if (text is not null && header >= 0) lines.Insert(header + 1, text);
        else if (text is not null) lines.AddRange([DefaultSection, text]);
        if (lines.SequenceEqual(old)) return; // xdg-mime has written the same line already, or there was nothing to take out
        UserData.WriteAllTextAtomic(file, string.Join('\n', lines) + "\n");
    }

    /// <summary>The shared-mime-info file that makes the data extensions one MIME type with the G3MStudio icon.</summary>
    public static string MimeXml()
    {
        List<string> lines =
        [
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<mime-info xmlns=\"http://www.freedesktop.org/standards/shared-mime-info\">",
            $"  <mime-type type=\"{MimeType}\">",
            "    <comment>GameMaker data file</comment>",
            $"    <icon name=\"{StudioIcon.Name}\"/>",
            .. Extensions.Select(e => $"    <glob pattern=\"*{e}\"/>"),
            "  </mime-type>",
            "</mime-info>",
        ];
        return string.Join('\n', lines) + "\n";
    }

    internal static string MimeFile(Places places) => Path.Combine(places.Mime, "packages", "g3mstudio.xml");

    internal static string IconFile(Places places) => Path.Combine(places.Icons, StudioIcon.Name + ".png");

    private static void WriteMime(Places places)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MimeFile(places)) ?? ".");
        File.WriteAllText(MimeFile(places), MimeXml());
        Directory.CreateDirectory(places.Icons);
        File.WriteAllBytes(IconFile(places), StudioIcon.Png());
        LinkRegistration.Run(places, "update-mime-database", places.Mime);
    }

    private static void CheckProgram(string exePath) => LinkRegistration.CheckProgram(exePath);
}
