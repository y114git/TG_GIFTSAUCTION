using G3MLib.DataFile;
using G3MLib.DataFile.Util;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Replaces the picture of a whole embedded texture page. The new picture must have the size of the page, because every sprite frame, font glyph and tile
/// of the page is a rectangle on it. Undo puts the old picture back. The picture is stored in the journal.
/// </summary>
public sealed class ReplacePageCommand(string page, byte[] picture) : EditCommand
{
    public const string Name = "replacePage";

    private GMImage? _before;
    private GMImage? _after;

    public override string Kind => Name;

    public override string Editor => "embeddedTextures";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["page"] = page, ["picture"] = Convert.ToBase64String(picture) };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("EmbeddedTextures", page)];

    public static ReplacePageCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["page"], Convert.FromBase64String(a["picture"]));

    /// <summary>The page as a PNG, for saving to a file.</summary>
    public static byte[] ReadPng(GameMakerData data, string page)
    {
        var texture = Find(data, page);
        using var worker = new TextureWorker();
        using var image = worker.GetEmbeddedTexture(texture);
        return image.ToByteArray(MagickFormat.Png);
    }

    private static G3MLib.DataFile.Models.GameMakerEmbeddedTexture Find(GameMakerData data, string page) =>
        data.EmbeddedTextures.FirstOrDefault(t => t.Name.Content == page) ?? throw new InvalidOperationException($"Texture page '{page}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var texture = Find(data, page);
        if (texture.TextureData is not { } pixels || pixels.Image is not { } current) throw new InvalidOperationException($"Texture page '{page}' has no picture to replace.");
        if (_after is null)
        {
            using var image = ReplaceFrameCommand.ReadPicture(picture);
            if (image.Width != (uint)current.Width || image.Height != (uint)current.Height)
                throw new InvalidOperationException($"The picture is {image.Width}x{image.Height}; the page is {current.Width}x{current.Height}. Use a picture of the page's size.");
            _after = GMImage.FromMagickImage(image).ConvertToFormat(current.Format);
        }

        _before = current;
        pixels.Image = _after;
    }

    public override void Revert(GameMakerData data)
    {
        var texture = Find(data, page);
        if (texture.TextureData is not { } pixels) throw new InvalidOperationException($"Texture page '{page}' has no picture.");
        pixels.Image = _before ?? throw new InvalidOperationException("The page was never replaced.");
    }
}
