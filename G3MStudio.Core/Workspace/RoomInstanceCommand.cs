using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Moves, deletes or adds one object instance of a room. Instances are found by their instance id; an added one takes the next id after <c>LastObj</c>.</summary>
#pragma warning disable CA1001 // the instance belongs to the room or is put back into it; the model disposes it
public sealed class RoomInstanceCommand : EditCommand
{
    public const string Name = "roomInstance";
    public const string Move = "move";
    public const string Delete = "delete";
    public const string Add = "add";
    public const string FlipX = "flipx";
    public const string FlipY = "flipy";
    public const string Set = "set";

    /// <summary>The fields Set can change: names are the instance property names in lower case.</summary>
    public static readonly string[] Fields = ["x", "y", "scalex", "scaley", "rotation", "imageindex", "imagespeed", "object"];

    private readonly string _room;
    private readonly string _operation;
    private readonly string _object;
    private readonly int _x;
    private readonly int _y;
    private readonly float _scaleX;
    private readonly float _scaleY;
    private (int X, int Y, float ScaleX, float ScaleY) _before;
    private string _oldValue = "";
    private readonly Dictionary<string, string> _args;
    private (int X, int Y) _oldPosition;
    private GameMakerRoom.GameObject? _instance;
    private readonly List<(IList<GameMakerRoom.GameObject> List, int Index)> _slots = [];
    private uint _oldLastObj;

    // id: the instance to move or delete; for an add, 0 picks the next free id.
    public RoomInstanceCommand(string room, string operation, uint id, int x = 0, int y = 0, string objectName = "", float scaleX = 1, float scaleY = 1, string value = "")
    {
        (_room, _operation, _x, _y, _object, _scaleX, _scaleY) = (room, operation, x, y, objectName, scaleX, scaleY);
        _args = new Dictionary<string, string>
        {
            ["room"] = room,
            ["operation"] = operation,
            ["id"] = id.ToString(CultureInfo.InvariantCulture),
            ["x"] = x.ToString(CultureInfo.InvariantCulture),
            ["y"] = y.ToString(CultureInfo.InvariantCulture),
            ["object"] = objectName,
            ["sx"] = scaleX.ToString("R", CultureInfo.InvariantCulture),
            ["sy"] = scaleY.ToString("R", CultureInfo.InvariantCulture),
            ["value"] = value,
        };
    }

    public override string Kind => Name;

    public override string Editor => "rooms";

    public override IReadOnlyDictionary<string, string> Args => _args;

    public override IReadOnlyList<ProjectTouch>? Touches => _operation == Add ? [new("Rooms", _room), new("GeneralInfo", null)] : [new("Rooms", _room)];

    public static RoomInstanceCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["room"], a["operation"], uint.Parse(a["id"], CultureInfo.InvariantCulture),
        int.Parse(a["x"], CultureInfo.InvariantCulture), int.Parse(a["y"], CultureInfo.InvariantCulture), a["object"],
        float.Parse(a.GetValueOrDefault("sx", "1"), CultureInfo.InvariantCulture), float.Parse(a.GetValueOrDefault("sy", "1"), CultureInfo.InvariantCulture), a.GetValueOrDefault("value", ""));

    /// <summary>Every instance list of a room: one per instance layer, or the flat list of the older layout.</summary>
    public static IEnumerable<IList<GameMakerRoom.GameObject>> Lists(GameMakerRoom room)
    {
        foreach (var layer in room.Layers ?? [])
            if (layer.InstancesData is { } data) yield return data.Instances;
        if (room.GameObjects is { } flat) yield return flat;
    }

    private GameMakerRoom FindRoom(GameMakerData data) => data.Rooms.FirstOrDefault(r => r.Name.Content == _room) ?? throw new InvalidOperationException($"Room '{_room}' does not exist.");

    private uint Id => uint.Parse(_args["id"], CultureInfo.InvariantCulture);

    // The room keeps one master list of all its instances; a layer lists the same objects again and the writer stores only their ids.
    // An instance missing from the master list reads back as nonexistent, so every change goes to the master list and to the layer.
    public override void Apply(GameMakerData data)
    {
        var room = FindRoom(data);
        _slots.Clear();
        if (_operation == Add)
        {
            var definition = data.GameObjects.FirstOrDefault(o => o.Name.Content == _object) ?? throw new InvalidOperationException($"Object '{_object}' does not exist.");
            var layer = (room.Layers ?? []).Select(l => l.InstancesData?.Instances).FirstOrDefault(l => l is not null);
            _oldLastObj = data.GeneralInfo.LastObj;
            var id = Id != 0 ? Id : Math.Max(_oldLastObj, data.Rooms.SelectMany(Lists).SelectMany(l => l).Select(i => i.InstanceID).DefaultIfEmpty().Max()) + 1;
            _args["id"] = id.ToString(CultureInfo.InvariantCulture);
            data.GeneralInfo.LastObj = Math.Max(_oldLastObj, id);
            _instance = new GameMakerRoom.GameObject { ObjectDefinition = definition, X = _x, Y = _y, InstanceID = id, ScaleX = _scaleX, ScaleY = _scaleY };
            foreach (var list in new IList<GameMakerRoom.GameObject>?[] { room.GameObjects, layer }.OfType<IList<GameMakerRoom.GameObject>>())
            {
                _slots.Add((list, list.Count));
                list.Add(_instance);
            }

            return;
        }

        _instance = Lists(room).SelectMany(l => l).FirstOrDefault(i => i.InstanceID == Id) ?? throw new InvalidOperationException($"Room '{_room}' has no instance {Id}.");
        if (_operation == Move)
        {
            _oldPosition = (_instance.X, _instance.Y);
            (_instance.X, _instance.Y) = (_x, _y);
        }
        else if (_operation == Set)
        {
            // For Set the object name slot holds the field name and the value argument the new value.
            var value = _args["value"];
            _oldValue = ReadField(_instance, _object);
            WriteField(_instance, _object, value, data);
        }
        else if (_operation is FlipX or FlipY)
        {
            // Mirroring keeps the picture where it is on screen, so the position moves by the sprite's width less twice its origin.
            _before = (_instance.X, _instance.Y, _instance.ScaleX, _instance.ScaleY);
            var sprite = _instance.ObjectDefinition?.Sprite;
            if (_operation == FlipX)
            {
                if (sprite is not null) _instance.X += (int)Math.Round((sprite.Width - 2 * sprite.OriginX) * _instance.ScaleX);
                _instance.ScaleX = -_instance.ScaleX;
            }
            else
            {
                if (sprite is not null) _instance.Y += (int)Math.Round((sprite.Height - 2 * sprite.OriginY) * _instance.ScaleY);
                _instance.ScaleY = -_instance.ScaleY;
            }
        }
        else if (_operation == Delete)
        {
            foreach (var list in Lists(room))
            {
                var at = list.IndexOf(_instance);
                if (at < 0) continue;
                _slots.Add((list, at));
                list.RemoveAt(at);
            }
        }
        else
        {
            throw new InvalidDataException($"Unknown room operation '{_operation}'.");
        }
    }

    private static string ReadField(GameMakerRoom.GameObject i, string field) => field switch
    {
        "x" => i.X.ToString(CultureInfo.InvariantCulture),
        "y" => i.Y.ToString(CultureInfo.InvariantCulture),
        "scalex" => i.ScaleX.ToString("R", CultureInfo.InvariantCulture),
        "scaley" => i.ScaleY.ToString("R", CultureInfo.InvariantCulture),
        "rotation" => i.Rotation.ToString("R", CultureInfo.InvariantCulture),
        "imageindex" => i.ImageIndex.ToString(CultureInfo.InvariantCulture),
        "imagespeed" => i.ImageSpeed.ToString("R", CultureInfo.InvariantCulture),
        "object" => i.ObjectDefinition?.Name.Content ?? "",
        _ => throw new InvalidDataException($"Unknown instance field '{field}'."),
    };

    private static void WriteField(GameMakerRoom.GameObject i, string field, string value, GameMakerData data)
    {
        switch (field)
        {
            case "x": i.X = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "y": i.Y = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "scalex": i.ScaleX = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "scaley": i.ScaleY = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "rotation": i.Rotation = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "imageindex": i.ImageIndex = int.Parse(value, CultureInfo.InvariantCulture); break;
            case "imagespeed": i.ImageSpeed = float.Parse(value, CultureInfo.InvariantCulture); break;
            case "object":
                i.ObjectDefinition = data.GameObjects.FirstOrDefault(o => o.Name.Content == value) ?? throw new InvalidOperationException($"Object '{value}' does not exist.");
                break;
            default: throw new InvalidDataException($"Unknown instance field '{field}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        if (_instance is null) throw new InvalidOperationException("The edit was never applied.");
        switch (_operation)
        {
            case Move:
                (_instance.X, _instance.Y) = _oldPosition;
                break;
            case Set:
                WriteField(_instance, _object, _oldValue, data);
                break;
            case FlipX or FlipY:
                (_instance.X, _instance.Y, _instance.ScaleX, _instance.ScaleY) = _before;
                break;
            case Delete:
                foreach (var (list, at) in _slots.AsEnumerable().Reverse()) list.Insert(at, _instance);
                break;
            default:
                foreach (var (list, _) in _slots) list.Remove(_instance);
                data.GeneralInfo.LastObj = _oldLastObj;
                break;
        }
    }
}
#pragma warning restore CA1001
