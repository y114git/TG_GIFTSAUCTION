using System.Collections.Concurrent;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>One place a name is used in one code entry, found by tokens (so a member of another instance or text in a comment is not a hit).</summary>
/// <param name="Entry">Name of the code entry.</param>
/// <param name="Line">One based line.</param>
/// <param name="Text">The line, trimmed.</param>
/// <param name="InString">True when the hit is a string that equals the name.</param>
public sealed record NameHit(string Entry, int Line, string Text, bool InString);

/// <summary>Reads every top-level code entry once under the read lock and looks for a name in the tokens of its decompiled text.</summary>
public static class CodeScan
{
    public static Task<IReadOnlyList<NameHit>> FindNameAsync(Workspace workspace, string name, CancellationToken cancellationToken = default) =>
        Task.Run(() => FindName(workspace, name, cancellationToken), cancellationToken);

    /// <summary>The same on the calling thread. Never call it on the UI thread: it decompiles every entry.</summary>
    public static IReadOnlyList<NameHit> FindName(Workspace workspace, string name, CancellationToken cancellationToken = default)
    {
        string[] entries;
        var context = default(G3MLib.DataFile.Decompiler.GlobalDecompileContext);
        using (workspace.Lock.Read())
        {
            entries = [.. workspace.Data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>()];
            context = workspace.DecompileContext();
        }

        var hits = new ConcurrentBag<NameHit>();
        Parallel.ForEach(entries, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount }, entry =>
        {
            try
            {
                string text;
                using (workspace.Lock.Read()) text = CodeText.Decompile(workspace.Data, entry, context);
                if (text.Contains(name, StringComparison.Ordinal)) foreach (var hit in InText(entry, text, name)) hits.Add(hit);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
            {
                // An entry the decompiler gives up on has no text to search.
            }
        });
        return [.. hits.OrderBy(h => h.Entry, StringComparer.Ordinal).ThenBy(h => h.Line)];
    }

    /// <summary>The hits of one text: plain uses of the name (and global.name), and string literals that are exactly the name.</summary>
    public static IEnumerable<NameHit> InText(string entry, string text, string name)
    {
        var doc = GmlDoc.Parse(text);
        var lines = text.Split('\n');
        var found = GmlSymbols.References(doc, name).Select(r => (r.Start, InString: false))
            .Concat(GmlSymbols.StringReferences(doc, name).Select(r => (r.Start, InString: true)));
        foreach (var (start, inString) in found.OrderBy(f => f.Start))
        {
            var line = text.AsSpan(0, start).Count('\n');
            yield return new NameHit(entry, line + 1, lines[line].Trim(), inString);
        }
    }
}
