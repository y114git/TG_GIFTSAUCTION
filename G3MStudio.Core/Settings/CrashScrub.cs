using System.Net;
using System.Text.RegularExpressions;

namespace G3MStudio.Core.Settings;

/// <summary>
/// Makes a crash report text safe to send (F-PRIV-1) while keeping everything that helps to fix the failure. Only the personal parts are replaced:
/// the user's folder name, directory and file names of the user's own files (the extension stays), quoted and GameMaker-prefixed resource names,
/// e-mail addresses, IP addresses, keys and tokens, URLs with credentials, registry paths, and the user and machine name. The rest of the text stays.
/// Limit: a resource name that is neither quoted nor prefixed (spr_, obj_, ...) cannot be recognised and stays.
/// </summary>
public static partial class CrashScrub
{
    public const int MaxLength = 2000;

    // An absolute path: a drive, a UNC share or a slash that does not continue an ASCII word, then everything up to a real end: the end of the line, a quote, a character no
    // path holds (< > | * ?), a colon that ends a sentence or starts ":line 12", or the start of the next path. Spaces and apostrophes inside a name do not end it; only a path
    // that itself opens right after a quote ends at a closing apostrophe (one not followed by a word character).
    [GeneratedRegex(@"(?<![A-Za-z0-9_/\\.:])(?<q>(?<=['’]))?(?:[A-Za-z]:[\\/]|\\\\(?=[^\s\\/""'<>|*?:]+[\\/])|/(?=[^\s\\/""'<>|*?:]+[\\/]))(?:(?!\s(?:[A-Za-z]:[\\/]|\\\\|/[^\s/]+[\\/]))(?:[^\r\n""“”「」『』«»<>|*?:'’]|:(?!\s|$|line\b|\d+(?:[\s:]|$))|(?(q)['’](?=\w)|['’])))*", RegexOptions.CultureInvariant)]
    private static partial Regex AbsolutePath();

    // The last word of a path that ends in a file extension; what follows it is the sentence around the path.
    [GeneratedRegex(@"\.[A-Za-z][A-Za-z0-9]{0,8}(?=[ .,;!)\]]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex Extension();

    // A quoted piece of text is where messages put a resource, entry or file name. The ASCII apostrophe must not sit inside an ASCII word, so "can't" and "isn't" stay;
    // the other quotes cannot be apostrophes, and CJK text has no spaces, so a particle or a verb may touch the closing or opening quote.
    [GeneratedRegex(@"(?:(?<![A-Za-z0-9_])'[^'\r\n]{1,300}'(?![A-Za-z0-9_])|""[^""\r\n]{1,300}""|「[^」\r\n]{1,300}」|『[^』\r\n]{1,300}』|“[^”\r\n]{1,300}”|‘[^’\r\n]{1,300}’|«[^»\r\n]{1,300}»)", RegexOptions.CultureInvariant)]
    private static partial Regex Quoted();

    [GeneratedRegex(@"(?:(?<![A-Za-z0-9_])'|[「『“‘«])[^'」』”’»\r\n]*$", RegexOptions.CultureInvariant)]
    private static partial Regex OpenQuote();

    [GeneratedRegex(@"\b(?:spr|obj|rm|scr|snd|bg|ts|fnt|sh|shd|tl|seq|ps|ac|spt)_[A-Za-z0-9_]+", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixedName();

    [GeneratedRegex(@"\b[A-Za-z][A-Za-z0-9+.\-]*://[^\s/@""']+@[^\s""']*", RegexOptions.CultureInvariant)]
    private static partial Regex UrlWithCredentials();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<![\w.])(?<!Version=)(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?![\w.])", RegexOptions.CultureInvariant)]
    private static partial Regex IPv4();

    [GeneratedRegex(@"(?<![\w:])(?:(?:[0-9A-Fa-f]{1,4}:){7}[0-9A-Fa-f]{1,4}|(?:[0-9A-Fa-f]{1,4}:){1,6}:(?:[0-9A-Fa-f]{1,4}:?){0,6}|::(?:[0-9A-Fa-f]{1,4}:?){1,7})(?![\w:])", RegexOptions.CultureInvariant)]
    private static partial Regex IPv6();

    [GeneratedRegex(@"\bHK(?:EY_[A-Z_]+|LM|CU|CR|U|CC)\\[^\s""'<>|*?]*", RegexOptions.CultureInvariant)]
    private static partial Regex Registry();

    [GeneratedRegex(@"\b(?<k>token|secret|password|passwd|pwd|api[_-]?key|authorization|dsn|sentry_key)(?<s>\s*[:=]\s*)(?:Bearer\s+)?[^\s"",;']+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex NamedSecret();

    [GeneratedRegex(@"\bBearer\s+[A-Za-z0-9._~+/\-]+=*|\b[A-Fa-f0-9]{32,}\b|(?<![\w/\\])[A-Za-z0-9+/_\-]{40,}={0,2}(?![\w/\\])", RegexOptions.CultureInvariant)]
    private static partial Regex LongToken();

    // The type names a framework message quotes.
    [GeneratedRegex(@"^(?:System|Microsoft|Avalonia|SkiaSharp|Sentry|G3MStudio|G3MLib)[\w.`<>\[\],+]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KnownType();

    private static readonly HashSet<string> SystemRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Program Files", "Program Files (x86)", "ProgramData", "usr", "lib", "lib64", "opt", "etc", "proc", "sys", "System", "Applications", "bin", "sbin", "snap", "nix",
    };

    // The one folder after a system root that names no one: kept, everything below it is hidden.
    private static readonly HashSet<string> SystemFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "System32", "SysWOW64", "WinSxS", "dotnet", "share", "lib", "bin", "local", "include", "Frameworks",
    };

    private static readonly HashSet<string> TempRoots = new(StringComparer.OrdinalIgnoreCase) { "tmp", "var", "private" };

    private static readonly HashSet<string> ProfileRoots = new(StringComparer.OrdinalIgnoreCase) { "Users", "home", "Documents and Settings" };

    // Folders every profile has: their names say nothing about the user.
    private static readonly HashSet<string> KnownFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Documents", "Desktop", "Downloads", "AppData", "Local", "Roaming", "LocalLow", "Temp", "Pictures", "Music", "Videos", "OneDrive", "Saved Games", "Library", "Application Support",
        "Caches", ".config", ".local", ".cache", "share", "G3MStudio",
    };

    // File names every GameMaker export has; they name no one's project.
    private static readonly HashSet<string> KnownFiles = new(StringComparer.OrdinalIgnoreCase) { "data.win", "game.droid", "game.ios", "game.unx", "options.ini", "settings.json" };

    /// <summary>The text of an exception for a crash report: the whole message with its personal parts replaced.</summary>
    public static string Exception(string? text, string? home = null) => Clean(text, home);

    /// <summary>Text with the personal parts replaced (see the type summary), cut to <see cref="MaxLength"/>.</summary>
    public static string Clean(string? text, string? home = null) => Strip(text, home, true, MaxLength);

    /// <summary>
    /// The text a user chose to send as feedback: paths, e-mail and IP addresses, keys, the user and machine name and GameMaker-prefixed names are replaced, quoted words stay
    /// (the user wrote them on purpose), cut to 4000 characters.
    /// </summary>
    public static string Feedback(string? text, string? home = null) => Strip(text, home, false, 4000);

    private static string Strip(string? text, string? home, bool quotes, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // the URL and e-mail patterns restart at every letter of a long unbroken run; the report is cut to MaxLength anyway
        if (text.Length > 8000) text = text[..8000];
        text = UrlWithCredentials().Replace(text, "<url>");
        text = Email().Replace(text, "<email>");
        text = Registry().Replace(text, "<registry>");
        text = Text(text, home);
        text = IPv4().Replace(text, "<ip>");
        text = IPv6().Replace(text, m => IPAddress.TryParse(m.Value, out _) ? "<ip>" : m.Value);
        text = NamedSecret().Replace(text, "${k}${s}<secret>");
        text = LongToken().Replace(text, "<secret>");
        text = Machine(text);
        if (quotes) text = Quoted().Replace(text, m => KeepQuoted(m.Value, text.AsSpan(0, m.Index)) ? m.Value : "<name>");
        return Cut(PrefixedName().Replace(text, "<name>"), max);
    }

    // The cut comes last, so a personal part is never half-seen by the patterns. A quote the cut leaves open starts a name that is replaced.
    private static string Cut(string text, int max)
    {
        if (text.Length <= max) return text;
        text = text[..max];
        if (text.Count(c => c == '"') % 2 == 1) text = text[..text.LastIndexOf('"')] + "<name>";
        return OpenQuote().Replace(text, "<name>", 1);
    }

    // A path that is already hidden, a parameter name, or a type name a framework message quotes.
    private static bool KeepQuoted(string quoted, ReadOnlySpan<char> before)
    {
        if (before.EndsWith("Parameter ", StringComparison.OrdinalIgnoreCase)) return true;
        var inner = quoted[1..^1];
        return AbsolutePath().IsMatch(inner) || KnownType().IsMatch(inner);
    }

    // The user name and the machine name can stand anywhere in a text; a very short one would hit ordinary words.
    private static string Machine(string text)
    {
        foreach (var (name, placeholder) in new[] { (Environment.UserName, "<user>"), (Environment.MachineName, "<machine>") })
            if (name.Length >= 3) text = Regex.Replace(text, @"(?<!\w)" + Regex.Escape(name) + @"(?!\w)", placeholder, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return text;
    }

    /// <summary>The absolute paths of a text, hidden by <see cref="HidePath"/>; the rest is untouched.</summary>
    public static string Text(string? text, string? home = null) => string.IsNullOrEmpty(text) ? "" : AbsolutePath().Replace(text, m => HideMatch(m.Value, home));

    // The path ends at its last extension when the last part has one, so the words after a file name stay; trailing punctuation and unmatched closers stay outside.
    private static string HideMatch(string value, string? home)
    {
        var lastSeparator = value.LastIndexOfAny(['/', '\\']);
        var end = value.Length;
        var matches = Extension().Matches(value, lastSeparator);
        if (matches.Count > 0) end = matches[^1].Index + matches[^1].Length;
        else
        {
            while (end > 0 && (value[end - 1] is '.' or ',' or ';' or '!' or ' ')) end--;
            var open = value[..end].Count(c => c == '(') - value[..end].Count(c => c == ')');
            while (end > 0 && (value[end - 1] is ')' or ']') && open < 0) { end--; open++; }
        }
        return HidePath(value[..end], home) + value[end..];
    }

    /// <summary>
    /// A path keeps its shape: the drive or slash, the system folders and the known profile folders, and the extension of the last part. The user's folder name becomes
    /// "&lt;user&gt;", every other folder "&lt;dir&gt;" and the last part "&lt;file&gt;" plus its extension ("&lt;dir&gt;" when it has none). A system folder is kept with at most one known folder below it.
    /// </summary>
    public static string HidePath(string path, string? home = null)
    {
        var backslash = path.Contains('\\', StringComparison.Ordinal) && !path.Contains('/', StringComparison.Ordinal);
        var normal = path.Replace('\\', '/');
        var unc = normal.StartsWith("//", StringComparison.Ordinal);
        var root = normal[..Math.Min(unc ? 2 : normal.StartsWith('/') ? 1 : 3, normal.Length)];
        var parts = normal[root.Length..].Split('/');

        // user is the index of the part that holds the user's folder name; -2 marks a UNC server name at index 0
        var user = -1;
        var profile = false;
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeText = home.Replace('\\', '/').TrimEnd('/');
        if (homeText.Length > root.Length && normal.StartsWith(homeText + "/", StringComparison.OrdinalIgnoreCase))
        {
            user = homeText[root.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).Length - 1;
            profile = true;
        }
        else if (unc) user = -2;
        else if (parts.Length > 1 && ProfileRoots.Contains(parts[0]))
        {
            user = 1;
            profile = true;
        }

        var system = !profile && !unc && SystemRoots.Contains(parts[0]);
        var hide = !profile;
        var shown = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var last = i == parts.Length - 1;
            if (part.Length == 0) shown[i] = part;
            else if (system && (i == 0 || (i == 1 && SystemFolders.Contains(part)))) shown[i] = part;
            else if (i == 0 && user == -2) shown[i] = "<host>";
            else if (i == user) shown[i] = "<user>";
            else if (i < user || (i == 0 && TempRoots.Contains(part))) shown[i] = part;
            else if (profile && !hide && KnownFolders.Contains(part)) shown[i] = part;
            else
            {
                hide = true;
                shown[i] = last ? Leaf(part) : "<dir>";
            }
        }

        var joined = root + string.Join('/', shown);
        return backslash ? joined.Replace('/', '\\') : joined;
    }

    private static string Leaf(string part)
    {
        if (KnownFiles.Contains(part)) return part;
        var extension = Path.GetExtension(part);
        return extension.Length is > 1 and <= 9 && extension.Skip(1).All(char.IsAsciiLetterOrDigit) ? "<file>" + extension : "<dir>";
    }
}
