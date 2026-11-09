using System.Text.Json;

namespace G3MStudio.Core.Workspace;

/// <summary>A user-made folder of the Explorer. <see cref="Parent"/> is the id of the folder it sits in, or null at the top.</summary>
public sealed record VirtualFolder(string Id, string Name, string? Parent, string Colour = "");

/// <summary>A named set of resource keys; one resource may be in many collections.</summary>
public sealed record ResourceSet(string Name, IReadOnlyList<string> Keys, string Colour = "");

/// <summary>
/// The user's own organisation of a data file: nested folders (a resource sits in one), collections (a resource may be in many) and tag colours.
/// It lives in Studio's user data folder beside the view state and never enters the data file (the file stores no folder tree). Every change returns a new value.
/// </summary>
public sealed record Organizer
{
    public static readonly Organizer Empty = new();

    public IReadOnlyList<VirtualFolder> Folders { get; init; } = [];

    /// <summary>Resource key to the id of the folder that holds it; a key that is not here sits at the top.</summary>
    public IReadOnlyDictionary<string, string> FolderOf { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<ResourceSet> Collections { get; init; } = [];

    /// <summary>Tag name to #RRGGBB.</summary>
    public IReadOnlyDictionary<string, string> TagColours { get; init; } = new Dictionary<string, string>();

    /// <summary>The Explorer shows folders as its main tree for this file; null follows the setting.</summary>
    public bool? FolderView { get; init; }

    public VirtualFolder? Folder(string? id) => id is null ? null : Folders.FirstOrDefault(f => f.Id == id);

    public IEnumerable<VirtualFolder> ChildrenOf(string? parent) =>
        Folders.Where(f => f.Parent == parent).OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Keys held directly by the folder.</summary>
    public IEnumerable<string> MembersOf(string id) => FolderOf.Where(p => p.Value == id).Select(p => p.Key);

    /// <summary>The folder and every folder below it, the folder first.</summary>
    public IReadOnlyList<string> Subtree(string id)
    {
        List<string> ids = [];
        var seen = new HashSet<string> { id };
        var todo = new Queue<string>([id]);
        while (todo.TryDequeue(out var next))
        {
            ids.Add(next);
            foreach (var child in Folders.Where(f => f.Parent == next && seen.Add(f.Id))) todo.Enqueue(child.Id);
        }

        return ids;
    }

    /// <summary>Every key in the folder and below it.</summary>
    public IReadOnlyList<string> KeysUnder(string id)
    {
        var ids = Subtree(id).ToHashSet();
        return [.. FolderOf.Where(p => ids.Contains(p.Value)).Select(p => p.Key)];
    }

    /// <summary>Folders from the top one down to this one, for the breadcrumb.</summary>
    public IReadOnlyList<VirtualFolder> PathOf(string id)
    {
        var path = new List<VirtualFolder>();
        for (var f = Folder(id); f is not null && path.Count <= Folders.Count; f = Folder(f.Parent)) path.Insert(0, f);
        return path;
    }

    /// <summary>The reason a folder name is refused ("empty", or "taken" when a sibling has it), or null.</summary>
    public string? NameProblem(string? parent, string name, string? exceptId = null)
    {
        name = name.Trim();
        if (name.Length == 0) return "empty";
        return Folders.Any(f => f.Parent == parent && f.Id != exceptId && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) ? "taken" : null;
    }

    /// <summary>Adds a folder; the id is null (and nothing changes) when the name is refused or the parent is unknown.</summary>
    public (Organizer State, string? Id) AddFolder(string name, string? parent)
    {
        if (NameProblem(parent, name) is not null || parent is not null && Folder(parent) is null) return (this, null);
        var id = Guid.NewGuid().ToString("N")[..10];
        return (this with { Folders = [.. Folders, new VirtualFolder(id, name.Trim(), parent)] }, id);
    }

    public Organizer RenameFolder(string id, string name) =>
        Folder(id) is { } f && NameProblem(f.Parent, name, id) is null ? Edit(id, f with { Name = name.Trim() }) : this;

    public Organizer ColourFolder(string id, string colour) => Folder(id) is { } f ? Edit(id, f with { Colour = colour }) : this;

    private Organizer Edit(string id, VirtualFolder folder) => this with { Folders = [.. Folders.Select(f => f.Id == id ? folder : f)] };

    /// <summary>Moves a folder under another (null: the top). Refused for itself, its own subtree, an unknown folder or a name that the new parent already has.</summary>
    public Organizer MoveFolder(string id, string? parent) =>
        Folder(id) is { } f && f.Parent != parent && (parent is null || Folder(parent) is not null && !Subtree(id).Contains(parent)) && NameProblem(parent, f.Name, id) is null
            ? Edit(id, f with { Parent = parent }) : this;

    /// <summary>Deletes a folder; its sub-folders and members move up to its parent, so no resource is lost from view.</summary>
    public Organizer DeleteFolder(string id)
    {
        if (Folder(id) is not { } f) return this;
        var up = f.Parent;
        var rest = Folders.Where(x => x.Id != id).ToList();
        foreach (var child in rest.Where(c => c.Parent == id).ToList())
        {
            // A child whose name the parent already has gets a number, so the move cannot fail.
            var name = child.Name;
            for (var n = 2; rest.Any(x => x.Parent == up && x.Id != child.Id && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)); n++) name = $"{child.Name} {n}";
            rest[rest.FindIndex(x => x.Id == child.Id)] = child with { Parent = up, Name = name };
        }

        var members = new Dictionary<string, string>();
        foreach (var (key, folder) in FolderOf)
        {
            if (folder != id) members[key] = folder;
            else if (up is not null) members[key] = up;
        }

        return this with { Folders = rest, FolderOf = members };
    }

    /// <summary>Puts the keys in a folder (null: back at the top). A key sits in one folder at a time.</summary>
    public Organizer MoveTo(IEnumerable<string> keys, string? folder)
    {
        if (folder is not null && Folder(folder) is null) return this;
        var map = new Dictionary<string, string>(FolderOf);
        foreach (var key in keys)
        {
            if (folder is null) map.Remove(key);
            else map[key] = folder;
        }

        return this with { FolderOf = map };
    }

    public string? CollectionProblem(string name, string? except = null) =>
        name.Trim().Length == 0 ? "empty" : Collections.Any(c => c.Name != except && string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) ? "taken" : null;

    public ResourceSet? Collection(string name) => Collections.FirstOrDefault(c => c.Name == name);

    public IReadOnlyList<string> CollectionsOf(string key) => [.. Collections.Where(c => c.Keys.Contains(key)).Select(c => c.Name)];

    /// <summary>Adds keys to the collection, making it when it is new. Returns this when the name is empty.</summary>
    public Organizer AddToCollection(string name, IEnumerable<string> keys)
    {
        name = name.Trim();
        if (name.Length == 0) return this;
        var existing = Collections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return this with { Collections = [.. Collections, new ResourceSet(name, [.. keys.Distinct()])] };
        return this with { Collections = [.. Collections.Select(c => c == existing ? c with { Keys = [.. c.Keys.Concat(keys).Distinct()] } : c)] };
    }

    public Organizer RemoveFromCollection(string name, IEnumerable<string> keys)
    {
        var drop = keys.ToHashSet();
        return this with { Collections = [.. Collections.Select(c => c.Name == name ? c with { Keys = [.. c.Keys.Where(k => !drop.Contains(k))] } : c)] };
    }

    public Organizer RenameCollection(string name, string newName) =>
        CollectionProblem(newName, name) is null ? this with { Collections = [.. Collections.Select(c => c.Name == name ? c with { Name = newName.Trim() } : c)] } : this;

    public Organizer ColourCollection(string name, string colour) =>
        this with { Collections = [.. Collections.Select(c => c.Name == name ? c with { Colour = colour } : c)] };

    public Organizer DeleteCollection(string name) => this with { Collections = [.. Collections.Where(c => c.Name != name)] };

    /// <summary>Sets the colour of a tag; an empty colour clears it.</summary>
    public Organizer ColourTag(string tag, string colour)
    {
        var map = new Dictionary<string, string>(TagColours);
        if (colour.Length == 0) map.Remove(tag);
        else map[tag] = colour;
        return this with { TagColours = map };
    }

    /// <summary>A resource was renamed or deleted: its key follows the new name, or leaves every folder and collection when <paramref name="newKey"/> is null.</summary>
    public Organizer Rekey(string oldKey, string? newKey)
    {
        if (!FolderOf.ContainsKey(oldKey) && !Collections.Any(c => c.Keys.Contains(oldKey))) return this;
        var map = new Dictionary<string, string>(FolderOf);
        if (map.Remove(oldKey, out var folder) && newKey is not null) map[newKey] = folder;
        IReadOnlyList<string> Map(IEnumerable<string> keys) => [.. keys.Select(k => k == oldKey ? newKey : k).OfType<string>().Distinct()];
        return this with { FolderOf = map, Collections = [.. Collections.Select(c => c with { Keys = Map(c.Keys) })] };
    }

    /// <summary>Puts a deleted resource back where it was (undo): its folder and the collections that still exist.</summary>
    public Organizer Restore(string key, string? folder, IEnumerable<string> collections)
    {
        var next = MoveTo([key], folder);
        foreach (var name in collections.Where(n => Collection(n) is not null)) next = next.AddToCollection(name, [key]);
        return next;
    }

    /// <summary>Drops nulls and links to folders that do not exist, which a hand-edited file may hold.</summary>
    internal Organizer Sanitized()
    {
        var folders = (Folders ?? []).Where(f => f is { Id.Length: > 0, Name: not null }).DistinctBy(f => f.Id).ToList();
        var ids = folders.Select(f => f.Id).ToHashSet();
        // A parent that is unknown or that loops back is cut, so the tree always has a top.
        folders = [.. folders.Select(f => f.Parent is not null && (!ids.Contains(f.Parent) || f.Parent == f.Id) ? f with { Parent = null } : f)];
        // A longer loop (A under B under A) is cut at the first folder that leads back to itself.
        var parents = folders.ToDictionary(f => f.Id, f => f.Parent);
        foreach (var f in folders)
        {
            var hops = 0;
            for (var at = parents[f.Id]; at is not null && hops <= folders.Count; at = parents[at]) hops++;
            if (hops > folders.Count) parents[f.Id] = null;
        }

        folders = [.. folders.Select(f => f with { Parent = parents[f.Id] })];
        var clean = this with
        {
            Folders = folders,
            FolderOf = (FolderOf ?? new Dictionary<string, string>()).Where(p => p.Value is not null && ids.Contains(p.Value)).ToDictionary(),
            Collections = [.. (Collections ?? []).Where(c => c is { Name: not null, Keys: not null }).Select(c => c with { Keys = [.. c.Keys.Where(k => k is not null)], Colour = c.Colour ?? "" })],
            TagColours = (TagColours ?? new Dictionary<string, string>()).Where(p => p.Value is not null).ToDictionary(),
        };
        return clean.Folders.Any(f => clean.PathOf(f.Id).Count == 0 || clean.Subtree(f.Id).Count > clean.Folders.Count) ? this with { Folders = [], FolderOf = new Dictionary<string, string>(), Collections = clean.Collections, TagColours = clean.TagColours } : clean;
    }
}

/// <summary>Reads and writes the <see cref="Organizer"/> of a data file in the user data folder, keyed like the view state.</summary>
public sealed class OrganizerStore(UserData userData)
{
    private string FileFor(string dataFile) => userData.Combine("organize", UserData.KeyFor(dataFile) + ".json");

    public void Save(string dataFile, Organizer state) => UserData.WriteAllTextAtomic(FileFor(dataFile), JsonSerializer.Serialize(state));

    /// <summary>The saved organisation, or an empty one when there is none or the file is damaged (only a lost convenience).</summary>
    public Organizer Load(string dataFile)
    {
        try
        {
            return JsonSerializer.Deserialize<Organizer>(File.ReadAllText(FileFor(dataFile)))?.Sanitized() ?? Organizer.Empty;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return Organizer.Empty;
        }
    }
}
