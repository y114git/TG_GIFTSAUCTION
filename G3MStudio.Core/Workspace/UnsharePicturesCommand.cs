using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Gives the pictures of a sprite, font or tile set new texture page items with a copy of the pixels, placed with <see cref="TextureFit"/>. A resource made by the library clone
/// shares its items with the source, so this keeps an edit of the copy from changing the original. Frames that shared one picture keep sharing one.
/// </summary>
public sealed class UnsharePicturesCommand(string type, string name) : TextureFitEdit
{
    public const string Name = "unsharePictures";

    private List<List<GameMakerSprite.TextureEntry>> _bound = [];

    public override string Kind => Name;

    public override string Editor => type;

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["type"] = type, ["name"] = name };

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static UnsharePicturesCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["type"], a["name"]);

    protected override (IReadOnlyList<FitRequest> Requests, IReadOnlyList<PixelBuffer?> Pictures, GameMakerEmbeddedTexture? Page, (int Width, int Height) Size) Prepare(GameMakerData data)
    {
        List<(GameMakerTexturePageItem Item, List<GameMakerSprite.TextureEntry> Frames)> items = ResourceTypes.Find(data, ResourceTypes.KeyOf(type, name)) switch
        {
            GameMakerSprite sprite => [.. sprite.Textures.Where(t => t.Texture is not null).GroupBy(t => t.Texture, ReferenceEqualityComparer.Instance).Select(g => (g.Select(t => t.Texture).OfType<GameMakerTexturePageItem>().First(), g.ToList()))],
            GameMakerFont { Texture: { } texture } => [(texture, [])],
            GameMakerBackground { Texture: { } texture } => [(texture, [])],
            _ => throw new InvalidOperationException($"'{name}' was not found in {type}, or has no picture."),
        };
        _bound = [.. items.Select(i => i.Frames)];
        var pictures = items.Select(i => FramePicture.ReadItem(i.Item, name)).ToList();
        return ([.. items.Zip(pictures, (i, p) => new FitRequest(null, p.Width, p.Height, i.Item))], [.. pictures], null, default);
    }

    protected override bool KeepsBox(GameMakerData data) => false;

    protected override void Bind(GameMakerData data, int index, GameMakerTexturePageItem item)
    {
        switch (ResourceTypes.Find(data, ResourceTypes.KeyOf(type, name)))
        {
            case GameMakerSprite:
                foreach (var frame in _bound[index]) frame.Texture = item;
                break;
            case GameMakerFont font:
                font.Texture = item;
                break;
            case GameMakerBackground background:
                background.Texture = item;
                break;
        }
    }
}
