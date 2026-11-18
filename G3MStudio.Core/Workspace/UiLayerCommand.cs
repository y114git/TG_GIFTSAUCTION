using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds, deletes or edits a node of the UI layers of the data file (the UILR chunk, shared by all rooms): a UI layer, or a flex panel, text item, sprite,
/// game object or sequence inside a layer or panel; or sets one typed property of a node. A node is found by the names from its UI layer down, joined with a slash. Undo puts the tree back as it was.
/// </summary>
public sealed class UiLayerCommand : EditCommand
{
    public const string Name = "uiLayer";
    public const string AddUi = "addUi";
    public const string AddLayer = "addLayer";
    public const string AddPanel = "addPanel";
    public const string AddText = "addText";
    public const string AddSprite = "addSprite";
    public const string AddObject = "addObject";
    public const string AddSequence = "addSequence";
    public const string SetProp = "setProp";
    public const string Delete = "delete";

    private readonly string _operation, _path, _name, _ref, _value;
    private Action? _undo;

    /// <param name="operation">One of the operation constants.</param>
    /// <param name="path">The parent node for an add (empty for a new UI layer), the node itself for a delete.</param>
    /// <param name="name">The name of the new node; for <see cref="SetProp"/> the property.</param>
    /// <param name="reference">The sprite, object or sequence a new node shows.</param>
    /// <param name="value">The new text of a property.</param>
    public UiLayerCommand(string operation, string path, string name = "", string reference = "", string value = "") =>
        (_operation, _path, _name, _ref, _value) = (operation, path, name, reference, value);

    public override string Kind => Name;

    public override string Editor => "uiLayers";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string> { ["operation"] = _operation, ["path"] = _path, ["name"] = _name, ["ref"] = _ref, ["value"] = _value };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("uiLayers", _path.Split('/')[0] is { Length: > 0 } root ? root : _name)];

    public static UiLayerCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["operation"], a["path"], a["name"], a.GetValueOrDefault("ref", ""), a.GetValueOrDefault("value", ""));

    /// <summary>The name of a node, or null for a kind that has none (an effect layer).</summary>
    public static string? NameOf(IGameMakerUINodeData node) => node switch
    {
        GameMakerUILayer l => l.Name?.Content,
        GameMakerUIFlexPanel p => p.Name?.Content,
        GameMakerUITextItemInstance t => t.Name?.Content,
        GameMakerUISpriteInstance s => s.Name?.Content,
        GameMakerUISequenceInstance q => q.Name?.Content,
        GameMakerUIGameObject o => o.ObjectDefinition?.Name?.Content,
        _ => null,
    };

    /// <summary>The children of a node; null when the node cannot hold children.</summary>
    public static IList<GameMakerUINode>? ChildrenOf(IGameMakerUINodeData node) => (node as GameMakerUIContainer)?.Children;

    internal static GameMakerUINode? NodeAt(GameMakerData data, string path)
    {
        GameMakerUINode? node = null;
        foreach (var name in path.Split('/'))
        {
            var list = node is null ? data.FORM.UILR?.List.Cast<GameMakerUINode>() : ChildrenOf(node.Node);
            node = list?.FirstOrDefault(n => NameOf(n.Node) == name);
            if (node is null) return null;
        }

        return node;
    }

    private static Action Cut<T>(IList<T> list, string leaf, string path)
        where T : GameMakerUINode
    {
        var at = list.ToList().FindIndex(n => NameOf(n.Node) == leaf);
        if (at < 0) throw new InvalidOperationException($"UI node '{path}' does not exist.");
        var node = list[at];
        list.RemoveAt(at);
        return () => list.Insert(at, node);
    }

    public override void Apply(GameMakerData data)
    {
        if (_operation == Delete)
        {
            var cut = _path.LastIndexOf('/');
            var missing = new InvalidOperationException($"UI node '{_path}' does not exist.");
            _undo = cut < 0
                ? Cut(data.FORM.UILR?.List ?? throw missing, _path, _path)
                : Cut(ChildrenOf((NodeAt(data, _path[..cut]) ?? throw missing).Node) ?? throw missing, _path[(cut + 1)..], _path);
            return;
        }

        if (_operation == SetProp)
        {
            var target = NodeAt(data, _path)?.Node ?? throw new InvalidOperationException($"UI node '{_path}' does not exist.");
            var old = UiLayerProps.Set(target, _name, _value);
            _undo = () => UiLayerProps.Restore(target, _name, old);
            return;
        }

        var nodeName = _operation == AddObject ? _ref : _name;
        if (nodeName.Length == 0 || nodeName.Contains('/', StringComparison.Ordinal)) throw new InvalidOperationException("A UI node needs a name without a slash.");
        if (_operation == AddLayer)
        {
            var created = data.FORM.UILR is null;
            var chunk = data.FORM.UILR ?? new GameMakerChunkUILR();
            if (created) data.FORM.Chunks["UILR"] = chunk;
            var roots = chunk.List;
            if (roots.Any(r => NameOf(r.Node) == _name))
            {
                if (created) data.FORM.Chunks.Remove("UILR");
                throw new InvalidOperationException($"A UI layer named '{_name}' exists already.");
            }

            var root = new GameMakerUIRootNode { Node = new GameMakerUILayer { Name = data.Strings.MakeString(_name, out _), Visible = true } };
            roots.Add(root);
            _undo = () =>
            {
                roots.Remove(root);
                if (created) data.FORM.Chunks.Remove("UILR");
            };
            return;
        }

        var children = (NodeAt(data, _path) is { } parent ? ChildrenOf(parent.Node) : null) ?? throw new InvalidOperationException($"UI node '{_path}' cannot hold children.");
        if (children.Any(c => NameOf(c.Node) == nodeName)) throw new InvalidOperationException($"'{_path}' has a child named '{nodeName}' already.");
        IGameMakerUINodeData made = _operation switch
        {
            AddPanel => new GameMakerUIFlexPanel { Name = data.Strings.MakeString(_name, out _) },
            AddText => new GameMakerUITextItemInstance { Name = data.Strings.MakeString(_name, out _), Visible = true, ScaleX = 1, ScaleY = 1 },
            AddSprite => new GameMakerUISpriteInstance
            {
                Name = data.Strings.MakeString(_name, out _),
                Visible = true,
                ScaleX = 1,
                ScaleY = 1,
                Color = 0xFFFFFFFF,
                Sprite = data.Sprites.FirstOrDefault(s => s.Name?.Content == _ref) ?? throw new InvalidOperationException($"There is no sprite named '{_ref}'."),
            },
            AddObject => new GameMakerUIGameObject
            {
                Visible = true,
                ScaleX = 1,
                ScaleY = 1,
                Color = 0xFFFFFFFF,
                ObjectDefinition = data.GameObjects.FirstOrDefault(o => o.Name?.Content == _ref) ?? throw new InvalidOperationException($"There is no object named '{_ref}'."),
            },
            AddSequence => new GameMakerUISequenceInstance
            {
                Name = data.Strings.MakeString(_name, out _),
                Visible = true,
                ScaleX = 1,
                ScaleY = 1,
                Color = 0xFFFFFFFF,
                Sequence = data.Sequences.FirstOrDefault(s => s.Name?.Content == _ref) ?? throw new InvalidOperationException($"There is no sequence named '{_ref}'."),
            },
            _ => throw new InvalidDataException($"Unknown UI layer operation '{_operation}'."),
        };
        var node = new GameMakerUINode { Node = made };
        children.Add(node);
        _undo = () => children.Remove(node);
    }

    public override void Revert(GameMakerData data)
    {
        if (_undo is null) throw new InvalidOperationException("The edit was never applied.");
        _undo();
    }
}
