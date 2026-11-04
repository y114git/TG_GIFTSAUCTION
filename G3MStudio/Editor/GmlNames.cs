using G3MLib.DataFile.Compiler;
using G3MStudio.Core.Workspace;

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
