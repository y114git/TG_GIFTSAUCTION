using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace G3MStudio.Core.Settings;

/// <summary>
/// Makes a crash report text safe to send (F-PRIV-1): any absolute path becomes a short hash of the whole path plus its file extension, and the
/// user's home folder in other text becomes "~", so no user name, folder name or game file name leaves the machine.
/// </summary>
public static partial class CrashScrub
{
    [GeneratedRegex(@"(?:[A-Za-z]:[\\/]|\\\\|/)[^\s""<>|*?:]*", RegexOptions.CultureInvariant)]
    private static partial Regex AbsolutePath();

    public static string Text(string? text, string? home = null)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = AbsolutePath().Replace(text, m => Hide(m.Value));
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 ? text.Replace(home, "~", StringComparison.OrdinalIgnoreCase) : text;
    }

    private static string Hide(string path)
    {
        var name = path.Replace('\\', '/').TrimEnd('/');
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8];
        return "<path:" + hash + Path.GetExtension(name) + ">";
    }
}
