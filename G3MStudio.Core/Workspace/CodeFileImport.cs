using System.Text;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <param name="Path">The file.</param>
/// <param name="Entry">The code entry whose name is the file name without its extension.</param>
/// <param name="Assembly">The file is VM assembly (.asm), not GML.</param>
public sealed record CodeFileMatch(string Path, string Entry, bool Assembly);

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
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Sorts the files into matches and misses by entry name. Hold the model read lock when called off the UI thread.</summary>
    public static CodeFilePlan Plan(GameMakerData data, IEnumerable<string> files)
    {
        var top = data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var matches = new List<CodeFileMatch>();
        var misses = new List<string>();
        foreach (var file in files)
        {
            var entry = Path.GetFileNameWithoutExtension(file);
            if (top.Contains(entry)) matches.Add(new CodeFileMatch(file, entry, Path.GetExtension(file).Equals(".asm", StringComparison.OrdinalIgnoreCase)));
            else misses.Add(Path.GetFileName(file));
        }

        return new CodeFilePlan(matches, misses);
    }

    /// <summary>
    /// The one undoable edit that replaces the code of every matched entry. A file with the same entry name twice (a .gml and an .asm) is refused.
    /// Nothing is written when one entry fails to compile or assemble: the group undoes itself.
    /// </summary>
    public static EditCommand Build(IReadOnlyList<CodeFileMatch> matches)
    {
        var twice = matches.GroupBy(m => m.Entry, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (twice is not null) throw new InvalidOperationException($"More than one file names the entry '{twice.Key}'.");
        var items = new List<EditCommand>();
        foreach (var match in matches)
        {
            var text = File.ReadAllText(match.Path, Encoding.UTF8);
            if (text.Contains((char)0, StringComparison.Ordinal)) throw new InvalidDataException($"'{Path.GetFileName(match.Path)}' is a binary file, not text.");
            items.Add(match.Assembly ? new EditAssemblyCommand(match.Entry, text) : new EditCodeCommand(match.Entry, text));
        }

        return new GroupCommand(items);
    }
}
