using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Gives a frame a picture of its own: a texture page that holds the picture, joined to the first texture group. For a frame whose picture other frames share, or whose
/// size changed (owner decisions Q59 and Q60), so painting it never changes another frame. No other page is packed again. Undo puts the old picture back and takes the page out.
/// </summary>
public sealed class FrameOwnPictureCommand(string sprite, int frame, byte[] picture) : EditCommand
{
    public const string Name = "frameOwnPicture";

    private GameMakerTexturePageItem? _before, _item;

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["picture"] = Convert.ToBase64String(picture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Sprites", sprite), new("EmbeddedTextures", null)];

    public static FrameOwnPictureCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sprite"], int.Parse(a["frame"], System.Globalization.CultureInfo.InvariantCulture), Convert.FromBase64String(a["picture"]));

    private GameMakerSprite.TextureEntry Entry(GameMakerData data)
    {
        var found = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        return found.Textures.ElementAtOrDefault(frame) ?? throw new InvalidOperationException($"Sprite '{sprite}' has no frame {frame}.");
    }

    public override void Apply(GameMakerData data)
    {
        var entry = Entry(data);
        var old = entry.Texture ?? throw new InvalidOperationException($"Frame {frame} of '{sprite}' has no picture.");
        using var image = ReplaceFrameCommand.ReadPicture(picture);
        var item = SpriteTemplate.AddItem(data, image);
        // The frame keeps its place inside the sprite's box; a larger picture grows the box of the frame.
        (item.TargetX, item.TargetY) = (old.TargetX, old.TargetY);
        item.BoundingWidth = (ushort)Math.Max(old.BoundingWidth, item.TargetX + item.TargetWidth);
        item.BoundingHeight = (ushort)Math.Max(old.BoundingHeight, item.TargetY + item.TargetHeight);
        (_before, _item) = (old, item);
        entry.Texture = item;
    }

    public override void Revert(GameMakerData data)
    {
        if (_before is null || _item is null) throw new InvalidOperationException("The edit was never applied.");
        Entry(data).Texture = _before;
        SpriteTemplate.RemoveItem(data, _item);
    }
}
