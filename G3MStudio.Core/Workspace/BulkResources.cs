using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>Edits on several resources chosen in the Explorer, each as one undoable group.</summary>
public static class BulkResources
{
    /// <summary>The entries of a selection as (type, name), in selection order, without repeats.</summary>
    public static IReadOnlyList<(string Type, string Name)> Split(IEnumerable<string> keys) =>
        [.. keys.Select(ResourceTypes.SplitKey).OfType<(string, string)>().Distinct()];

    /// <summary>One edit that deletes every item; an item that something uses first has those uses cleared, as a single delete does.</summary>
    public static EditCommand Delete(IReadOnlyList<(string Type, string Name, bool Used)> items) =>
        new GroupCommand([.. items.SelectMany(i => i.Used
            ? new EditCommand[] { new ClearReferencesCommand(i.Type, i.Name), new ResourceListCommand(i.Type, ResourceListCommand.Delete, i.Name) }
            : [new ResourceListCommand(i.Type, ResourceListCommand.Delete, i.Name)])]);

    /// <summary>A free name for a copy: name_copy, then name_copy2, name_copy3. <paramref name="taken"/> holds the copies chosen so far in the same batch.</summary>
    public static string CopyName(GameMakerData data, string name, ISet<string> taken)
    {
        var copy = name + "_copy";
        for (var n = 2; ResourceListCommand.NameTaken(data, copy) || taken.Contains(copy); n++) copy = $"{name}_copy{n}";
        taken.Add(copy);
        return copy;
    }

    /// <summary>One edit that copies every item that can be copied; returns the new keys. Call under the read lock: the code of each copy is decompiled first.</summary>
    public static EditCommand Duplicate(GameMakerData data, IReadOnlyList<(string Type, string Name)> items, out IReadOnlyList<string> newKeys)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var commands = new List<EditCommand>();
        var keys = new List<string>();
        foreach (var (type, name) in items.Where(i => ResourceDuplicate.Types.Contains(i.Type)))
        {
            var copy = CopyName(data, name, taken);
            commands.Add(ResourceDuplicate.Build(data, type, name, copy));
            keys.Add(ResourceTypes.KeyOf(type, copy));
        }

        newKeys = keys;
        return new GroupCommand(commands);
    }
}
