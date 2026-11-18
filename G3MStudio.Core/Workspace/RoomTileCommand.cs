using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Moves or deletes one old-style tile of a room (the tiles of GameMaker Studio 1 games, listed in the room itself, not in a layer; UTMT-ROOM-07). The tile is found by its instance id.
/// Undo moves it back or puts it back at its place in the list.
/// </summary>
public sealed class RoomTileCommand : EditCommand
{
    public const string Name = "roomTile";
    public const string Move = "move";
    public const string Delete = "delete";

    private readonly string _room, _operation;
    private readonly uint _id;
    private readonly int _x, _y;
    private GameMakerRoom.Tile? _tile;
    private (int X, int Y) _oldPosition;
    private int _index = -1;

    public RoomTileCommand(string room, string operation, uint id, int x = 0, int y = 0) => (_room, _operation, _id, _x, _y) = (room, operation, id, x, y);

    public override string Kind => Name;

    public override string Editor => "rooms";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["room"] = _room,
        ["operation"] = _operation,
        ["id"] = _id.ToString(CultureInfo.InvariantCulture),
        ["x"] = _x.ToString(CultureInfo.InvariantCulture),
        ["y"] = _y.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Rooms", _room)];

    public static RoomTileCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["room"], a["operation"], uint.Parse(a["id"], CultureInfo.InvariantCulture), int.Parse(a["x"], CultureInfo.InvariantCulture), int.Parse(a["y"], CultureInfo.InvariantCulture));

    public override void Apply(GameMakerData data)
    {
        var room = data.Rooms.FirstOrDefault(r => r.Name.Content == _room) ?? throw new InvalidOperationException($"Room '{_room}' does not exist.");
        _index = room.Tiles.ToList().FindIndex(t => t.InstanceID == _id);
        _tile = _index >= 0 ? room.Tiles[_index] : throw new InvalidOperationException($"Room '{_room}' has no tile {_id}.");
        switch (_operation)
        {
            case Move:
                _oldPosition = (_tile.X, _tile.Y);
                (_tile.X, _tile.Y) = (_x, _y);
                break;
            case Delete:
                room.Tiles.RemoveAt(_index);
                break;
            default:
                throw new InvalidDataException($"Unknown tile operation '{_operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        if (_tile is null) return;
        if (_operation == Move) (_tile.X, _tile.Y) = _oldPosition;
        else if (data.Rooms.FirstOrDefault(r => r.Name.Content == _room) is { } room) room.Tiles.Insert(_index, _tile);
    }
}
