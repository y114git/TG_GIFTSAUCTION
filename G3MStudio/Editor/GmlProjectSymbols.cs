using Avalonia.Threading;
using G3MLib.Analyzer.Compiler;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>A name one code entry declares for the whole game: a macro, an enum, a globalvar or a variable made by variable_instance_set and the like.</summary>
/// <param name="Name">The declared name.</param>
/// <param name="Kind">macro, enum, global or variable.</param>
/// <param name="Entry">The code entry that declares it.</param>
/// <param name="Preview">The line that declares it.</param>
public sealed record ProjectSymbol(string Name, string Kind, string Entry, string Preview);

/// <summary>
/// The macros, enums and dynamically made names of every code entry, so code that uses one declared elsewhere is not told the name is unknown.
/// Built on a worker the first time it is asked for: each entry is decompiled once (macros come from the text last applied to an entry, since
/// compiling inlines them). An edit drops only the entries it touched and the index is built again from the rest; until then the last index answers.
/// Every member is safe to call from any thread.
/// </summary>
public static class GmlProjectSymbols
{
    private sealed class Index
    {
        public readonly object Gate = new();
        public readonly Dictionary<string, ProjectSymbol[]> ByEntry = new(StringComparer.Ordinal);
        public readonly HashSet<string> Touched = new(StringComparer.Ordinal); // entries edited while the current build runs
        public volatile Dictionary<string, ProjectSymbol[]>? All;
        public bool Stale; // All misses edits made since it was built
        public bool TouchedAll;
        public Task? Building;
        public int Version;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, Index> Indexes = [];

    // Called before an entry is decompiled for the index, so a test can hold a build in the middle.
    internal static Action<Workspace, string>? Decompiling;

    /// <summary>Raised on the UI thread when a build of the index is done, so code checked while it was missing or out of date can be checked again.</summary>
    public static event Action<Workspace>? Ready;

    /// <summary>Where the name is declared outside the entry, or null (also before the first build is done). While the index is built again after an edit, the last one answers.</summary>
    public static ProjectSymbol? Find(Workspace workspace, string name, string? except = null)
    {
        var all = AllOf(workspace);
        return all is not null && all.TryGetValue(name, out var found) ? found.FirstOrDefault(s => s.Entry != except) : null;
    }

    /// <summary>Whether an index exists, so that <see cref="Find"/> returning null means the name is declared nowhere else. Starts the build when there is none.</summary>
    public static bool IsBuilt(Workspace workspace) => AllOf(workspace) is not null;

    /// <summary>Waits for an index that has every edit, for tests and for callers that need the full answer.</summary>
    public static async Task ReadyAsync(Workspace workspace)
    {
        while (true)
        {
            var index = IndexOf(workspace);
            Task? building;
            lock (index.Gate)
            {
                if (index.All is not null && !index.Stale) return;
                building = index.Building;
            }

            if (building is null) AllOf(workspace);
            else await building.ConfigureAwait(false);
        }
    }

    private static Dictionary<string, ProjectSymbol[]>? AllOf(Workspace workspace)
    {
        var index = IndexOf(workspace);
        lock (index.Gate)
        {
            if (index.All is { } ready && !index.Stale) return ready;
            if (index.Building is null) Start(workspace, index);
            return index.All;
        }
    }

    // Starts a build; the caller holds the gate.
    private static void Start(Workspace workspace, Index index)
    {
        var version = index.Version;
        index.Touched.Clear();
        index.TouchedAll = false;
        index.Building = Task.Run(() =>
        {
            bool? current = null;
            try
            {
                current = Build(workspace, index, version);
            }
            catch (Exception e) when (e is not OutOfMemoryException && FailureReport.Seen(e, "gml.symbols"))
            {
                throw; // never reached: the filter reports and answers false
            }
            finally
            {
                // A failed build must not block every later one; one that an edit overtook starts again and reads only what that edit touched.
                lock (index.Gate)
                {
                    index.Building = null;
                    if (current == false) Start(workspace, index);
                }
            }

            if (current == true) Dispatcher.UIThread.Post(() => Ready?.Invoke(workspace));
        });
    }

    private static Index IndexOf(Workspace workspace) => Indexes.GetValue(workspace, w =>
    {
        var index = new Index();
        w.History.Changed += () =>
        {
            var history = w.History;
            // A save changes no code: the entries already read stay.
            if (history.Last is null && !history.IsDirty) return;
            lock (index.Gate)
            {
                index.Version++;
                index.Stale = true;
                var touches = history.Last?.Touches;
                if (touches is null || touches.Any(t => t.Type == "CodeEntries" && t.Name is null))
                {
                    index.ByEntry.Clear();
                    index.TouchedAll = true;
                    return;
                }

                foreach (var entry in touches.Where(t => t.Type == "CodeEntries").Select(t => t.Name).OfType<string>())
                {
                    index.ByEntry.Remove(entry);
                    index.Touched.Add(entry);
                }
            }
        };
        return index;
    });

    // Reads the entries not in the index yet. False when an edit came in meanwhile: what it did not touch is kept, and nothing is published.
    private static bool Build(Workspace workspace, Index index, int version)
    {
        string[] entries;
        Dictionary<string, string> applied;
        G3MLib.DataFile.Decompiler.GlobalDecompileContext context;
        using (workspace.Lock.Read())
        {
            entries = [.. workspace.Data.Code.Where(c => c?.ParentEntry is null).Select(c => c?.Name?.Content).OfType<string>()];
            context = workspace.DecompileContext();
            // The text the user last applied to an entry still has its macros; the decompiled one has them inlined.
            applied = new Dictionary<string, string>(StringComparer.Ordinal);
            var history = workspace.History;
            for (var i = 0; i < history.Position && i < history.Items.Count; i++)
                if (history.Items[i] is EditCodeCommand code && code.Args.TryGetValue("entry", out var entry) && code.Args.TryGetValue("text", out var text)) applied[entry] = text;
        }

        string[] missing;
        lock (index.Gate) missing = [.. entries.Where(e => !index.ByEntry.ContainsKey(e))];
        var read = new System.Collections.Concurrent.ConcurrentDictionary<string, ProjectSymbol[]>(StringComparer.Ordinal);
        Parallel.ForEach(missing, entry =>
        {
            try
            {
                if (!applied.TryGetValue(entry, out var text))
                {
                    Decompiling?.Invoke(workspace, entry);
                    using (workspace.Lock.Read()) text = CodeText.Decompile(workspace.Data, entry, context);
                }

                read[entry] = Extract(entry, text);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                read[entry] = []; // an entry the decompiler gives up on declares nothing that can be read
            }
        });

        lock (index.Gate)
        {
            if (!index.TouchedAll)
                foreach (var (entry, symbols) in read)
                    if (!index.Touched.Contains(entry)) index.ByEntry[entry] = symbols;
            if (index.Version != version) return false;
            index.All = index.ByEntry.Values.SelectMany(s => s).GroupBy(s => s.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            index.Stale = false;
            return true;
        }
    }

    /// <summary>Only declarations: #macro and enum names, globalvar names, and names set by a string literal through the variable_*_set calls.</summary>
    public static ProjectSymbol[] Extract(string entry, string text)
    {
        // Most entries declare nothing: skip the tokenizer for them.
        if (!text.Contains("#macro", StringComparison.Ordinal) && !text.Contains("enum", StringComparison.Ordinal) && !text.Contains("globalvar", StringComparison.Ordinal) && !text.Contains("_set", StringComparison.Ordinal)) return [];
        var doc = GmlDoc.Parse(text);
        var found = new List<ProjectSymbol>();
        var tokens = doc.Tokens;
        string Line(int token)
        {
            var start = text.LastIndexOf('\n', Math.Max(0, doc.Start(token) - 1)) + 1;
            var end = text.IndexOf('\n', doc.Start(token));
            var line = text[start..(end < 0 ? text.Length : end)].Trim();
            return line.Length > 160 ? line[..160] + " ..." : line;
        }

        for (var i = 0; i + 1 < tokens.Count; i++)
        {
            var word = tokens[i].Kind is GmlTokenKind.Keyword or GmlTokenKind.Preprocessor or GmlTokenKind.Identifier ? doc.Slice(i) : "";
            if (word == "#macro")
            {
                var name = i + 3 < tokens.Count && doc.IsPunct(i + 2, ':') ? i + 3 : i + 1;
                if (tokens[name].Kind == GmlTokenKind.Identifier) found.Add(new ProjectSymbol(doc.Slice(name), "macro", entry, Line(i)));
            }
            else if (word == "enum" && tokens[i + 1].Kind == GmlTokenKind.Identifier) found.Add(new ProjectSymbol(doc.Slice(i + 1), "enum", entry, Line(i)));
            else if (word == "globalvar")
            {
                for (var j = i + 1; j < tokens.Count && !doc.IsPunct(j, ';') && GmlHover.LineOf(doc, j) == GmlHover.LineOf(doc, i); j++)
                    if (tokens[j].Kind == GmlTokenKind.Identifier) found.Add(new ProjectSymbol(doc.Slice(j), "global", entry, Line(i)));
            }
            else if (word is "variable_instance_set" or "variable_struct_set" or "struct_set" or "variable_global_set" && doc.IsPunct(i + 1, '('))
            {
                // The name is the first argument of variable_global_set and the second of the others, when it is a plain string.
                var wanted = word == "variable_global_set" ? 0 : 1;
                var argument = 0;
                var close = doc.PairOf(i + 1);
                for (var j = i + 2; j < close; j++)
                {
                    if (tokens[j].Kind == GmlTokenKind.Separator && doc.Text[doc.Start(j)] is '(' or '[' or '{' && doc.PairOf(j) is var pair and >= 0) { j = pair; continue; }
                    if (doc.IsPunct(j, ',')) { argument++; continue; }
                    if (argument == wanted && tokens[j].Kind == GmlTokenKind.String && (doc.IsPunct(j + 1, ',') || j + 1 == close) && doc.Slice(j) is { Length: > 2 } literal && GmlSymbols.IsValidName(literal[1..^1]))
                        found.Add(new ProjectSymbol(literal[1..^1], word == "variable_global_set" ? "global" : "variable", entry, Line(i)));
                    if (argument > wanted) break;
                }
            }
        }

        return [.. found];
    }
}
