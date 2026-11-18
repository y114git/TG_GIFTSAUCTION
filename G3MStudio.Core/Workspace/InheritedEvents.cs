using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>An event an object gets from an ancestor because it has no event of that type and subtype itself.</summary>
public sealed record InheritedEvent(string Owner, uint Type, uint Subtype, string? Code);

public static class InheritedEvents
{
    /// <summary>The events of the parent chain, nearest ancestor first, leaving out any type and subtype the object or a nearer ancestor already has.</summary>
    public static IReadOnlyList<InheritedEvent> Of(GameMakerGameObject obj)
    {
        var seen = new HashSet<(int, uint)>();
        foreach (var (type, evt) in Events(obj)) seen.Add((type, evt.EventSubtype));
        var result = new List<InheritedEvent>();
        var visited = new HashSet<GameMakerGameObject> { obj };
        for (var parent = obj.ParentId; parent is not null && visited.Add(parent); parent = parent.ParentId)
        {
            foreach (var (type, evt) in Events(parent))
                if (seen.Add((type, evt.EventSubtype)))
                    result.Add(new InheritedEvent(parent.Name.Content ?? "", (uint)type, evt.EventSubtype, evt.Actions.Select(a => a.CodeId?.Name?.Content).OfType<string>().FirstOrDefault()));
        }

        return result;
    }

    private static IEnumerable<(int Type, GameMakerGameObject.Event Event)> Events(GameMakerGameObject obj) =>
        obj.Events.SelectMany((list, type) => list.Select(e => (type, e)));
}
