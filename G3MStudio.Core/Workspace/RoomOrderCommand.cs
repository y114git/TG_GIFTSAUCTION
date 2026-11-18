using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Changes the room order of the game (the first room is where the game starts): adds a room at the end, removes one, or moves one to another place. Undo puts the entry back where it was.</summary>
public sealed class RoomOrderCommand : EditCommand
{
    public const string Name = "gameRoomOrder";
    public const string Add = "add";
    public const string Remove = "remove";
    public const string Move = "move";

    private readonly string _operation, _room;
    private readonly int _index, _to;
    private object? _entry;

    /// <param name="operation">Add, Remove or Move.</param>
    /// <param name="room">The room to add (Add only).</param>
    /// <param name="index">The place of the entry to remove or move.</param>
    /// <param name="to">The place a moved entry goes to.</param>
    public RoomOrderCommand(string operation, string room = "", int index = -1, int to = -1) => (_operation, _room, _index, _to) = (operation, room, index, to);

    public override string Kind => Name;

    public override string Editor => "general";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["operation"] = _operation,
        ["room"] = _room,
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
        ["to"] = _to.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("GeneralInfo", null)];

    public static RoomOrderCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["operation"], a["room"], int.Parse(a["index"], CultureInfo.InvariantCulture), int.Parse(a["to"], CultureInfo.InvariantCulture));

    public override void Apply(GameMakerData data)
    {
        var order = data.GeneralInfo.RoomOrder;
        switch (_operation)
        {
            case Add:
                var room = data.Rooms.FirstOrDefault(r => r.Name.Content == _room) ?? throw new InvalidOperationException($"Room '{_room}' does not exist.");
                if (order.Any(r => r.Resource == room)) throw new InvalidOperationException($"'{_room}' is in the room order already.");
                var added = new GameMakerResourceById<GameMakerRoom, G3MLib.DataFile.GameMakerChunkROOM>(room);
                order.Add(added);
                _entry = added;
                break;
            case Remove:
                Check(order.Count, _index);
                _entry = order[_index];
                order.RemoveAt(_index);
                break;
            case Move:
                Check(order.Count, _index);
                Check(order.Count, _to);
                _entry = order[_index];
                order.RemoveAt(_index);
                order.Insert(_to, (GameMakerResourceById<GameMakerRoom, G3MLib.DataFile.GameMakerChunkROOM>)_entry);
                break;
            default:
                throw new InvalidDataException($"Unknown room order operation '{_operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        var order = data.GeneralInfo.RoomOrder;
        var entry = (GameMakerResourceById<GameMakerRoom, G3MLib.DataFile.GameMakerChunkROOM>?)_entry ?? throw new InvalidOperationException("The edit was never applied.");
        switch (_operation)
        {
            case Add:
                order.Remove(entry);
                break;
            case Remove:
                order.Insert(_index, entry);
                break;
            default:
                order.Remove(entry);
                order.Insert(_index, entry);
                break;
        }
    }

    private static void Check(int count, int index)
    {
        if (index < 0 || index >= count) throw new InvalidOperationException($"The room order has no place {index}.");
    }
}
