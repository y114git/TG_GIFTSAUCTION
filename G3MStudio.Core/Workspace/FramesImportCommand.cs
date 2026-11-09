using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Puts pictures into a sprite as frames, each on a texture page of its own (like <see cref="FrameOwnPictureCommand"/>): they follow the last frame, or replace every frame.
/// Undo puts the old frame list back and takes the new pages out again. <see cref="Build"/> adds the size, margin and mask changes of a replace in one undo step.
/// </summary>
#pragma warning disable CA1001 // the page items belong to the data file, which disposes them
public sealed class FramesImportCommand(string sprite, IReadOnlyList<byte[]> pictures, bool replace) : EditCommand
{
    public const string Name = "framesImport";

    private GameMakerSprite.TextureEntry[] _before = [];
    private List<GameMakerTexturePageItem> _added = [];

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["pictures"] = string.Join('\n', pictures.Select(Convert.ToBase64String)),
        ["replace"] = replace ? "1" : "0",
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Sprites", sprite), new("EmbeddedTextures", null)];

    public static FramesImportCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["sprite"], [.. a["pictures"].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Convert.FromBase64String)], a["replace"] == "1");

    /// <summary>
    /// The edit that imports <paramref name="frames"/>. With <paramref name="replace"/> the sprite takes the frame size (box and margins) and its masks are built again from the new pictures;
    /// without it the frames must have the size of the sprite.
    /// </summary>
    public static EditCommand Build(GameMakerData data, string sprite, IReadOnlyList<PixelBuffer> frames, bool replace, long maxPngBytes = FramePicture.MaxPngBytes)
    {
        if (frames.Count == 0) throw new InvalidDataException("There are no frames to import.");
        var target = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        var (w, h) = (frames[0].Width, frames[0].Height);
        if (frames.Any(f => (f.Width, f.Height) != (w, h))) throw new InvalidDataException("The frames differ in size.");
        if (!replace && (w, h) != ((int)target.Width, (int)target.Height))
            throw new InvalidDataException($"The frames are {w} by {h} pixels and the sprite is {target.Width} by {target.Height}; replace the frames to take the new size.");
        var import = new FramesImportCommand(sprite, FramePicture.ToPngs(frames, maxPngBytes), replace);
        if (!replace) return import;
        SetPropertyCommand Set(string path, int value) => new("sprites", sprite, path, value.ToString(CultureInfo.InvariantCulture));
        return new GroupCommand([import, Set("Width", w), Set("Height", h), Set("MarginLeft", 0), Set("MarginTop", 0), Set("MarginRight", w - 1), Set("MarginBottom", h - 1), new RecalcMaskCommand(sprite)]);
    }

    private GameMakerSprite Find(GameMakerData data) => data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var target = Find(data);
        _before = [.. target.Textures];
        _added = [];
        try
        {
            foreach (var picture in pictures)
            {
                _added.Add(AddPicture(data, picture));
            }
        }
        catch
        {
            TakeOut(data);
            throw;
        }

        if (replace) target.Textures.Clear();
        foreach (var item in _added) target.Textures.Add(new GameMakerSprite.TextureEntry { Texture = item });
    }

    private static GameMakerTexturePageItem AddPicture(GameMakerData data, byte[] picture)
    {
        using var image = ReplaceFrameCommand.ReadPicture(picture);
        return SpriteTemplate.AddItem(data, image);
    }

    // The last page added goes first: a page leaves the end of its group's list.
    private void TakeOut(GameMakerData data)
    {
        foreach (var item in Enumerable.Reverse(_added)) SpriteTemplate.RemoveItem(data, item);
        _added = [];
    }

    public override void Revert(GameMakerData data)
    {
        var target = Find(data);
        target.Textures.Clear();
        foreach (var entry in _before) target.Textures.Add(entry);
        TakeOut(data);
    }
}
#pragma warning restore CA1001
