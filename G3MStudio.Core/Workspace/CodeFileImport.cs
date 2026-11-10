using System.Text;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <param name="Path">The file.</param>
/// <param name="Entry">The code entry whose name is the file name without its extension.</param>
/// <param name="Assembly">The file is VM assembly (.asm), not GML.</param>
/// <param name="Create">No entry has the name yet: the import adds an empty one first.</param>
public sealed record CodeFileMatch(string Path, string Entry, bool Assembly, bool Create = false);

/// <summary>A file the import could not read or compile, and why.</summary>
public sealed record CodeFileFailure(string File, string Reason);

/// <param name="Matches">Files that name an existing top-level code entry.</param>
/// <param name="Misses">Files that name no such entry; they are not imported.</param>
public sealed record CodeFilePlan(IReadOnlyList<CodeFileMatch> Matches, IReadOnlyList<string> Misses);

/// <summary>Import GML (UTMT ImportGML): .gml and .asm files replace the code of the existing entries they are named after.</summary>
public static class CodeFileImport
{
    /// <summary>The .gml and .asm files among the paths; a folder gives the ones inside it, in every sub folder.</summary>
    public static IReadOnlyList<string> Collect(IEnumerable<string> paths) =>
    [
        .. paths.SelectMany(p => Directory.Exists(p) ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories) : [p])
            .Where(f => Path.GetExtension(f).Equals(".gml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(f).Equals(".asm", StringComparison.OrdinalIgnoreCase))
            .Distinct(PathComparer).OrderBy(f => f, PathComparer),
    ];

    /// <summary>The file system's own rule: Windows paths ignore case, Linux and macOS paths are compared exactly (two files that differ only in case are two files).</summary>
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>The largest file Import GML reads. A code entry this big is not source text a person wrote.</summary>
    public const long MaxFileBytes = 8 * 1024 * 1024;

    /// <summary>Sorts the files into matches and misses by entry name. Hold the model read lock when called off the UI thread.</summary>
    public static CodeFilePlan Plan(GameMakerData data, IEnumerable<string> files, bool createMissing = false)
    {
        var top = data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var matches = new List<CodeFileMatch>();
        var misses = new List<string>();
        foreach (var file in files)
        {
            var entry = Path.GetFileNameWithoutExtension(file);
            if (top.Contains(entry)) matches.Add(new CodeFileMatch(file, entry, Path.GetExtension(file).Equals(".asm", StringComparison.OrdinalIgnoreCase)));
            else if (createMissing && ResourceListCommand.IsValidName(entry) && data.Code.ByName(entry) is null) matches.Add(new CodeFileMatch(file, entry, Path.GetExtension(file).Equals(".asm", StringComparison.OrdinalIgnoreCase), true));
            else misses.Add(Path.GetFileName(file));
        }

        return new CodeFilePlan(matches, misses);
    }

    /// <summary>
    /// The one undoable edit that replaces the code of every matched entry. A file with the same entry name twice (a .gml and an .asm) is refused, and so is a file over
    /// <see cref="MaxFileBytes"/> or a binary one. The files are read on a pool thread. Nothing is written when one entry fails to compile or assemble: the group undoes itself.
    /// </summary>
    public static async Task<EditCommand> BuildAsync(IReadOnlyList<CodeFileMatch> matches, CancellationToken cancellationToken = default)
    {
        var (items, failures) = await BuildEachAsync(matches, cancellationToken);
        if (failures.Count > 0) throw new InvalidDataException(failures[0].Reason);
        return new GroupCommand([.. items.Select(i => i.Command)]);
    }

    /// <summary>One edit per file that could be read (a file with no entry gets one added first), and the files that could not: too big, binary or unreadable.</summary>
    public static Task<(IReadOnlyList<(CodeFileMatch Match, EditCommand Command)> Items, IReadOnlyList<CodeFileFailure> Failures)> BuildEachAsync(IReadOnlyList<CodeFileMatch> matches, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var twice = matches.GroupBy(m => m.Entry, StringComparer.Ordinal).FirstOrDefault(g => g.Skip(1).Any());
        if (twice is not null) throw new InvalidOperationException($"More than one file names the entry '{twice.Key}'.");
        var items = new List<(CodeFileMatch, EditCommand)>();
        var failures = new List<CodeFileFailure>();
        foreach (var match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(match.Path);
            try
            {
                if (new FileInfo(match.Path).Length > MaxFileBytes) throw new InvalidDataException($"'{name}' is larger than {MaxFileBytes / (1024 * 1024)} MB, which is too big to import as code.");
                var text = File.ReadAllText(match.Path, Encoding.UTF8);
                if (text.Contains((char)0, StringComparison.Ordinal)) throw new InvalidDataException($"'{name}' is a binary file, not text.");
                EditCommand edit = match.Assembly ? new EditAssemblyCommand(match.Entry, text) : new EditCodeCommand(match.Entry, text);
                items.Add((match, match.Create ? new GroupCommand([new CodeEntryCommand(match.Entry), edit]) : edit));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                failures.Add(new CodeFileFailure(name, e.Message));
            }
        }

        return ((IReadOnlyList<(CodeFileMatch, EditCommand)>)items, (IReadOnlyList<CodeFileFailure>)failures);
    }, cancellationToken);
}
