using System.Collections.Concurrent;
using G3MLib.DataFile.Decompiler;
using G3MStudio.Core.GmlDocs;

namespace G3MStudio.Core.Workspace;

/// <param name="Numbers">Numbers that were written as asset names.</param>
/// <param name="Entries">Code entries that were changed.</param>
/// <param name="Failed">Entries left as they were: they did not decompile, or the new text did not compile.</param>
public sealed record AssetNumbersResult(int Numbers, int Entries, IReadOnlyList<string> Failed);

/// <summary>Writes asset names instead of asset numbers across all code as one undoable edit (the same way Replace in all code does).</summary>
public static class AssetNumbersApply
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, AssetParameters> Learned = [];

    /// <summary>The tables the editor uses: the learned ones once a run read the whole game, the built-in functions before that.</summary>
    public static AssetParameters ParametersFor(Workspace workspace) => Learned.TryGetValue(workspace, out var found) ? found : Builtin.Value;

    private static readonly Lazy<AssetParameters> Builtin = new(() => AssetParameters.Builtin(GmlReference.Default));

    /// <summary>The tables for one data file. <paramref name="learn"/> adds the game's own functions, which reads every code entry once.</summary>
    public static async Task<AssetParameters> BuildParametersAsync(Workspace workspace, bool learn, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancellationToken = default)
    {
        var parameters = AssetParameters.Builtin(GmlReference.Default);
        if (!learn) return parameters;
        AssetNames names;
        string[] entries;
        GlobalDecompileContext context;
        using (workspace.Lock.Read())
        {
            names = new AssetNames(workspace.Data);
            entries = [.. workspace.Data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>()];
            context = workspace.DecompileContext();
        }

        var done = 0;
        await Parallel.ForEachAsync(entries, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount }, (entry, _) =>
        {
            if (TryDecompile(workspace, entry, context) is { } text) parameters.Learn(text, names);
            var finished = Interlocked.Increment(ref done);
            if (finished % 64 == 0 || finished == entries.Length) progress?.Report((finished, entries.Length * 2));
            return ValueTask.CompletedTask;
        });
        parameters.Seal();
        Learned.AddOrUpdate(workspace, parameters);
        return parameters;
    }

    public static async Task<AssetNumbersResult> RunAsync(Workspace workspace, AssetParameters parameters, Func<string, bool>? allowed, IProgress<(int Done, int Total)>? progress = null, CancellationToken cancellationToken = default)
    {
        AssetNames names;
        string[] entries;
        GlobalDecompileContext context;
        using (workspace.Lock.Read())
        {
            names = new AssetNames(workspace.Data);
            entries = [.. workspace.Data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>()];
            context = workspace.DecompileContext();
        }

        var edits = new ConcurrentBag<(EditCodeCommand Command, string Entry, int Numbers)>();
        var failed = new ConcurrentBag<string>();
        var done = 0;
        await Parallel.ForEachAsync(entries, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount }, (entry, _) =>
        {
            try
            {
                if (TryDecompile(workspace, entry, context) is { } text)
                {
                    var found = AssetNumberReplacer.Find(text, names, parameters, allowed);
                    if (found.Count > 0) edits.Add((new EditCodeCommand(entry, AssetNumberReplacer.Apply(text, found)), entry, found.Count));
                }
                else
                {
                    failed.Add(entry);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failed.Add(entry); // one entry the decompiler chokes on is listed, the others still go in
            }

            var finished = Interlocked.Increment(ref done);
            if (finished % 64 == 0 || finished == entries.Length) progress?.Report((entries.Length + finished, entries.Length * 2));
            return ValueTask.CompletedTask;
        });

        var list = edits.OrderBy(e => e.Entry, StringComparer.Ordinal).ToList();
        // Each edit is tried alone first, so every entry that does not compile is found in one pass and the rest are written as one group.
        if (list.Count > 0)
        {
            foreach (var (command, error) in await workspace.ProbeInBackground([.. list.Select(e => e.Command)]))
            {
                var bad = list.First(e => e.Command == command);
                failed.Add(error is CodeCompileException compile ? compile.Entry : bad.Entry);
                list.Remove(bad);
            }
        }

        if (list.Count > 0) await workspace.EditInBackground(new GroupCommand([.. list.Select(e => e.Command)]));
        return new AssetNumbersResult(list.Sum(e => e.Numbers), list.Count, [.. failed.Distinct().Order(StringComparer.Ordinal)]);
    }

    private static string? TryDecompile(Workspace workspace, string entry, GlobalDecompileContext context)
    {
        try
        {
            using (workspace.Lock.Read()) return CodeText.Decompile(workspace.Data, entry, context);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}
