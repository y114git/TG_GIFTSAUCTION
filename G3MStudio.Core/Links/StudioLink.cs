namespace G3MStudio.Core.Links;

public enum StudioLinkKind
{
    File,

    /// <summary>A web address of a file; it needs the user's confirmation and is not downloaded by this version.</summary>
    Download,

    /// <summary>One of the named actions: open, new-mod, patch-tools.</summary>
    Action,
}

/// <summary>A link of the g3mstudio:// scheme after parsing: the kind and the local path, the address or the action name.</summary>
public sealed record StudioLink(StudioLinkKind Kind, string Target)
{
    public const string Scheme = "g3mstudio://";

    public static readonly string[] Actions = ["open", "new-mod", "patch-tools"];

    /// <summary>
    /// Reads the text of a link as G3M reads its g3m:// links: the scheme is cut off, the text stops at the first comma (a comma inside must be %2C),
    /// surrounding spaces and a trailing slash go, and a broken `https//` is repaired. A file link must name an existing local file (no network or
    /// UNC path). Returns null for anything else, which the caller reports as "Link not recognised."
    /// </summary>
    public static StudioLink? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = text[Scheme.Length..];
        if (rest.IndexOf(',', StringComparison.Ordinal) is var comma and >= 0) rest = rest[..comma];
        rest = rest.Trim().TrimEnd('/');
        if (rest.StartsWith("https//", StringComparison.OrdinalIgnoreCase) || rest.StartsWith("http//", StringComparison.OrdinalIgnoreCase)) rest = rest.Insert(rest.IndexOf("//", StringComparison.Ordinal), ":");
        if (rest.StartsWith("action/", StringComparison.OrdinalIgnoreCase))
        {
            var name = rest["action/".Length..].ToLowerInvariant();
            return Actions.Contains(name) ? new StudioLink(StudioLinkKind.Action, name) : null;
        }

        if (rest.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(rest, UriKind.Absolute, out var web) && web.Host.Length > 0 && web.AbsolutePath.Length > 1)
            return new StudioLink(StudioLinkKind.Download, web.AbsoluteUri);
        if (!rest.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(rest, UriKind.Absolute, out var file) || !file.IsFile) return null;
        if (file.IsUnc && !string.Equals(file.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return null;
        var path = file.LocalPath;
        return File.Exists(path) ? new StudioLink(StudioLinkKind.File, path) : null;
    }

    /// <summary>The link a file is opened with, for other programs to use.</summary>
    public static string ForFile(string path) => Scheme + new Uri(Path.GetFullPath(path)).AbsoluteUri;
}
