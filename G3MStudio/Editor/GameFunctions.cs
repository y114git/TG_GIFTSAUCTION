using Avalonia.Threading;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>
/// The functions the game defines (the script functions of the open file) described like the built-in ones: name, parameter names, types and text from /// doc comments.
/// A script is read once, and again only after an edit that touches its code entry.
/// </summary>
public static class GameFunctions
{
    /// <summary>The category name of every game entry; the panel shows its own localized label.</summary>
    public const string Category = "Game";

    // A script is read once on a worker; Value stays null until the read is done. Code is the entry it came from.
    // Required is the fewest arguments a call must pass (null when not certain); Constructor tells a constructor apart.
    private sealed record Result(RefEntry Entry, string? Code, int? Required = null, bool Constructor = false);

    private sealed class Item
    {
        public volatile Result? Value;
    }

    private sealed class Cache
    {
        public readonly Dictionary<string, Item> Items = new(StringComparer.Ordinal);
        public readonly object Gate = new();
        public readonly HashSet<string> Pending = new(StringComparer.Ordinal);
        public IReadOnlyList<RefEntry>? All;

        /// <summary>An edit drops the list at once (names may have changed) and only the entries of the code it touched; an unknown edit drops everything.</summary>
        public void Invalidate(EditCommand? last)
        {
            lock (Gate)
            {
                All = null;
                if (last?.Touches is not { } touches) { Items.Clear(); return; }
                if (touches.Any(t => t.Type == "CodeEntries" && t.Name is null)) { Items.Clear(); return; }
                var changed = touches.Where(t => t.Type == "CodeEntries").Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var name in Items.Where(i => i.Value.Value is not { } done || changed.Contains(done.Code)).Select(i => i.Key).ToList()) Items.Remove(name);
            }
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, Cache> Caches = [];

    /// <summary>The entry of a function declared in the text (function name(a, b) or name = function(a, b)), or null when the text declares none.</summary>
    public static RefEntry? FromDoc(GmlDoc doc, string name, string? where)
    {
        if (GmlSignatures.FromDoc(doc, name) is not { } signature) return null;
        var info = GmlSignatures.DocOf(doc, name);
        var parameters = signature.Parameters.Select((p, i) =>
        {
            var documented = info?.Parameters.FirstOrDefault(x => x.Name == p);
            return new RefParam(p, documented?.Type ?? "", documented?.Meaning ?? "", i >= signature.Required, false);
        });
        return new RefEntry(name, RefKind.Function, Category, info?.Summary ?? "", [.. parameters], info?.Returns ?? "") { Game = true, Where = where };
    }

    // A game function whose parameters are not known: shown as name(...).
    private static RefEntry Unknown(string name, string? where) =>
        new(name, RefKind.Function, Category, "", [new RefParam("", "", "", false, true)], "") { Game = true, Where = where };

    /// <summary>Whether the entry has real parameter names (an unknown one only has the rest marker).</summary>
    public static bool HasParameters(RefEntry entry) => !entry.Parameters.Any(p => p.Name.Length == 0);

    /// <summary>Raised on the UI thread when a worker has read a script, so hints that showed a name-only entry can ask again.</summary>
    public static event Action<Workspace>? Ready;

    /// <summary>
    /// The script function of the open file with that name, or null when the name is not one. The script is decompiled on a worker; until it is read the caller
    /// gets a name-only entry at once and <see cref="Ready"/> tells when the next call finds the result.
    /// </summary>
    public static RefEntry? Find(Workspace workspace, string name)
    {
        if (!GmlNames.IsGameFunction(workspace, name)) return null;
        var cache = CacheOf(workspace);
        var where = GmlNames.Definitions(workspace)(name);
        Item item;
        lock (cache.Gate)
        {
            if (cache.Items.GetValueOrDefault(name) is not { } known)
            {
                cache.Items[name] = item = new Item();
                _ = Task.Run(() =>
                {
                    item.Value = Read(workspace, name, where);
                    lock (cache.Gate) cache.All = null; // the list shows the detail now
                    Dispatcher.UIThread.Post(() => Ready?.Invoke(workspace));
                });
            }
            else item = known;
        }

        return item.Value?.Entry ?? Unknown(name, where);
    }

    /// <summary>
    /// What the code check may judge about a script function: the fewest arguments a call needs and whether it is a constructor. Null until the script
    /// was read; it then asks for the read (on the UI thread), so a later check finds it. Safe to call from a worker.
    /// </summary>
    public static (int? Required, bool Constructor)? Peek(Workspace workspace, string name)
    {
        var cache = CacheOf(workspace);
        lock (cache.Gate)
        {
            if (cache.Items.TryGetValue(name, out var item)) return item.Value is { } done ? (done.Required, done.Constructor) : null;
            // One read is asked for per name, however many calls a check meets before it starts.
            if (!cache.Pending.Add(name)) return null;
        }

        Dispatcher.UIThread.Post(() =>
        {
            lock (cache.Gate) cache.Pending.Remove(name);
            Find(workspace, name);
        });
        return null;
    }

    /// <summary>Every script function of the file as a cheap entry without parameters; <see cref="Find"/> gives the detail.</summary>
    public static IReadOnlyList<RefEntry> All(Workspace workspace)
    {
        var cache = CacheOf(workspace);
        lock (cache.Gate)
        {
            var definitions = GmlNames.Definitions(workspace);
            return cache.All ??= [.. GmlNames.GameFunctionNames(workspace).Select(n => cache.Items.GetValueOrDefault(n)?.Value?.Entry ?? Unknown(n, definitions(n)))];
        }
    }

    /// <summary>The call hint of a game function, or null when its parameters could not be read.</summary>
    public static Signature? SignatureOf(Workspace workspace, string name) =>
        Find(workspace, name) is { } entry && HasParameters(entry) ? Signature.FromReference(entry) : null;

    private static Cache CacheOf(Workspace workspace) => Caches.GetValue(workspace, w =>
    {
        var cache = new Cache();
        w.History.Changed += () =>
        {
            if (w.History.Last is null && !w.History.IsDirty) return; // a save changes no script
            cache.Invalidate(w.History.Last);
        };
        return cache;
    });

    private static Result Read(Workspace workspace, string name, string? where)
    {
        string? code = null;
        try
        {
            string? text = null;
            using (workspace.Lock.Read())
            {
                if (workspace.Data.Scripts.FirstOrDefault(s => s.Name.Content == name)?.Code?.Name.Content is { Length: > 0 } entry)
                {
                    code = entry;
                    text = CodeText.Decompile(workspace.Data, entry, workspace.DecompileContext());
                }
            }

            if (text is not null && GmlDoc.Parse(text) is var doc && FromDoc(doc, name, where) is { } found)
            {
                var declared = GmlLint.Functions(doc).FirstOrDefault(f => f.Name >= 0 && doc.Slice(f.Name) == name);
                return new(found, code, declared is null || declared.Constructor ? null : GmlLint.RequiredArguments(doc, declared), declared?.Constructor == true);
            }
        }
        catch (Exception)
        {
            // A script the decompiler gives up on keeps its name only.
        }

        return new(Unknown(name, where), code);
    }
}
