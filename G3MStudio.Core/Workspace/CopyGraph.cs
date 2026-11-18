using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Which assets each asset of a data file uses, and which code entries each asset owns. Built once from the library's reverse reference index,
/// so code is followed by its instruction operands and not by text search.
/// </summary>
public sealed class CopyGraph
{
    private readonly Dictionary<(string Type, string Name), HashSet<(string Type, string Name)>> _uses = [];
    private readonly Dictionary<(string Type, string Name), List<string>> _owned = [];

    private CopyGraph()
    {
    }

    /// <summary>The assets the item refers to, by its own properties and by the code it owns.</summary>
    public IEnumerable<(string Type, string Name)> Uses(string type, string name) =>
        _uses.TryGetValue((type, name), out var set) ? set.Where(u => u != (type, name)) : [];

    /// <summary>The names of the code entries that belong to the item (an object's events, a script, a room's creation code, a timeline's moments).</summary>
    public IReadOnlyList<string> OwnedCode(string type, string name) => _owned.TryGetValue((type, name), out var list) ? list : [];

    public static CopyGraph Build(GameMakerData data, CancellationToken cancellationToken = default)
    {
        var graph = new CopyGraph();
        var codeOwner = graph.IndexCode(data);
        var index = ReferenceCache.Of(data).Get(data, cancellationToken);
        foreach (var type in ResourceTypes.All.Where(t => UsageSearch.AssetTypes.Contains(t.Key)))
        {
            foreach (var asset in type.Items(data) ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                var used = (type.Key, ResourceTypes.NameOf(asset));
                foreach (var use in index.FindUsages(asset))
                    if (OwnerOf(use.Owner, codeOwner) is { } owner) graph.AddUse(owner, used);
            }
        }

        graph.AddCollisionPartners(data);
        return graph;
    }

    private void AddUse((string Type, string Name) owner, (string Type, string Name) used)
    {
        if (!_uses.TryGetValue(owner, out var set)) _uses[owner] = set = [];
        set.Add(used);
    }

    private static (string Type, string Name)? OwnerOf(object owner, Dictionary<string, (string Type, string Name)> codeOwner)
    {
        if (owner is GameMakerCode code) return codeOwner.TryGetValue(((code.ParentEntry ?? code).Name?.Content) ?? "", out var found) ? found : null;
        return UsageSearch.TypeKeyOf(owner) is { } type && UsageSearch.AssetTypes.Contains(type) ? (type, ResourceTypes.NameOf(owner)) : null;
    }

    // The code entry name to its owner, from the models: exact, unlike a name prefix that "obj_a" shares with "obj_a_b".
    private Dictionary<string, (string Type, string Name)> IndexCode(GameMakerData data)
    {
        var map = new Dictionary<string, (string Type, string Name)>(StringComparer.Ordinal);
        void Own(string type, string name, GameMakerCode? code)
        {
            if (code?.Name?.Content is not { } entry) return;
            map[entry] = (type, name);
            if (!_owned.TryGetValue((type, name), out var list)) _owned[(type, name)] = list = [];
            list.Add(entry);
        }

        foreach (var obj in data.GameObjects)
            foreach (var action in obj.Events.SelectMany(kind => kind).SelectMany(e => e.Actions))
                Own("objects", ResourceTypes.NameOf(obj), action.CodeId);
        foreach (var script in data.Scripts) Own("scripts", ResourceTypes.NameOf(script), script.Code);
        foreach (var room in data.Rooms)
        {
            var name = ResourceTypes.NameOf(room);
            Own("rooms", name, room.CreationCodeId);
            foreach (var instance in RoomInstanceCommand.Lists(room).SelectMany(l => l))
            {
                Own("rooms", name, instance.CreationCode);
                Own("rooms", name, instance.PreCreateCode);
            }
        }

        foreach (var timeline in data.Timelines)
            foreach (var action in timeline.Moments.SelectMany(m => m.Event ?? []))
                Own("timelines", ResourceTypes.NameOf(timeline), action.CodeId);
        return map;
    }

    // A collision event names its partner by position in the object list, which the reference index does not report.
    private void AddCollisionPartners(GameMakerData data)
    {
        const int collision = (int)EventType.Collision;
        foreach (var obj in data.GameObjects.Where(o => o.Events.Count > collision))
            foreach (var evt in obj.Events[collision].Where(e => e.EventSubtype < data.GameObjects.Count))
                AddUse(("objects", ResourceTypes.NameOf(obj)), ("objects", ResourceTypes.NameOf(data.GameObjects[(int)evt.EventSubtype])));
    }
}
