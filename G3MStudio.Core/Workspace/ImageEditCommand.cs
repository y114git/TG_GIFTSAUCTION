using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// One edit of the image editor on one frame of a sprite: the tool operations as text, run on the frame's picture. The journal stores the operations (and
/// the selection they used), so a replay after a crash makes the same pixels. The picture goes back through <see cref="ReplaceFrameCommand"/>, or through
/// <see cref="FrameOwnPictureCommand"/> when the frame is shared or the size changed. The live editor already has the result and passes it in, so applying is not done twice.
/// </summary>
public sealed class ImageEditCommand : EditCommand
{
    public const string Name = "imageEdit";

    private readonly string _sprite;
    private readonly int _frame;
    private readonly int _layer;
    private readonly PixelSelection? _selection;
    private readonly IReadOnlyList<ImageOp> _ops;
    private PixelBuffer? _result;
    private EditCommand? _inner;
    private LayerSet? _before, _after;

    // layer: the layer the pixel operations draw on, bottom is 0; -1 for the top one.
    public ImageEditCommand(string sprite, int frame, PixelSelection? selection, IReadOnlyList<ImageOp> ops, PixelBuffer? result = null, int layer = -1)
    {
        (_sprite, _frame, _selection, _ops, _result, _layer) = (sprite, frame, selection, ops, result, layer);
        var args = new Dictionary<string, string>
        {
            ["sprite"] = sprite,
            ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ops"] = string.Join('\n', ops),
        };
        if (selection is not null) args["selection"] = selection.ToText();
        if (layer >= 0) args["layer"] = layer.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Args = args;
    }

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; }

    public static ImageEditCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["sprite"], int.Parse(a["frame"], System.Globalization.CultureInfo.InvariantCulture),
        a.TryGetValue("selection", out var selection) ? PixelSelection.FromText(selection) : null, [.. a["ops"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(ImageOp.Parse)]);

    /// <summary>Runs the operations on a picture; the editor and the replay both call this.</summary>
    public static PixelBuffer Run(PixelBuffer start, PixelSelection? selection, IEnumerable<ImageOp> ops)
    {
        var state = new ImageState(start.Copy(), selection?.Copy());
        foreach (var op in ops) ImageOps.Run(state, op);
        return state.Picture;
    }

    private bool IsLayerEdit => _ops.Any(o => o.Kind == ImageOps.Layer);

    // These change the size of the whole picture, which the layers cannot follow one by one, so they flatten first.
    private bool ChangesCanvas => _selection is null && _ops.Any(o => o.Kind is ImageOps.Resize or ImageOps.Crop or ImageOps.Trim or ImageOps.Rotate90 or ImageOps.Scale or ImageOps.Skew);

    public override void Apply(GameMakerData data)
    {
        var store = LayerStore.Of(data);
        if (_inner is null)
        {
            var found = data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite) ?? throw new InvalidOperationException($"Sprite '{_sprite}' does not exist.");
            var item = found.Textures.ElementAtOrDefault(_frame)?.Texture ?? throw new InvalidOperationException($"Sprite '{_sprite}' has no frame {_frame}.");
            var result = Build(data, store);
            var shared = data.Sprites.SelectMany(s => s.Textures).Count(t => ReferenceEquals(t.Texture, item)) > 1;
            var png = FramePicture.ToPng(result);
            _result = null; // the live editor keeps drawing on its own picture
            _inner = !shared && (result.Width, result.Height) == (item.SourceWidth, item.SourceHeight) ? new ReplaceFrameCommand(_sprite, _frame, png) : new FrameOwnPictureCommand(_sprite, _frame, png);
        }

        _inner.Apply(data);
        store.Set(_sprite, _frame, _after);
    }

    /// <summary>Works out the layers after the edit and returns the flat picture to store in the data file. Without layers it is the plain old path.</summary>
    private PixelBuffer Build(GameMakerData data, LayerStore store)
    {
        if (!IsLayerEdit && !store.Has(_sprite, _frame)) return _result ??= Run(FramePicture.Read(data, _sprite, _frame), _selection, _ops);
        _before = store.Read(data, _sprite, _frame);
        _after = IsLayerEdit ? _ops.Aggregate(_before, (stack, op) => stack.Apply(op)) : Draw(_before);
        return _after.Flat;
    }

    private LayerSet Draw(LayerSet before)
    {
        var start = ChangesCanvas ? LayerSet.OneLayer(before.Flat) : before;
        var at = _layer < 0 ? start.Count - 1 : Math.Min(_layer, start.Count - 1);
        var pixels = !ChangesCanvas && _result is { } live && live.Width == start.Width && live.Height == start.Height ? live : Run(start.Layers[at].Pixels, _selection, _ops);
        return start.With(at, start.Layers[at] with { Pixels = pixels });
    }

    public override void Revert(GameMakerData data)
    {
        (_inner ?? throw new InvalidOperationException("The edit was never applied.")).Revert(data);
        LayerStore.Of(data).Set(_sprite, _frame, _before);
    }
}
