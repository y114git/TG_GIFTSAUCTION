namespace G3MStudio.Core.Links;

/// <summary>
/// Makes G3MStudio the program that opens the GameMaker data file extensions when they are double-clicked, for the current user only (no administrator).
/// It runs only when the user asks, and Remove puts back what Register changed: an extension is given up only when it still points to G3MStudio.
/// shortcut: Windows only. Linux needs MIME types for the extensions and macOS declares document types in the Info.plist of an app bundle; add them with the packaging of each.
/// </summary>
public static class FileAssociations
{
    public const string ProgId = "G3MStudio.DataFile";

    /// <summary>The same set UndertaleModTool associates, so the files a user opened there open here.</summary>
    public static readonly string[] Extensions = [".win", ".unx", ".ios", ".droid", ".3ds", ".symbian"];

    public static bool Supported => OperatingSystem.IsWindows();

    /// <summary>Points each extension at the program. <paramref name="classes"/> is the key under the current user that holds the file classes; tests pass a throwaway one.</summary>
    public static void Register(string exePath, IEnumerable<string> extensions, string classes = @"Software\Classes")
    {
        if (!File.Exists(exePath)) throw new FileNotFoundException("The program to register was not found.", exePath);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("File associations are only set on Windows.");
        using var user = Microsoft.Win32.Registry.CurrentUser;
        using (var progId = user.CreateSubKey($@"{classes}\{ProgId}"))
        {
            progId.SetValue("", "GameMaker data file (G3MStudio)");
            using var command = progId.CreateSubKey(@"shell\open\command");
            command.SetValue("", $"\"{exePath}\" \"%1\"");
        }

        foreach (var extension in extensions.Where(Extensions.Contains))
        {
            using var key = user.CreateSubKey($@"{classes}\{extension}");
            key.SetValue("", ProgId);
        }
    }

    /// <summary>The extensions whose class entry names G3MStudio. A default the user chose in Windows settings overrides it and is not read.</summary>
    public static IReadOnlyList<string> Registered(string classes = @"Software\Classes")
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var user = Microsoft.Win32.Registry.CurrentUser;
        var found = new List<string>();
        foreach (var extension in Extensions)
        {
            using var key = user.OpenSubKey($@"{classes}\{extension}");
            if (key?.GetValue("") as string == ProgId) found.Add(extension);
        }

        return found;
    }

    /// <summary>Gives up the extensions that still point to G3MStudio and removes the program class.</summary>
    public static void Remove(string classes = @"Software\Classes")
    {
        if (!OperatingSystem.IsWindows()) return;
        using var user = Microsoft.Win32.Registry.CurrentUser;
        foreach (var extension in Registered(classes)) user.DeleteSubKeyTree($@"{classes}\{extension}", false);
        user.DeleteSubKeyTree($@"{classes}\{ProgId}", false);
    }
}
