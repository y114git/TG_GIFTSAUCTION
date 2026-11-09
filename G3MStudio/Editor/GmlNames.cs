using G3MLib.DataFile.Compiler;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>Tells the colorizer what a GML name stands for, from GameMaker's built-in names and the resources of the open data file.</summary>
public static class GmlNames
{
    /// <summary>The built-in names the compiler accepts for the GameMaker version of the data, plus the functions the data already calls (UTMT-CODE-15).</summary>
    public static Dictionary<string, string> BuiltinFor(G3MLib.DataFile.GameMakerData data)
    {
        var list = new BuiltinList(data);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in list.InstanceVars.Keys.Concat(list.GlobalVars.Keys).Concat(list.GlobalArrayVars.Keys)) names[name] = "Variable";
        foreach (var name in list.Constants.Keys) names[name] = "Constant";
        foreach (var name in list.Functions.Keys) names[name] = "Function";
        return names;
    }

    /// <summary>The names of one data file read in one pass: what each is (for the colors), where it leads (for Go to Definition) and the list to complete from.</summary>
    private sealed record Table(int Version, Dictionary<string, string> Own, Dictionary<string, string> Definitions, List<KeyValuePair<string, string>> All, Dictionary<string, string> Builtin, string GameVersion);

    // One table per workspace, rebuilt when the edit history has moved (any edit, undo or bulk change), so every code tab shares one walk of the file.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, Table> Tables = [];

    private static Table TableOf(Workspace workspace)
    {
        Tables.TryGetValue(workspace, out var known);
        if (known is not null && known.Version == workspace.History.Version) return known;
        var (own, definitions) = (new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));
        string version;
        Dictionary<string, string> builtin;
        using (workspace.Lock.Read())
        {
            var info = workspace.Data.GeneralInfo;
            version = $"{info?.Major}.{info?.Minor}.{info?.Release}.{info?.Build}";
            // shortcut: functions the data starts calling after the first read are not added until the game version changes or the file is reopened.
            builtin = known is not null && known.GameVersion == version ? known.Builtin : BuiltinFor(workspace.Data);
            foreach (var type in ResourceTypes.All.Where(t => !t.IsData))
            {
                foreach (var item in type.Items(workspace.Data) ?? [])
                {
                    if (ResourceTypes.NameOf(item) is not { Length: > 0 } name) continue;
                    own[name] = type.Key == "scripts" ? "Function" : "Asset";
                    definitions[name] = item is G3MLib.DataFile.Models.GameMakerScript { Code: { } scriptCode } && (scriptCode.ParentEntry ?? scriptCode).Name.Content is { Length: > 0 } code ? ResourceTypes.KeyOf("code", code) : ResourceTypes.KeyOf(type.Key, name);
                }
            }
        }

        var table = new Table(workspace.History.Version, own, definitions, [.. builtin.Where(b => !own.ContainsKey(b.Key)).Concat(own)], builtin, version);
        Tables.AddOrUpdate(workspace, table);
        return table;
    }

    /// <summary>A lookup for the names of one data file: scripts are functions, every other resource is an asset, then the built-in names. It follows renames and new resources.</summary>
    public static Func<string, string?> For(Workspace workspace) =>
        name => TableOf(workspace).Own.TryGetValue(name, out var found) ? found : TableOf(workspace).Builtin.GetValueOrDefault(name);

    /// <summary>Every name the editor can complete: the resources of the file, then the built-in names (a resource wins when both have the name).</summary>
    public static Func<IReadOnlyList<KeyValuePair<string, string>>> Completions(Workspace workspace) => () => TableOf(workspace).All;

    /// <summary>Where a name leads: the Explorer key of the resource of the open file that has it (a script leads to its code). Built-in names lead nowhere.</summary>
    public static Func<string, string?> Definitions(Workspace workspace) => name => TableOf(workspace).Definitions.GetValueOrDefault(name);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, Dictionary<string, Signature>> Signatures = [];

    /// <summary>The call hint of a built-in function, from the compiler's list (argument counts). Null for a script of the file.</summary>
    public static Signature? SignatureOf(Workspace workspace, string name)
    {
        if (!Signatures.TryGetValue(workspace, out var table))
        {
            table = [];
            using (workspace.Lock.Read())
                foreach (var f in new BuiltinList(workspace.Data).Functions.Values) table[f.Name] = Signature.FromCounts(f.Name, f.MinArguments, f.MaxArguments);
            // The documented parameter names replace argument0, argument1 ... and cover functions only the data file knows.
            foreach (var entry in Reference.Entries.Where(e => e.Kind == RefKind.Function)) table[entry.Name] = Signature.FromReference(entry);
            Signatures.AddOrUpdate(workspace, table);
        }

        // The cached hint holds the English texts; the documentation is looked up again in the language shown now.
        return table.GetValueOrDefault(name) is { Doc: { } doc } hint ? hint with { Doc = Reference.Find(doc.Name) ?? doc } : table.GetValueOrDefault(name);
    }

    private sealed class ObjectCache
    {
        public int Version = -1;
        public readonly Dictionary<string, IReadOnlyList<string>?> Items = new(StringComparer.Ordinal);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, ObjectCache> ObjectCaches = [];

    /// <summary>The variables the Create code of an object assigns, its parents included, or null when the name is no object of the file. Read once per object and edit step.</summary>
    public static Func<string, IReadOnlyList<string>?> ObjectVariables(Workspace workspace) => name =>
    {
        var cache = ObjectCaches.GetOrCreateValue(workspace);
        lock (cache)
        {
            if (cache.Version != workspace.History.Version)
            {
                cache.Items.Clear();
                cache.Version = workspace.History.Version;
            }

            if (!cache.Items.TryGetValue(name, out var known)) cache.Items[name] = known = ReadObjectVariables(workspace, name);
            return known;
        }
    };

    private static List<string>? ReadObjectVariables(Workspace workspace, string name)
    {
        using var read = workspace.Lock.Read();
        if (workspace.Data.GameObjects.FirstOrDefault(o => o.Name.Content == name) is not { } obj) return null;
        var context = workspace.DecompileContext();
        var names = new SortedSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<G3MLib.DataFile.Models.GameMakerGameObject>();
        for (var o = obj; o is not null && visited.Add(o); o = o.ParentId)
        {
            if (o.Events.Count == 0) continue;
            foreach (var code in o.Events[0].SelectMany(e => e.Actions).Select(a => a.CodeId?.Name?.Content).OfType<string>())
            {
                try
                {
                    foreach (var variable in GmlMembers.AssignedNames(CodeText.Decompile(workspace.Data, code, context))) names.Add(variable);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    // An entry that does not decompile adds nothing.
                }
            }
        }

        return [.. names];
    }

    private static readonly string[] EventWords = ["Create", "Destroy", "Alarm", "Step", "Collision", "Keyboard", "KeyPress", "KeyRelease", "Mouse", "Other", "Draw", "Gesture", "PreCreate", "CleanUp", "Async"];

    /// <summary>The object that owns a code entry named gml_Object_name_Event_n, or null for any other entry.</summary>
    public static string? OwnerObject(string entry, Func<string, IReadOnlyList<string>?> objectVariables)
    {
        const string prefix = "gml_Object_";
        if (!entry.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var rest = entry[prefix.Length..];
        for (var k = rest.IndexOf('_', StringComparison.Ordinal); k > 0; k = rest.IndexOf('_', k + 1))
            if (EventWords.Any(w => rest.AsSpan(k + 1).StartsWith(w, StringComparison.Ordinal)) && objectVariables(rest[..k]) is not null) return rest[..k];
        return null;
    }

    public static GmlReference Reference => GmlReference.Default.For(Loc.Current.Language);

    /// <summary>The name (letters, digits and underscores) that touches the offset, or null when the offset is not on one.</summary>
    public static string? WordAt(string text, int offset)
    {
        static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        if (offset < 0 || offset > text.Length) return null;
        int start = offset, end = offset;
        while (start > 0 && Word(text[start - 1])) start--;
        while (end < text.Length && Word(text[end])) end++;
        return end > start && !char.IsDigit(text[start]) ? text[start..end] : null;
    }
}
