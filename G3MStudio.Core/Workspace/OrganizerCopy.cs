namespace G3MStudio.Core.Workspace;

/// <summary>Carries the folder, collection and tag membership of copied resources into the organisation of the target data file.</summary>
public static class OrganizerCopy
{
    /// <summary>True when any of the keys sits in a folder or collection or carries a tag.</summary>
    public static bool HasMembership(Organizer organizer, QuickAccess quick, IEnumerable<string> keys) =>
        keys.Any(k => organizer.FolderOf.ContainsKey(k) || organizer.CollectionsOf(k).Count > 0 || quick.TagsOf(k).Count > 0);

    /// <summary>
    /// Puts each target key where its source key sits: the folder path is made in the target when it is missing (found by name), the collection is
    /// made or joined by name, the tag is added. <paramref name="keyMap"/> maps source keys to target keys.
    /// </summary>
    public static (Organizer Organizer, QuickAccess Quick) Merge(Organizer from, QuickAccess fromQuick, Organizer to, QuickAccess toQuick, IReadOnlyDictionary<string, string> keyMap)
    {
        Dictionary<string, string> made = [];
        foreach (var (source, target) in keyMap)
        {
            if (from.FolderOf.TryGetValue(source, out var folder))
            {
                string? parent = null;
                foreach (var part in from.PathOf(folder))
                {
                    if (!made.TryGetValue(part.Id, out var id))
                    {
                        id = to.Folders.FirstOrDefault(f => f.Parent == parent && string.Equals(f.Name, part.Name, StringComparison.OrdinalIgnoreCase))?.Id;
                        if (id is null)
                        {
                            (to, id) = to.AddFolder(part.Name, parent);
                            if (id is not null && part.Colour.Length > 0) to = to.ColourFolder(id, part.Colour);
                        }

                        if (id is not null) made[part.Id] = id;
                    }

                    parent = id;
                    if (id is null) break;
                }

                if (parent is not null) to = to.MoveTo([target], parent);
            }

            foreach (var name in from.CollectionsOf(source))
            {
                var had = to.Collection(name) is not null;
                to = to.AddToCollection(name, [target]);
                if (!had && from.Collection(name)?.Colour is { Length: > 0 } colour) to = to.ColourCollection(name, colour);
            }

            foreach (var tag in fromQuick.TagsOf(source))
            {
                toQuick = toQuick.AddTag(target, tag);
                if (from.TagColours.TryGetValue(tag, out var colour) && !to.TagColours.ContainsKey(tag)) to = to.ColourTag(tag, colour);
            }
        }

        return (to, toQuick);
    }
}
