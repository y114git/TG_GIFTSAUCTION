using System.Text.RegularExpressions;

namespace G3MStudio.Core.Workspace;

/// <summary>The single Explorer entry (Game group) that opens the folder view of the loose files next to the data file.</summary>
public sealed class IncludedFilesEntry
{
    public static readonly IncludedFilesEntry Instance = new();

    private IncludedFilesEntry()
    {
    }

    public override string ToString() => ResourceTypes.IncludedFilesName;
}

/// <summary>One loose file below the data file folder. <see cref="Path"/> is relative and uses forward slashes.</summary>
public sealed record IncludedFile(string Path, long Size, DateTime Modified)
{
    public string Name => System.IO.Path.GetFileName(Path);

    public string Kind => System.IO.Path.GetExtension(Path).ToLowerInvariant() switch
    {
        ".ogg" or ".wav" or ".mp3" or ".flac" => "audio",
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" => "images",
        ".json" or ".txt" or ".xml" or ".ini" or ".csv" or ".md" or ".yaml" or ".yml" => "text",
        ".dll" or ".so" or ".dylib" => "libraries",
        _ => "other",
    };
}

/// <summary>The result of <see cref="IncludedFiles.Scan"/>: the files, and whether the scan stopped at the limit.</summary>
public sealed record IncludedScan(IReadOnlyList<IncludedFile> Files, bool Truncated);

/// <summary>
/// The loose files that ship next to a data file (GameMaker Included Files). These are writes to the game folder, not edits of the data file,
/// so they are not on the undo stack; a delete moves the file to the trash folder in the user data, which is the way back. Every path is checked to stay inside the folder.
/// A rename or delete of a file that a sound or the strings refer to by name throws <see cref="IncludedFileUseException"/> unless forced.
/// </summary>
public sealed partial class IncludedFiles(string dataFile, UserData? home = null)
{
    public const int MaxFiles = 10_000;

    public string Root { get; } = Path.GetDirectoryName(Path.GetFullPath(dataFile)) ?? throw new ArgumentException("The data file has no folder.", nameof(dataFile));

    [GeneratedRegex(@"^audiogroup\d+\.dat$", RegexOptions.IgnoreCase)]
    private static partial Regex AudioGroupName();

    // File names are case-sensitive on Linux only.
    private static readonly StringComparison NameCase = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private const string TempSuffix = ".g3mtmp";

    // The data file, its backups ("data.win.bak") and the files Studio writes beside it are not loose game files; "data.winter.txt" is.
    private bool Hidden(string full)
    {
        var name = Path.GetFileName(full);
        var data = Path.GetFileName(dataFile);
        return name.Equals(data, NameCase) || name.StartsWith(data + ".", NameCase) || name.EndsWith(TempSuffix, NameCase) || AudioGroupName().IsMatch(name) || name.Equals("g3mstudio_run.tmp", StringComparison.OrdinalIgnoreCase);
    }

    public IncludedScan Scan(int limit = MaxFiles)
    {
        var files = new List<IncludedFile>();
        var pending = new Stack<string>([Root]);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            try
            {
                foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos())
                {
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue; // a link could lead out of the game folder
                    if (entry is DirectoryInfo) pending.Push(entry.FullName);
                    else if (entry is FileInfo file && !Hidden(file.FullName))
                    {
                        if (files.Count >= limit) return new IncludedScan(Sorted(files), true);
                        files.Add(new IncludedFile(Relative(file.FullName), file.Length, file.LastWriteTime));
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // an unreadable folder is skipped; the rest is still listed
            }
        }

        return new IncludedScan(Sorted(files), false);
    }

    private static List<IncludedFile> Sorted(List<IncludedFile> files) => [.. files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)];

    private string Relative(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');

    /// <summary>The full path of a relative one, refused when it leaves the game folder (UTMT-FILE-14).</summary>
    public string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        // A root folder such as C:\ already ends with the separator.
        var prefix = Path.EndsInDirectorySeparator(Root) ? Root : Root + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, NameCase) && !Hidden(full)
            ? full
            : throw new InvalidOperationException($"'{relative}' is outside the game folder and was not used.");
    }
}

/// <summary>A rename or delete of an included file is refused because a sound or a string names it; the owner may go on.</summary>
public sealed class IncludedFileUseException(bool rename, string message)
    : RiskyRefusalException(rename ? "risky.includedRenameTitle" : "risky.includedDeleteTitle", rename ? "risky.includedRename" : "risky.includedDelete", message);
