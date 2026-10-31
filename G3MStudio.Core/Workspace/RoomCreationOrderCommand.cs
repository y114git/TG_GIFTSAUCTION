using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Moves one instance to a position in the room's creation order, which is the order of the room's master instance list.</summary>
public sealed class RoomCreationOrderCommand : EditCommand
{
    public const string Name = "roomOrder";

    private readonly string _room;
    private readonly uint _id;
    private readonly int _index;
    private int _oldIndex;

    public RoomCreationOrderCommand(string room, uint id, int index) => (_room, _id, _index) = (room, id, index);

    public override string Kind => Name;

    public override string Editor => "rooms";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["room"] = _room,
        ["id"] = _id.ToString(CultureInfo.InvariantCulture),
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Rooms", _room)];

    public static RoomCreationOrderCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["room"], uint.Parse(a["id"], CultureInfo.InvariantCulture), int.Parse(a["index"], CultureInfo.InvariantCulture));

    private GameMakerPointerList<GameMakerRoom.GameObject> Order(GameMakerData data) =>
        (data.Rooms.FirstOrDefault(r => r.Name.Content == _room) ?? throw new InvalidOperationException($"Room '{_room}' does not exist.")).GameObjects
        ?? throw new InvalidOperationException($"Room '{_room}' has no instance list.");

    private static void Place(GameMakerPointerList<GameMakerRoom.GameObject> list, uint id, int from, int to)
    {
        var instance = list[from];
        if (instance.InstanceID != id) throw new InvalidOperationException($"Instance {id} is not at position {from}.");
        list.RemoveAt(from);
        list.Insert(Math.Clamp(to, 0, list.Count), instance);
    }

    public override void Apply(GameMakerData data)
    {
        var list = Order(data);
        _oldIndex = list.ToList().FindIndex(i => i.InstanceID == _id);
        if (_oldIndex < 0) throw new InvalidOperationException($"Room '{_room}' has no instance {_id}.");
        Place(list, _id, _oldIndex, _index);
    }

    public override void Revert(GameMakerData data)
    {
        var list = Order(data);
        Place(list, _id, list.ToList().FindIndex(i => i.InstanceID == _id), _oldIndex);
    }
}
