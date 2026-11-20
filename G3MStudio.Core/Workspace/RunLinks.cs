namespace G3MStudio.Core.Workspace;

/// <summary>Something put next to the game for one run: a link or a copy of an outside file or folder.</summary>
public sealed record LinkedItem(string Path, bool IsDirectory, bool IsLink);

/// <summary>
/// Files and folders a game needs that are not next to the data file (for example the mus folder of DELTARUNE when a chapter is started directly).
/// Each setting line is "source" or "source => name". A relative source is read from the folder of the data file; name is the item's name in the run folder.
/// The items exist only while the game runs. A link is tried first; where links are not allowed the item is copied.
/// </summary>
public static class RunLinks
{
    private static readonly string[] Usual = ["mus", "lang", "audiogroup", "data"];

    public static IReadOnlyList<(string Source, string Name)> Parse(string? text, string dataFolder)
    {
        var result = new List<(string, string)>();
        foreach (var raw in (text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split("=>", 2, StringSplitOptions.TrimEntries);
            if (parts[0].Length == 0 || parts[0].StartsWith('#')) continue;
            var source = Path.GetFullPath(parts[0], dataFolder);
            var name = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (name.Length == 0 || name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal) || name is "." or "..") continue; // the name stays a single entry inside the run folder
            result.Add((source, name));
        }

        return result;
    }

    /// <summary>Puts every listed item in the run folder. Items that already exist there are left alone. Failures are returned as text; the rest still go in.</summary>
    public static (List<LinkedItem> Added, List<string> Failed) Apply(string runFolder, string dataFolder, string? setting)
    {
        var added = new List<LinkedItem>();
        var failed = new List<string>();
        foreach (var (source, name) in Parse(setting, dataFolder))
        {
            var target = Path.Combine(runFolder, name);
            if (Path.Exists(target)) continue;
            var directory = Directory.Exists(source);
            if (!directory && !File.Exists(source))
            {
                failed.Add(source);
                continue;
            }

            try
            {
                var linked = TryLink(source, target, directory);
                if (!linked) Copy(source, target, directory);
                added.Add(new LinkedItem(target, directory, linked));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failed.Add(source + ": " + e.Message);
            }
        }

        return (added, failed);
    }

    /// <summary>Takes the items back. A link only removes itself; a copy made for the run is deleted.</summary>
    public static void Remove(IEnumerable<LinkedItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                if (item.IsDirectory) Directory.Delete(item.Path, !item.IsLink);
                else File.Delete(item.Path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // a game still using it is not our concern; the next run finds it present and leaves it alone
            }
        }
    }

    /// <summary>Usual game folders that sit next to the data file's folder but not in it, so a game started from the chapter folder may not find them. Empty when nothing is missing.</summary>
    public static List<string> Missing(string runFolder)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(runFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return parent is null ? [] : [.. Usual.Where(n => Directory.Exists(Path.Combine(parent, n)) && !Path.Exists(Path.Combine(runFolder, n)))];
    }

    private static bool TryLink(string source, string target, bool directory)
    {
        try
        {
            if (directory) Directory.CreateSymbolicLink(target, source);
            else File.CreateSymbolicLink(target, source);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false; // Windows without developer mode cannot make symbolic links
        }
    }

    private static void Copy(string source, string target, bool directory)
    {
        if (!directory)
        {
            File.Copy(source, target);
            return;
        }

        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to) ?? target);
            File.Copy(file, to);
        }
    }
}
