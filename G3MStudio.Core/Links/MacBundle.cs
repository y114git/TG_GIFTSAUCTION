using System.Diagnostics;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Links;

/// <summary>
/// Declares the g3mstudio:// scheme and the data file types in the Info.plist of the app bundle the program runs from, so macOS lists G3MStudio under
/// "Open With". The types use the Alternate rank: the app is offered but never takes a default from another program.
/// shortcut: the plist is edited as text, once (a marker key says it was done); a bundle that is read-only or signed is left alone.
/// </summary>
public static class MacBundle
{
    private const string Marker = "G3MStudioDeclared";

    /// <summary>The Info.plist of the bundle that holds <paramref name="exePath"/> (Contents/MacOS/name), or null outside a bundle.</summary>
    public static string? PlistOf(string exePath) =>
        Path.GetDirectoryName(Path.GetDirectoryName(exePath)) is { } contents && Path.GetFileName(Path.GetDirectoryName(exePath)) == "MacOS" && Path.GetFileName(contents) == "Contents" ? Path.Combine(contents, "Info.plist") : null;

    /// <summary>The plist text with the declarations added before its last closing dict; the text itself when it already has them, has its own types, or is not a plist.</summary>
    public static string Merge(string plist)
    {
        var end = plist.LastIndexOf("</dict>", StringComparison.Ordinal);
        // A plist that has either key of its own is left alone: a second key of the same name would make the file ambiguous.
        if (end < 0 || plist.Contains(Marker, StringComparison.Ordinal) || plist.Contains("<key>CFBundleURLTypes</key>", StringComparison.Ordinal) || plist.Contains("<key>CFBundleDocumentTypes</key>", StringComparison.Ordinal)) return plist;
        var types = string.Concat(FileAssociations.Extensions.Select(e => $"<string>{e[1..]}</string>"));
        var added = $"""
              <key>{Marker}</key>
              <true/>
              <key>CFBundleURLTypes</key>
              <array><dict><key>CFBundleURLName</key><string>G3MStudio link</string><key>CFBundleURLSchemes</key><array><string>g3mstudio</string></array></dict></array>
              <key>CFBundleDocumentTypes</key>
              <array><dict><key>CFBundleTypeName</key><string>GameMaker data file</string><key>CFBundleTypeRole</key><string>Editor</string><key>LSHandlerRank</key><string>Alternate</string><key>CFBundleTypeExtensions</key><array>{types}</array></dict></array>

            """;
        return plist.Insert(end, added);
    }

    /// <summary>Writes the declarations into the bundle's Info.plist and asks Launch Services to read it again; false when this is not a bundle, it is signed, or the plist cannot take them.</summary>
    public static bool Declare(string exePath, bool runTools)
    {
        if (PlistOf(exePath) is not { } file || !File.Exists(file)) return false;
        try
        {
            var text = File.ReadAllText(file);
            if (text.Contains(Marker, StringComparison.Ordinal)) return true;
            // Changing Info.plist breaks the signature of a signed bundle, and macOS then refuses to start it.
            if (Directory.Exists(Path.Combine(Path.GetDirectoryName(file) ?? "", "_CodeSignature"))) return false;
            var merged = Merge(text);
            if (merged == text) return false;
            UserData.WriteAllTextAtomic(file, merged);
            if (runTools && Path.GetDirectoryName(Path.GetDirectoryName(file)) is { } bundle)
            {
                using var process = Process.Start(new ProcessStartInfo("/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister", ["-f", bundle]) { UseShellExecute = false });
                process?.WaitForExit(5000);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
