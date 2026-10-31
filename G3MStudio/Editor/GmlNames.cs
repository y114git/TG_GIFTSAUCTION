using G3MLib.DataFile.Compiler;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>Tells the colorizer what a GML name stands for, from GameMaker's built-in names and the resources of the open data file.</summary>
public static class GmlNames
{
    private static readonly Lazy<Dictionary<string, string>> Builtin = new(() =>
    {
        var list = new BuiltinList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in list.InstanceVars.Keys.Concat(list.GlobalVars.Keys).Concat(list.GlobalArrayVars.Keys)) names[name] = "Variable";
        foreach (var name in list.Constants.Keys) names[name] = "Constant";
        foreach (var name in list.Functions.Keys) names[name] = "Function";
        return names;
    });

    /// <summary>
    /// A lookup for the names of one data file: scripts are functions, every other resource is an asset, then the built-in names.
    /// shortcut: the resource names are read once when the tab opens, so a rename shows after the tab is opened again.
    /// </summary>
    public static Func<string, string?> For(Workspace workspace)
    {
        var own = new Dictionary<string, string>(StringComparer.Ordinal);
        using (workspace.Lock.Read())
        {
            foreach (var type in ResourceTypes.All.Where(t => !t.IsData))
                foreach (var item in type.Items(workspace.Data) ?? [])
                    if (ResourceTypes.NameOf(item) is { Length: > 0 } name) own[name] = type.Key == "scripts" ? "Function" : "Asset";
        }

        return name => own.TryGetValue(name, out var found) ? found : Builtin.Value.GetValueOrDefault(name);
    }
}
