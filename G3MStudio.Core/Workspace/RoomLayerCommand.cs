using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Changes one layer of a room, found by its layer id: shows or hides it, renames it, sets its depth, deletes it, or adds an empty instance layer.
/// Deleting an instance layer also takes its instances out of the room's master instance list (otherwise they would be left in the file with no layer).
/// </summary>
#pragma warning disable CA1001 // the layer belongs to the room or is put back into it; the model disposes it
public sealed class RoomLayerCommand : EditCommand
{
    public const string Name = "roomLayer";
    public const string Visible = "visible";
    public const string Rename = "rename";
    public const string Depth = "depth";
    public const string Delete = "delete";
    public const string AddInstances = "addInstances";
    public const string AddAssets = "addAssets";
    public const string AddBackground = "addBackground";
    public const string AddTiles = "addTiles";
    public const string XOffset = "xoffset";
    public const string YOffset = "yoffset";
    public const string HSpeed = "hspeed";
    public const string VSpeed = "vspeed";

    /// <summary>Resizes the cells of a tile layer to cover the room, keeping the tiles that still fit.</summary>
    public const string FitTiles = "fitTiles";

    private readonly string _room;
    private readonly string _operation;
    private readonly string _text;
    private readonly Dictionary<string, string> _args;
    private Layer? _layer;
    private int _index;
    private string _oldText = "";
    private (uint[][] Data, uint X, uint Y)? _oldTiles;
    private List<(int Index, GameObject Instance)> _removed = [];

    // layerId: the layer to change; for an add, 0 picks the next free id. text: visible "1" or "0"; rename and add the name; depth the new depth.
    public RoomLayerCommand(string room, string operation, uint layerId, string text = "", int depth = 0, string tileSet = "")
    {
        (_room, _operation, _text) = (room, operation, text);
        _args = new Dictionary<string, string>
        {
            ["room"] = room,
            ["operation"] = operation,
            ["layer"] = layerId.ToString(CultureInfo.InvariantCulture),
            ["text"] = text,
            ["newDepth"] = depth.ToString(CultureInfo.InvariantCulture),
            ["tileSet"] = tileSet,
        };
    }

    public override string Kind => Name;

    public override string Editor => "rooms";

    public override IReadOnlyDictionary<string, string> Args => _args;

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Rooms", _room), new("Strings", null)];

    public static RoomLayerCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["room"], a["operation"], uint.Parse(a["layer"], CultureInfo.InvariantCulture), a["text"],
        int.Parse(a.GetValueOrDefault("newDepth", "0"), CultureInfo.InvariantCulture), a.GetValueOrDefault("tileSet", ""));

    private uint Id => uint.Parse(_args["layer"], CultureInfo.InvariantCulture);

    private float ReadFloat(Layer l) => _operation switch { XOffset => l.XOffset, YOffset => l.YOffset, HSpeed => l.HSpeed, _ => l.VSpeed };

    private void WriteFloat(Layer l, float value)
    {
        switch (_operation)
        {
            case XOffset: l.XOffset = value; break;
            case YOffset: l.YOffset = value; break;
            case HSpeed: l.HSpeed = value; break;
            default: l.VSpeed = value; break;
        }
    }

    private Layer.LayerData NewData(GameMakerData data, GameMakerRoom room, Layer layer)
    {
        switch (_operation)
        {
            case AddAssets:
                return new Layer.LayerAssetsData { LegacyTiles = [], Sprites = [], Sequences = [], ParticleSystems = [], TextItems = [] };
            case AddBackground:
                return new Layer.LayerBackgroundData { ParentLayer = layer };
            case AddTiles:
                var set = data.Backgrounds.FirstOrDefault(b => b.Name.Content == _args["tileSet"]) ?? throw new InvalidOperationException($"Tile set '{_args["tileSet"]}' does not exist.");
                uint columns = (uint)Math.Max(1, (int)Math.Ceiling((double)room.Width / Math.Max(1, set.GMS2TileWidth))), rows = (uint)Math.Max(1, (int)Math.Ceiling((double)room.Height / Math.Max(1, set.GMS2TileHeight)));
                var tiles = new uint[rows][];
                for (var y = 0; y < rows; y++) tiles[y] = new uint[columns];
                return new Layer.LayerTilesData { ParentLayer = layer, Background = set, TilesX = columns, TilesY = rows, TileData = tiles };
            default:
                return new Layer.LayerInstancesData();
        }
    }

    public override void Apply(GameMakerData data)
    {
        var room = data.Rooms.FirstOrDefault(r => r.Name.Content == _room) ?? throw new InvalidOperationException($"Room '{_room}' does not exist.");
        var layers = room.Layers ?? throw new InvalidOperationException($"Room '{_room}' has no layers.");
        if (_operation is AddInstances or AddAssets or AddBackground or AddTiles)
        {
            if (layers.Any(l => l.LayerName.Content == _text)) throw new InvalidOperationException($"'{_text}' is already a layer name.");
            var id = Id != 0 ? Id : layers.Select(l => l.LayerId).DefaultIfEmpty().Max() + 1;
            _args["layer"] = id.ToString(CultureInfo.InvariantCulture);
            var type = _operation switch { AddAssets => LayerType.Assets, AddBackground => LayerType.Background, AddTiles => LayerType.Tiles, _ => LayerType.Instances };
            _layer = new Layer { LayerName = data.Strings.MakeString(_text, out _), LayerId = id, LayerType = type, LayerDepth = int.Parse(_args["newDepth"], CultureInfo.InvariantCulture), IsVisible = true, ParentRoom = room };
            _layer.Data = NewData(data, room, _layer);
            _index = layers.Count;
            layers.Add(_layer);
            return;
        }

        _layer = layers.FirstOrDefault(l => l.LayerId == Id) ?? throw new InvalidOperationException($"Room '{_room}' has no layer {Id}.");
        _index = layers.IndexOf(_layer);
        switch (_operation)
        {
            case Visible:
                _oldText = _layer.IsVisible ? "1" : "0";
                _layer.IsVisible = _text == "1";
                break;
            case Rename:
                if (layers.Any(l => l != _layer && l.LayerName.Content == _text)) throw new InvalidOperationException($"'{_text}' is already a layer name.");
                _oldText = _layer.LayerName.Content ?? "";
                _layer.LayerName = data.Strings.MakeString(_text, out _);
                break;
            case XOffset or YOffset or HSpeed or VSpeed:
                _oldText = ReadFloat(_layer).ToString("R", CultureInfo.InvariantCulture);
                WriteFloat(_layer, float.Parse(_text, CultureInfo.InvariantCulture));
                break;
            case Depth:
                _oldText = _layer.LayerDepth.ToString(CultureInfo.InvariantCulture);
                _layer.LayerDepth = Math.Clamp(int.Parse(_text, CultureInfo.InvariantCulture), -16000, 16000);
                break;
            case FitTiles:
                var tiles = _layer.TilesData ?? throw new InvalidOperationException("Only a tile layer can be fitted to the room.");
                var tileSet = tiles.Background ?? throw new InvalidOperationException("The tile layer has no tile set.");
                _oldTiles = (tiles.TileData, tiles.TilesX, tiles.TilesY);
                var columns = (uint)Math.Max(1, (int)Math.Ceiling((double)room.Width / Math.Max(1, tileSet.GMS2TileWidth)));
                var rowCount = (uint)Math.Max(1, (int)Math.Ceiling((double)room.Height / Math.Max(1, tileSet.GMS2TileHeight)));
                var fitted = new uint[rowCount][];
                for (var y = 0; y < rowCount; y++)
                {
                    fitted[y] = new uint[columns];
                    if (y < tiles.TileData.Length) Array.Copy(tiles.TileData[y], fitted[y], Math.Min(columns, tiles.TileData[y].Length));
                }

                (tiles.TileData, tiles.TilesX, tiles.TilesY) = (fitted, columns, rowCount);
                tiles.TileDataUpdated();
                break;
            case Delete:
                _removed = [];
                if (_layer.InstancesData is { } instances && room.GameObjects is { } master)
                {
                    foreach (var instance in instances.Instances)
                    {
                        var at = master.IndexOf(instance);
                        if (at >= 0) _removed.Add((at, instance));
                    }

                    foreach (var (at, _) in _removed.OrderByDescending(r => r.Index)) master.RemoveAt(at);
                }

                layers.RemoveAt(_index);
                break;
            default:
                throw new InvalidDataException($"Unknown layer operation '{_operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        var room = data.Rooms.FirstOrDefault(r => r.Name.Content == _room) ?? throw new InvalidOperationException($"Room '{_room}' does not exist.");
        var layers = room.Layers ?? throw new InvalidOperationException($"Room '{_room}' has no layers.");
        if (_layer is null) throw new InvalidOperationException("The edit was never applied.");
        switch (_operation)
        {
            case Visible:
                _layer.IsVisible = _oldText == "1";
                break;
            case Rename:
                _layer.LayerName = data.Strings.MakeString(_oldText, out _);
                break;
            case XOffset or YOffset or HSpeed or VSpeed:
                WriteFloat(_layer, float.Parse(_oldText, CultureInfo.InvariantCulture));
                break;
            case Depth:
                _layer.LayerDepth = int.Parse(_oldText, CultureInfo.InvariantCulture);
                break;
            case FitTiles when _layer.TilesData is { } restored && _oldTiles is { } old:
                (restored.TileData, restored.TilesX, restored.TilesY) = old;
                restored.TileDataUpdated();
                break;
            case Delete:
                layers.Insert(_index, _layer);
                foreach (var (at, instance) in _removed.OrderBy(r => r.Index)) room.GameObjects.Insert(at, instance);
                break;
            default:
                layers.Remove(_layer);
                break;
        }
    }
}
#pragma warning restore CA1001
