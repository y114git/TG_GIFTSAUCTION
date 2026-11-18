using G3MStudio.Core.Links;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

/// <summary>Why a remembered data file cannot simply be opened.</summary>
public enum LostReason
{
    /// <summary>It can: present, readable, and the hash is the remembered one (or none was remembered).</summary>
    None,
    Missing,
    /// <summary>It is there but another program holds it or the account may not read it.</summary>
    Unreadable,
    /// <summary>It is there and readable, but its size or hash is not the remembered one: changed outside Studio, another game version, or another file with that name.</summary>
    Changed,
}

/// <summary>A file that may be the lost one. <paramref name="SameHash"/> means same size and MD5 as remembered; otherwise it only has the same name.</summary>
public sealed record Candidate(string Path, bool SameHash);

/// <summary>
/// A data file Studio remembered (the last session, a recent entry) and may no longer find. The checks and the search are here so they can be tested
/// without a window; the dialogs are in the app. Edge cases and how they are handled:
/// missing (search by hash), renamed or moved (found by hash), unreadable (locked, no permission: ask, retry), changed (hash differs: policy),
/// several files with the hash (ask, never guess), read-only (opens as usual, saving reports it), corrupt (the open fails, Home), network drive gone (Try again).
/// </summary>
public static class LostFile
{
    /// <summary>At most this many recent folders and this many sibling folders are searched, so a recent list full of network paths cannot stall a start.</summary>
    public const int MaxFolders = 24;

    // The first file system calls run on a pool thread: a network path that is gone can block them for a long time, and the caller is the UI thread.
    public static Task<LostReason> CheckAsync(string path, long size, string? md5, CancellationToken cancellationToken = default) =>
        Task.Run(() => CheckCoreAsync(path, size, md5, cancellationToken), cancellationToken);

    private static async Task<LostReason> CheckCoreAsync(string path, long size, string? md5, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return LostReason.Missing;
        try
        {
            if (string.IsNullOrEmpty(md5)) return LostReason.None;
            if (size > 0 && new FileInfo(path).Length != size) return LostReason.Changed; // another size is another content: no need to hash the file
            var now = await DataFileIdentity.ComputeAsync(path, cancellationToken);
            return Same(now, size, md5) ? LostReason.None : LostReason.Changed;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return LostReason.Unreadable;
        }
    }

    public static bool Same(DataFileIdentity file, long size, string md5) => (size <= 0 || file.Size == size) && string.Equals(file.Md5, md5, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Files that may be the lost one, same-hash ones first: data files in the old folder, its parent and that parent's folders (a renamed or moved folder
    /// next to it), and the folders of <paramref name="otherFolders"/> (the recent files). A file is hashed only when its size matches, so the search stays cheap.
    /// A file with the old name but another content is listed too, flagged as not the same.
    /// </summary>
    public static async Task<IReadOnlyList<Candidate>> FindAsync(string oldPath, long size, string md5, IEnumerable<string> otherFolders, CancellationToken cancellationToken = default)
    {
        var oldName = Path.GetFileName(oldPath);
        var found = new List<Candidate>();
        var seen = new HashSet<string>(PathComparer);
        foreach (var folder in Folders(oldPath, otherFolders))
        {
            foreach (var file in DataFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(file) || PathComparer.Equals(file, oldPath)) continue;
                var named = string.Equals(Path.GetFileName(file), oldName, StringComparison.OrdinalIgnoreCase);
                try
                {
                    var length = new FileInfo(file).Length;
                    if (md5.Length > 0 && (size <= 0 || length == size) && Same(await DataFileIdentity.ComputeAsync(file, cancellationToken), size, md5)) found.Add(new Candidate(file, true));
                    else if (named) found.Add(new Candidate(file, false));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // a file that cannot be read cannot be the answer
                }
            }
        }

        return [.. found.OrderByDescending(c => c.SameHash).ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase)];
    }

    private static readonly StringComparer PathComparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    /// <summary>The old folder and its parent, then the recent folders, then the parent's other folders; the recent ones and the siblings are capped apart, so neither cuts the other.</summary>
    private static IEnumerable<string> Folders(string oldPath, IEnumerable<string> others)
    {
        var folders = new List<string>();
        var siblings = new List<string>();
        var old = Path.GetDirectoryName(Path.GetFullPath(oldPath));
        if (old is not null)
        {
            folders.Add(old);
            if (Path.GetDirectoryName(old) is { } parent)
            {
                folders.Add(parent);
                try { siblings.AddRange(Directory.EnumerateDirectories(parent)); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* the parent cannot be listed */ }
            }
        }

        var recent = others.Where(o => !string.IsNullOrWhiteSpace(o)).Select(Path.GetFullPath);
        return folders.Concat(recent.Where(Directory.Exists).Take(MaxFolders)).Concat(siblings.Where(Directory.Exists).Take(MaxFolders)).Distinct(PathComparer);
    }

    private static IEnumerable<string> DataFiles(string folder)
    {
        try
        {
            return [.. Directory.EnumerateFiles(folder).Where(f => FileAssociations.Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The single candidate that may be used without asking: exactly one has the remembered hash.</summary>
    public static Candidate? Unique(IReadOnlyList<Candidate> found) => found.Count(c => c.SameHash) == 1 ? found.First(c => c.SameHash) : null;

    /// <summary>How a file that does not match is opened under a policy: true keeps the saved layout, false starts it fresh, null means ask (or refuse).</summary>
    public static bool? KeepLayout(MismatchPolicy policy) => policy switch { MismatchPolicy.KeepLayout => true, MismatchPolicy.FreshLayout => false, _ => null };
}
