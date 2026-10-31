using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace G3MStudio.Core.Workspace;

/// <param name="Entry">Name of the code entry.</param>
/// <param name="Line">One based line of the decompiled text.</param>
/// <param name="Text">The line, trimmed.</param>
public sealed record CodeHit(string Entry, int Line, string Text);

public sealed record CodeSearchOptions(string Query, bool CaseSensitive = false, bool UseRegex = false);

/// <param name="Hits">Matches, ordered by entry name and line.</param>
/// <param name="Searched">Entries that were decompiled.</param>
/// <param name="Failed">Entries that could not be decompiled and so were not searched.</param>
public sealed record CodeSearchResult(IReadOnlyList<CodeHit> Hits, int Searched, int Failed);

/// <summary>
/// Find in code (UTMT's most used search): decompiles every top-level code entry in parallel under the model's read lock, one entry at a time so
/// edits are never kept waiting, and lists the lines that match. A child entry is part of its parent's text.
/// </summary>
public static class CodeSearch
{
    public static async Task<CodeSearchResult> RunAsync(Workspace workspace, CodeSearchOptions options, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancellationToken = default)
    {
        var matcher = CreateMatcher(options);
        // Building the shared context reads every function and variable: not on the caller's thread, which is the UI thread.
        var (entries, context) = await Task.Run(() =>
        {
            using (workspace.Lock.Read())
                return ((string[])[.. workspace.Data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>()], workspace.DecompileContext());
        }, cancellationToken);
        var hits = new ConcurrentBag<CodeHit>();
        int done = 0, failed = 0;
        await Parallel.ForEachAsync(entries, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount }, (entry, token) =>
        {
            try
            {
                string text;
                using (workspace.Lock.Read()) text = CodeText.Decompile(workspace.Data, entry, context);
                var lines = text.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                    if (matcher(lines[i])) hits.Add(new CodeHit(entry, i + 1, lines[i].Trim()));
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
            {
                // The decompiler gives up on some entries; they are counted so the result says how complete it is.
                Interlocked.Increment(ref failed);
            }

            progress?.Report((Interlocked.Increment(ref done), entries.Length));
            return ValueTask.CompletedTask;
        });
        return new CodeSearchResult([.. hits.OrderBy(h => h.Entry, StringComparer.Ordinal).ThenBy(h => h.Line)], entries.Length - failed, failed);
    }

    private static Func<string, bool> CreateMatcher(CodeSearchOptions options)
    {
        if (options.Query.Length == 0) throw new ArgumentException("The search text is empty.", nameof(options));
        if (options.UseRegex)
        {
            var regex = new Regex(options.Query, options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            return regex.IsMatch;
        }

        var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return line => line.Contains(options.Query, comparison);
    }
}
