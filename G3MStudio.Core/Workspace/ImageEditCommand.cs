using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// One edit of the image editor on one frame of a sprite: the tool operations as text, run on the frame's picture. The journal stores the operations (and
/// the selection they used), so a replay after a crash makes the same pixels. The picture goes back through <see cref="ReplaceFrameCommand"/>, or through
/// <see cref="TextureFitCommand"/> when the frame is shared or the size changed. The live editor already has the result and passes it in, so applying is not done twice.
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
    private (Action<GameMakerData> Apply, Action<GameMakerData> Revert)? _inner; // the write of the flat picture into the page, and its undo
    private LayerSet? _before, _after;
    private long _pixelBytes;

    public override long RetainedBytes => base.RetainedBytes + _pixelBytes;

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
        _args = args;
    }

    public override string Kind => Name;

    public override string Editor => "sprites";

    private readonly Dictionary<string, string> _args;

    public override IReadOnlyDictionary<string, string> Args => _args;

    /// <summary>The advanced choices of the placement when the frame changes size (see <see cref="FitOptions"/>); the journal keeps them.</summary>
    public FitOptions? Fit
    {
        get;
        init
        {
            field = value;
            _args.With(value);
        }
    }

    public static ImageEditCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["sprite"], int.Parse(a["frame"], System.Globalization.CultureInfo.InvariantCulture),
        a.TryGetValue("selection", out var selection) ? PixelSelection.FromText(selection) : null, [.. a["ops"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(ImageOp.Parse)],
        layer: a.TryGetValue("layer", out var layer) ? int.Parse(layer, System.Globalization.CultureInfo.InvariantCulture) : -1)
    {
        Fit = FitOptions.From(a),
    };

    /// <summary>Runs the operations on a picture; the editor and the replay both call this.</summary>
    public static PixelBuffer Run(PixelBuffer start, PixelSelection? selection, IEnumerable<ImageOp> ops)
    {
        var state = new ImageState(start.Copy(), selection?.Copy());
        foreach (var op in ops) ImageOps.Run(state, op);
        return state.Picture;
    }

    private bool IsLayerEdit => _ops.Any(o => o.Kind == ImageOps.Layer);

    // These change the size of the whole picture, which the layers cannot follow one by one, so they flatten first.
    private static bool ChangesCanvasOf(PixelSelection? selection, IReadOnlyList<ImageOp> ops) => selection is null && ops.Any(o => o.Kind is ImageOps.Resize or ImageOps.Crop or ImageOps.Trim or ImageOps.Rotate or ImageOps.Rotate90 or ImageOps.Scale or ImageOps.Skew);

    public override void Apply(GameMakerData data)
    {
        var store = LayerStore.Of(data);
        if (_inner is null)
        {
            var found = data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite) ?? throw new InvalidOperationException($"Sprite '{_sprite}' does not exist.");
            var item = found.Textures.ElementAtOrDefault(_frame)?.Texture ?? throw new InvalidOperationException($"Sprite '{_sprite}' has no frame {_frame}.");
            var result = Build(data, store);
            // The old and new layers, plus the frame's old pixels.
            _pixelBytes = 4L * result.Width * result.Height * (1 + (_before?.Count ?? 0) + (_after?.Count ?? 0));
            var shared = data.Sprites.SelectMany(s => s.Textures).Count(t => ReferenceEquals(t.Texture, item)) > 1 || TextureFitEdit.Shared(data, item);
            _result = null; // the live editor keeps drawing on its own picture
            // A lock, a rename or a change of a hidden layer leaves the frame as it looks: the page is not written at all.
            if (IsLayerEdit && _before is not null && LayerSet.SameLook(_before.Flat, result)) _inner = (_ => { }, _ => { });
            else if (!shared && (result.Width, result.Height) == (item.SourceWidth, item.SourceHeight) && PagePixels.Holds(item.TexturePage, item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight))
            {
                var write = new FramePixels(item, result);
                _inner = (_ => write.Apply(), _ => write.Revert());
            }
            else
            {
                var png = FramePicture.ToPng(result);
                // A new size, or a frame other frames share, finds its room on the texture pages (free space, a packed or grown page, a new page only when nothing else fits).
                EditCommand command = !shared && (result.Width, result.Height) == (item.SourceWidth, item.SourceHeight) ? new ReplaceFrameCommand(_sprite, _frame, png) : new TextureFitCommand("sprites", _sprite, _frame, png, Fit);
                _inner = (command.Apply, command.Revert);
            }
        }

        _inner.Value.Apply(data);
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

    private LayerSet Draw(LayerSet before) => Draw(before, _selection, _ops, _result, _layer);

    /// <summary>The layers after pixel operations drew on one layer; the image editor's local edits use it to show what an edit will make before it is applied.</summary>
    public static LayerSet Draw(LayerSet before, PixelSelection? selection, IReadOnlyList<ImageOp> ops, PixelBuffer? result, int layer)
    {
        var start = ChangesCanvasOf(selection, ops) ? LayerSet.OneLayer(before.Flat) : before;
        var at = layer < 0 ? start.Count - 1 : Math.Min(layer, start.Count - 1);
        var pixels = !ChangesCanvasOf(selection, ops) && result is { } live && live.Width == start.Width && live.Height == start.Height ? live : Run(start.Layers[at].Pixels, selection, ops);
        return start.With(at, start.Layers[at] with { Pixels = pixels });
    }

    public override void Revert(GameMakerData data)
    {
        (_inner ?? throw new InvalidOperationException("The edit was never applied.")).Revert(data);
        LayerStore.Of(data).Set(_sprite, _frame, _before);
    }
}

/// <summary>
/// Writes a picture of the size of a page item into its place on the page, without encoding anything (see <see cref="PagePixels"/>); undo writes the old pixels back.
/// Used inside <see cref="ImageEditCommand"/> only, so it is never in the journal on its own.
/// </summary>
internal sealed class FramePixels(GameMakerTexturePageItem item, PixelBuffer picture)
{
    private PixelBuffer? _before;
    private (ushort Width, ushort Height) _target;

    public void Apply()
    {
        _before ??= PagePixels.Read(item.TexturePage, item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight);
        _target = (item.TargetWidth, item.TargetHeight);
        PagePixels.Write(item.TexturePage, [(item.SourceX, item.SourceY, picture)]);
        (item.TargetWidth, item.TargetHeight) = ((ushort)picture.Width, (ushort)picture.Height);
    }

    public void Revert()
    {
        PagePixels.Write(item.TexturePage, [(item.SourceX, item.SourceY, _before ?? throw new InvalidOperationException("The frame was never written."))]);
        (item.TargetWidth, item.TargetHeight) = _target;
    }
}
