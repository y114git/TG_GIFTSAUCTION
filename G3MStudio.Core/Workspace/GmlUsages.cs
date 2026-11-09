namespace G3MStudio.Core.Workspace;

/// <summary>Maps a name typed in code to the resource it names, so Find Usages and Go to Definition can start from the text.</summary>
public static class GmlUsages
{
    /// <summary>The Explorer type and name of the resource of the open file that has this name, or null (a variable or a built-in).</summary>
    public static (string Type, string Name)? Resolve(Workspace workspace, string name)
    {
        using (workspace.Lock.Read())
        {
            foreach (var type in ResourceTypes.All.Where(t => !t.IsData))
                if ((type.Items(workspace.Data) ?? []).Any(i => ResourceTypes.NameOf(i) == name)) return (type.Key, name);
        }

        return null;
    }
}
