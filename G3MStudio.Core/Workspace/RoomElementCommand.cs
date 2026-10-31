using System.Collections;
using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Core.Workspace;

/// <summary>Reads what the room editor needs from the things a room can hold: object instances and the elements of asset layers (sprites, sequences, particle systems, text items).</summary>
public static class RoomElements
{
    public const string Sprite = "sprite";
    public const string Sequence = "sequence";
    public const string Particle = "particle";
    public const string Text = "text";

    public static int X(object element) => element switch { GameObject o => o.X, SpriteInstance s => s.X, SequenceInstance q => q.X, ParticleSystemInstance p => p.X, TextItemInstance t => t.X, _ => 0 };

    public static int Y(object element) => element switch { GameObject o => o.Y, SpriteInstance s => s.Y, SequenceInstance q => q.Y, ParticleSystemInstance p => p.Y, TextItemInstance t => t.Y, _ => 0 };

    public static string? KindOf(object element) => element switch
    {
        SpriteInstance => Sprite,
        SequenceInstance => Sequence,
        ParticleSystemInstance => Particle,
        TextItemInstance => Text,
        _ => null,
    };

    public static string? NameOf(object element) => element switch
    {
        SpriteInstance s => s.Name?.Content,
        SequenceInstance q => q.Name?.Content,
        ParticleSystemInstance p => p.Name?.Content,
        TextItemInstance t => t.Name?.Content,
        GameObject o => o.ObjectDefinition?.Name.Content,
        _ => null,
    };

    /// <summary>The typed list inside an assets layer that holds elements of the given kind; null when the layer is not an assets layer.</summary>
    public static IList? ListOf(Layer layer, string kind) => kind switch
    {
        Sprite => layer.AssetsData?.Sprites,
        Sequence => layer.AssetsData?.Sequences,
        Particle => layer.AssetsData?.ParticleSystems,
        Text => layer.AssetsData?.TextItems,
        _ => null,
    };

    /// <summary>The assets layer that holds an element, or null (instances and tiles are not asset elements).</summary>
    public static Layer? LayerOf(GameMakerRoom room, object element) =>
        KindOf(element) is { } kind ? (room.Layers ?? []).FirstOrDefault(l => ListOf(l, kind)?.Contains(element) == true) : null;

    public static void SetPosition(object element, int x, int y)
    {
        switch (element)
        {
            case SpriteInstance s: (s.X, s.Y) = (x, y); break;
            case SequenceInstance q: (q.X, q.Y) = (x, y); break;
            case ParticleSystemInstance p: (p.X, p.Y) = (x, y); break;
            case TextItemInstance t: (t.X, t.Y) = (x, y); break;
        }
    }
}

/// <summary>An asset-layer element: the room, the layer id, the kind (see <see cref="RoomElements"/>) and the element's name.</summary>
public sealed record ElementRef(string Room, uint Layer, string Kind, string Name);

/// <summary>
/// Moves, deletes or adds one asset-layer element. Only sprite elements can be added (from a sprite name);
/// sequences, particle systems and text items can be moved and deleted.
/// </summary>
#pragma warning disable CA1001 // the element belongs to the layer or is put back into it; the model disposes it
public sealed class RoomElementCommand : EditCommand
{
    public const string Name = "roomElement";
    public const string Move = "move";
    public const string Delete = "delete";
    public const string Add = "add";

    private readonly ElementRef _target;
    private readonly string _operation;
    private readonly int _x;
    private readonly int _y;
    private readonly string _sprite;
    private object? _element;
    private IList? _list;
    private int _index;
    private (int X, int Y) _old;

    // sprite: for an add, the sprite the new element shows.
    public RoomElementCommand(ElementRef target, string operation, int x = 0, int y = 0, string sprite = "")
    {
        (_target, _operation, _x, _y, _sprite) = (target, operation, x, y, sprite);
        Args = new Dictionary<string, string>
        {
            ["room"] = target.Room,
            ["operation"] = operation,
            ["layer"] = target.Layer.ToString(CultureInfo.InvariantCulture),
            ["kind"] = target.Kind,
            ["name"] = target.Name,
            ["x"] = x.ToString(CultureInfo.InvariantCulture),
            ["y"] = y.ToString(CultureInfo.InvariantCulture),
            ["sprite"] = sprite,
        };
    }

    public override string Kind => Name;

    public override string Editor => "rooms";

    public override IReadOnlyDictionary<string, string> Args { get; }

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Rooms", _target.Room), new("Strings", null)];

    public static RoomElementCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(new ElementRef(a["room"], uint.Parse(a["layer"], CultureInfo.InvariantCulture), a["kind"], a["name"]), a["operation"],
        int.Parse(a["x"], CultureInfo.InvariantCulture), int.Parse(a["y"], CultureInfo.InvariantCulture), a["sprite"]);

    public override void Apply(GameMakerData data)
    {
        var room = data.Rooms.FirstOrDefault(r => r.Name.Content == _target.Room) ?? throw new InvalidOperationException($"Room '{_target.Room}' does not exist.");
        var layer = (room.Layers ?? []).FirstOrDefault(l => l.LayerId == _target.Layer) ?? throw new InvalidOperationException($"Room '{_target.Room}' has no layer {_target.Layer}.");
        _list = RoomElements.ListOf(layer, _target.Kind) ?? throw new InvalidOperationException($"Layer {_target.Layer} cannot hold {_target.Kind} elements.");
        if (_operation == Add)
        {
            if (_target.Kind != RoomElements.Sprite) throw new InvalidOperationException("Only sprite elements can be added.");
            var sprite = data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite) ?? throw new InvalidOperationException($"Sprite '{_sprite}' does not exist.");
            _element = new SpriteInstance { Name = data.Strings.MakeString(_target.Name, out _), Sprite = sprite, X = _x, Y = _y };
            _index = _list.Count;
            _list.Add(_element);
            return;
        }

        _element = _list.Cast<object>().FirstOrDefault(e => RoomElements.NameOf(e) == _target.Name) ?? throw new InvalidOperationException($"Layer {_target.Layer} has no {_target.Kind} element '{_target.Name}'.");
        _index = _list.IndexOf(_element);
        if (_operation == Move)
        {
            _old = (RoomElements.X(_element), RoomElements.Y(_element));
            RoomElements.SetPosition(_element, _x, _y);
        }
        else if (_operation == Delete)
        {
            _list.RemoveAt(_index);
        }
        else
        {
            throw new InvalidDataException($"Unknown element operation '{_operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        if (_element is null || _list is null) throw new InvalidOperationException("The edit was never applied.");
        switch (_operation)
        {
            case Move:
                RoomElements.SetPosition(_element, _old.X, _old.Y);
                break;
            case Delete:
                _list.Insert(_index, _element);
                break;
            default:
                _list.Remove(_element);
                break;
        }
    }
}
#pragma warning restore CA1001
