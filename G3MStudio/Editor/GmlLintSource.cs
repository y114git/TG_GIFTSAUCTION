using G3MLib.DataFile.Compiler;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>The code check context of an open data file: what the game, GameMaker and the reference know. Built once per edit step and shared by every code tab.</summary>
public static class GmlLintSource
{
    private sealed record Cached(int Version, int Nicks, GmlLintContext Context);

    // What GameMaker and the reference know: fixed for an open file, so it is read once instead of after every edit.
    private sealed record Builtins(
        Dictionary<string, (int Min, int Max)> Ranges,
        HashSet<string> Deprecated,
        HashSet<string> Functions,
        HashSet<string> Variables,
        HashSet<string> ReadOnly,
        bool Escapes);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Workspace, Cached> Cache = [];

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<G3MLib.DataFile.GameMakerData, Builtins> BuiltinTables = [];

    // Script functions are listed as gml_Script_name in the function list of GameMaker 2.3 and later.
    private const string ScriptPrefix = "gml_Script_";

    /// <summary>
    /// The context for code of the entry in the open file: also knows the macros, enums and dynamically made names other entries declare.
    /// Call it on the UI thread; the result may be used on any thread.
    /// </summary>
    public static GmlLintContext For(Workspace workspace, string entry)
    {
        var shared = For(workspace);
        var instance = !entry.StartsWith(ScriptPrefix, StringComparison.Ordinal) && !entry.StartsWith("gml_GlobalScript_", StringComparison.Ordinal);
        // Before the first index of other entries is built any name may be declared there; the tab checks again when the index is ready.
        return shared with { IsKnown = name => shared.IsKnown(name) || GmlProjectSymbols.Find(workspace, name, entry) is not null || !GmlProjectSymbols.IsBuilt(workspace), InstanceCode = instance };
    }

    /// <summary>The context for the open file. Call it on the UI thread; the result may be used on any thread.</summary>
    public static GmlLintContext For(Workspace workspace)
    {
        if (Cache.TryGetValue(workspace, out var known) && known.Version == workspace.History.Version && known.Nicks == workspace.Nicknames.Version) return known.Context;
        var names = GmlNames.Completions(workspace)().Select(n => n.Key).ToHashSet(StringComparer.Ordinal);
        var game = new HashSet<string>(StringComparer.Ordinal);
        Builtins builtins;
        using (workspace.Lock.Read())
        {
            var data = workspace.Data;
            builtins = BuiltinTables.GetValue(data, ReadBuiltins);
            // Every variable and function the compiled code of the game uses: instance variables set in other events, methods stored in variables.
            foreach (var variable in data.Variables) if (variable?.Name?.Content is { Length: > 0 } name) game.Add(name);
            foreach (var function in data.Functions)
            {
                if (function?.Name?.Content is not { Length: > 0 } name) continue;
                game.Add(name);
                if (name.StartsWith(ScriptPrefix, StringComparison.Ordinal)) game.Add(name[ScriptPrefix.Length..]);
            }

            foreach (var extension in data.Extensions ?? [])
                foreach (var file in extension?.Files ?? [])
                    foreach (var function in file?.Functions ?? [])
                        if (function?.Name?.Content is { Length: > 0 } name) game.Add(name);
        }

        var reference = GmlReference.Default;
        // A script of the game with a built-in name is the game's own function: its calls are not judged by the built-in rules.
        var gameFunctions = GmlNames.GameFunctionNames(workspace).ToHashSet(StringComparer.Ordinal);
        var (ranges, deprecated, builtinFunctions, builtinVariables, readOnly, escapes) = builtins;
        var context = new GmlLintContext(
            name => names.Contains(name) || game.Contains(name) || reference.Find(name) is not null,
            name => gameFunctions.Contains(name) ? null : ranges.TryGetValue(name, out var range) ? range : null,
            readOnly.Contains,
            name => !gameFunctions.Contains(name) && deprecated.Contains(name),
            [.. names],
            name => !gameFunctions.Contains(name) && builtinFunctions.Contains(name),
            builtinVariables.Contains,
            // A script of the game: what its declaration says, once a worker has read it.
            name => gameFunctions.Contains(name) ? GameFunctions.Peek(workspace, name)?.Required : null,
            name => gameFunctions.Contains(name) && GameFunctions.Peek(workspace, name)?.Constructor == true,
            escapes);
        Cache.AddOrUpdate(workspace, new Cached(workspace.History.Version, workspace.Nicknames.Version, context));
        return context;
    }

    // The caller holds the read lock of the model.
    private static Builtins ReadBuiltins(G3MLib.DataFile.GameMakerData data)
    {
        var compiler = new Dictionary<string, (int Min, int Max)>(StringComparer.Ordinal);
        var readOnly = new HashSet<string>(StringComparer.Ordinal);
        var variables = new HashSet<string>(StringComparer.Ordinal);
        var list = new BuiltinList(data);
        foreach (var function in list.Functions.Values) compiler[function.Name] = (function.MinArguments, function.MaxArguments);
        foreach (var name in list.InstanceVars.Keys.Concat(list.GlobalVars.Keys).Concat(list.GlobalArrayVars.Keys).Concat(list.InstanceLimitedVars.Keys))
        {
            variables.Add(name);
            if (list.LookupBuiltinVariable(name) is { CanSet: false }) readOnly.Add(name);
        }

        foreach (var name in list.Constants.Keys) readOnly.Add(name);
        var functions = new HashSet<string>(compiler.Keys, StringComparer.Ordinal);
        var ranges = new Dictionary<string, (int Min, int Max)>(StringComparer.Ordinal);
        var deprecated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in GmlReference.Default.Entries)
        {
            if (entry.Deprecated) deprecated.Add(entry.Name);
            // The compiler list knows the version of the game; the reference decides only for a variable that list does not have.
            if (entry is { Kind: RefKind.Variable, ReadOnly: true } && !variables.Contains(entry.Name)) readOnly.Add(entry.Name);
            if (entry.Kind != RefKind.Function) continue;
            functions.Add(entry.Name);
            var documented = (entry.Required, entry.Variadic ? int.MaxValue : entry.Parameters.Count);
            if (GmlLint.Combine(entry.Name, documented, compiler.TryGetValue(entry.Name, out var counts) ? counts : null) is { } range) ranges[entry.Name] = range;
        }

        return new Builtins(ranges, deprecated, functions, variables, readOnly, data.IsGameMaker2());
    }
}
