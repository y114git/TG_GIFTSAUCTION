using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace G3MStudio.Core.Workspace;

/// <param name="Entry">Name of the code entry.</param>
/// <param name="Line">One based line of the decompiled text.</param>
/// <param name="Text">The line, trimmed.</param>
/// <param name="Replaced">The line after the replacement, trimmed; null in a plain search.</param>
public sealed record CodeHit(string Entry, int Line, string Text, string? Replaced = null);

/// <param name="Query">The text or pattern to find.</param>
/// <param name="CaseSensitive">Match upper and lower case apart.</param>
/// <param name="UseRegex">The query is a regular expression.</param>
/// <param name="Sequential">One entry at a time, for low-memory mode: parallel decompiling holds one text per core.</param>
/// <param name="WholeWord">The match must not touch a letter, digit or underscore.</param>
/// <param name="Replacement">Set for a replace preview: the text that takes the place of each match (group references with a regular expression, literal text otherwise).</param>
public sealed record CodeSearchOptions(string Query, bool CaseSensitive = false, bool UseRegex = false, bool Sequential = false, bool WholeWord = false, string? Replacement = null);

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
        var regex = NeedsRegex(options) ? CreateRegex(options) : null;
        // Building the shared context reads every function and variable: not on the caller's thread, which is the UI thread.
        var (entries, context) = await Task.Run(() =>
        {
            using (workspace.Lock.Read())
                return ((string[])[.. workspace.Data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>()], workspace.DecompileContext());
        }, cancellationToken);
        var hits = new ConcurrentBag<CodeHit>();
        int done = 0, failed = 0;
        await Parallel.ForEachAsync(entries, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = options.Sequential ? 1 : Environment.ProcessorCount }, (entry, token) =>
        {
            try
            {
                string text;
                using (workspace.Lock.Read()) text = CodeText.Decompile(workspace.Data, entry, context);
                var lines = text.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                    if (matcher(lines[i]) && ReplaceLine(regex, options, lines[i]) is var replaced && replaced != lines[i]) hits.Add(new CodeHit(entry, i + 1, lines[i].Trim(), replaced?.Trim()));
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
            {
                // The decompiler gives up on some entries; they are counted so the result says how complete it is.
                Interlocked.Increment(ref failed);
            }

            // One report per 64 entries: each report is a job on the UI thread, and 12,000 of them made the window crawl while a search ran.
            var finished = Interlocked.Increment(ref done);
            if (finished % 64 == 0 || finished == entries.Length) progress?.Report((finished, entries.Length));
            return ValueTask.CompletedTask;
        });
        return new CodeSearchResult([.. hits.OrderBy(h => h.Entry, StringComparer.Ordinal).ThenBy(h => h.Line)], entries.Length - failed, failed);
    }

    private static bool NeedsRegex(CodeSearchOptions options) => options.UseRegex || options.WholeWord || options.Replacement is not null;

    /// <summary>The pattern of the options as one regular expression: a plain query is escaped, a whole word match is fenced.</summary>
    public static Regex CreateRegex(CodeSearchOptions options)
    {
        if (options.Query.Length == 0) throw new ArgumentException("The search text is empty.", nameof(options));
        var pattern = options.UseRegex ? options.Query : Regex.Escape(options.Query);
        if (options.WholeWord) pattern = $@"(?<!\w)(?:{pattern})(?!\w)";
        return new Regex(pattern, options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }

    /// <summary>The line with every match replaced; the line itself in a plain search, so a hit that changes nothing can be told from one that does.</summary>
    internal static string? ReplaceLine(Regex? regex, CodeSearchOptions options, string line) =>
        options.Replacement is not { } replacement || regex is null ? null : options.UseRegex ? regex.Replace(line, replacement) : regex.Replace(line, _ => replacement);

    private static Func<string, bool> CreateMatcher(CodeSearchOptions options)
    {
        if (NeedsRegex(options)) return CreateRegex(options).IsMatch;
        if (options.Query.Length == 0) throw new ArgumentException("The search text is empty.", nameof(options));
        var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return line => line.Contains(options.Query, comparison);
    }
}
