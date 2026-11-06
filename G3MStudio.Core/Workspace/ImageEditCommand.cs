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
    private readonly PixelSelection? _selection;
    private readonly IReadOnlyList<ImageOp> _ops;
    private PixelBuffer? _result;
    private EditCommand? _inner;

    public ImageEditCommand(string sprite, int frame, PixelSelection? selection, IReadOnlyList<ImageOp> ops, PixelBuffer? result = null)
    {
        (_sprite, _frame, _selection, _ops, _result) = (sprite, frame, selection, ops, result);
        var args = new Dictionary<string, string>
        {
            ["sprite"] = sprite,
            ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ops"] = string.Join('\n', ops),
        };
        if (selection is not null) args["selection"] = selection.ToText();
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

    public override void Apply(GameMakerData data)
    {
        if (_inner is null)
        {
            var found = data.Sprites.FirstOrDefault(s => s.Name.Content == _sprite) ?? throw new InvalidOperationException($"Sprite '{_sprite}' does not exist.");
            var item = found.Textures.ElementAtOrDefault(_frame)?.Texture ?? throw new InvalidOperationException($"Sprite '{_sprite}' has no frame {_frame}.");
            var result = _result ??= Run(FramePicture.Read(data, _sprite, _frame), _selection, _ops);
            var shared = data.Sprites.SelectMany(s => s.Textures).Count(t => ReferenceEquals(t.Texture, item)) > 1;
            var png = FramePicture.ToPng(result);
            _result = null; // the live editor keeps drawing on its own picture
            _inner = !shared && (result.Width, result.Height) == (item.SourceWidth, item.SourceHeight) ? new ReplaceFrameCommand(_sprite, _frame, png) : new FrameOwnPictureCommand(_sprite, _frame, png);
        }

        _inner.Apply(data);
    }

    public override void Revert(GameMakerData data) => (_inner ?? throw new InvalidOperationException("The edit was never applied.")).Revert(data);
}
