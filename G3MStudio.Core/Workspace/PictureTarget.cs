using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// A picture the image editor can open: where its pixels come from, the largest picture that fits without packing the pages again, and the undoable command that
/// writes an edited picture back. A sprite frame, a texture page item and a whole texture page each have one, so the editor itself is the same for all of them.
/// </summary>
public interface IPictureTarget
{
    string Title { get; }

    /// <summary>Language key of the limit shown in the editor's status line ({w} and {h} are the <see cref="Limit"/>), or null when the target has none.</summary>
    string? NoteKey => null;

    PixelBuffer Read(GameMakerData data);

    /// <summary>The size of the rectangle the picture must stay inside, or null when the target takes any size.</summary>
    (int Width, int Height)? Limit(GameMakerData data);

    /// <summary>The command that stores the picture. Throws <see cref="RiskyRefusalException"/> for a size the target cannot take unless <paramref name="force"/> is set.</summary>
    EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force);

    /// <summary>
    /// As <see cref="Apply(GameMakerData, PixelBuffer, bool)"/>, knowing where the top left of the picture last applied lies in <paramref name="picture"/> after canvas
    /// changes (null after a scale, rotation or skew of the whole picture). Only a font sheet needs it, to move its glyph rectangles along.
    /// </summary>
    EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force, (int X, int Y)? shift, FitOptions? fit = null) => Apply(data, picture, force);

    /// <summary>Where a picture of the given size would go on the texture pages, without changing anything; null for a target that is not a page item. <paramref name="fit"/> holds the Advanced choices.</summary>
    IReadOnlyList<FitStep>? Plan(GameMakerData data, int width, int height, FitOptions? fit = null) => null;
}

public static class PictureTargets
{
    /// <summary>The target a tab key names (sprites, fonts, tileSets, texturePageItems, embeddedImages, embeddedTextures), or null for any other type.</summary>
    public static IPictureTarget? For(string type, string name, int frame = 0, string? dataFile = null) => type switch
    {
        "sprites" => new FrameTarget(name, frame),
        "spriteMasks" when name.LastIndexOf('#') is var cut and > 0 && int.TryParse(name[(cut + 1)..], out var index) => new MaskTarget(name[..cut], index),
        "fontGlyphs" when name.LastIndexOf('#') is var at and > 0 && int.TryParse(name[(at + 1)..], out var glyph) => new GlyphTarget(name[..at], glyph),
        "includedFiles" when dataFile is not null && IncludedPictureCommand.IsPicture(name) => new IncludedTarget(dataFile, name),
        "embeddedTextures" => new PageTarget(name),
        _ when ReplaceItemCommand.Types.Contains(type) => new ItemTarget(type, name),
        _ => null,
    };
}

/// <summary>One frame of a sprite. A picture of another size becomes the frame's own picture, so no rectangle limits it.</summary>
public sealed class FrameTarget(string sprite, int frame) : IPictureTarget
{
    public string Title => sprite;

    public PixelBuffer Read(GameMakerData data) => FramePicture.Read(data, sprite, frame);

    public (int Width, int Height)? Limit(GameMakerData data) => null;

    public EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force)
    {
        var item = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite)?.Textures.ElementAtOrDefault(frame)?.Texture ?? throw new InvalidOperationException($"Sprite '{sprite}' has no frame {frame}.");
        var png = FramePicture.ToPng(picture);
        return (picture.Width, picture.Height) == (item.SourceWidth, item.SourceHeight) ? new ReplaceFrameCommand(sprite, frame, png) : new TextureFitCommand("sprites", sprite, frame, png);
    }
}

/// <summary>
/// The picture of one texture page item (a tile set, a font sheet, an embedded image or a bare item). A picture of another size finds its room on the texture pages
/// (<see cref="TextureFitCommand"/>). A font sheet does too: its glyph rectangles are measured from the item, so they stay valid where the item goes, and a canvas
/// change on the top or left side moves them along (<see cref="FontSheet"/>). The Advanced choices (page, growing, new page, largest side) apply to all of them.
/// </summary>
public sealed class ItemTarget(string type, string name) : IPictureTarget
{
    public string Title => name;

    public PixelBuffer Read(GameMakerData data) => FramePicture.ReadItem(ReplaceItemCommand.ItemOf(data, type, name), name);

    public string? NoteKey => type == "fonts" ? "image.limitFont" : null;

    public (int Width, int Height)? Limit(GameMakerData data) => null;

    public IReadOnlyList<FitStep>? Plan(GameMakerData data, int width, int height, FitOptions? fit = null) => TextureFit.Plan(data, [new FitRequest(ReplaceItemCommand.ItemOf(data, type, name), width, height)], options: fit);

    public EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force) => Apply(data, picture, force, (0, 0));

    public EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force, (int X, int Y)? shift, FitOptions? fit = null)
    {
        var item = ReplaceItemCommand.ItemOf(data, type, name);
        var png = FramePicture.ToPng(picture);
        EditCommand write = (picture.Width, picture.Height) != (item.SourceWidth, item.SourceHeight) ? new TextureFitCommand(type, name, 0, png, fit) : new ReplaceItemCommand(type, name, png);
        // tiles are cut from a grid that starts at the top left of the picture: only a canvas grown to the right or down keeps every tile where it was
        if (type == "tileSets" && !force && (shift != (0, 0) || picture.Width < item.SourceWidth || picture.Height < item.SourceHeight))
        {
            var change = shift switch { null => ", scaled, rotated or skewed", (0, 0) => "", var (x, y) => $", moved by {x},{y}" };
            throw new RiskyRefusalException("risky.tileGridTitle", "risky.tileGrid",
                $"Tile set '{name}' cuts its tiles from a grid that starts at the top left of its {item.SourceWidth}x{item.SourceHeight} picture. The new picture is {picture.Width}x{picture.Height}{change}.");
        }

        if (type != "fonts") return write;
        FontSheet.Check(data.Fonts.First(f => f.Name.Content == name), picture.Width, picture.Height, shift);
        var (dx, dy) = shift ?? (0, 0); // the check refused a null shift
        return (dx, dy) == (0, 0) ? write : new GroupCommand([write, new FontSheetShiftCommand(name, dx, dy)]);
    }
}

/// <summary>A whole embedded texture page; the picture must have the size of the page.</summary>
public sealed class PageTarget(string page) : IPictureTarget
{
    public string Title => page;

    public string? NoteKey => "image.limitPage";

    public PixelBuffer Read(GameMakerData data)
    {
        using var image = new MagickImage(ReplacePageCommand.ReadPng(data, page));
        image.Alpha(AlphaOption.Set);
        using var pixels = image.GetPixels();
        return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidOperationException("The page has no pixels."));
    }

    public (int Width, int Height)? Limit(GameMakerData data) =>
        (data.EmbeddedTextures.FirstOrDefault(t => t.Name.Content == page) ?? throw new InvalidOperationException($"Texture page '{page}' does not exist.")).TextureData?.Image is { } image ? (image.Width, image.Height) : null;

    public EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force) => new ReplacePageCommand(page, FramePicture.ToPng(picture), force);
}

/// <summary>One collision mask of a sprite as a black and white picture (white is solid). The size is fixed; it is written back through the mask import.</summary>
public sealed class MaskTarget(string sprite, int index) : IPictureTarget
{
    public string Title => $"{sprite} #{index}";

    public string? NoteKey => "image.limitMask";

    private GameMakerSprite.MaskEntry Entry(GameMakerData data) =>
        data.Sprites.FirstOrDefault(s => s.Name.Content == sprite)?.CollisionMasks.ElementAtOrDefault(index) ?? throw new InvalidOperationException($"Sprite '{sprite}' has no mask {index}.");

    public PixelBuffer Read(GameMakerData data)
    {
        var entry = Entry(data);
        using var image = new MagickImage(MaskPicture.ToPng(entry.Data, entry.Width, entry.Height));
        using var pixels = image.GetPixels();
        return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidOperationException("The mask has no pixels."));
    }

    public (int Width, int Height)? Limit(GameMakerData data) => (Entry(data).Width, Entry(data).Height);

    public EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force)
    {
        var entry = Entry(data);
        if ((picture.Width, picture.Height) != (entry.Width, entry.Height)) throw new InvalidOperationException($"The mask is {entry.Width}x{entry.Height}; the picture is {picture.Width}x{picture.Height}. A mask cannot change size.");
        return new ImportMaskCommand(sprite, index, MaskPicture.FromPicture(FramePicture.ToPng(picture), entry.Width, entry.Height));
    }
}

/// <summary>An image file in the Included Files folder; it is written back to the file, with a warning, as one undoable step.</summary>
public sealed class IncludedTarget(string dataFile, string relative) : IPictureTarget
{
    public string Title => relative;

    public string? NoteKey => "image.limitIncluded";

    public PixelBuffer Read(GameMakerData data)
    {
        using var image = ReplaceFrameCommand.ReadPicture(File.ReadAllBytes(new IncludedFiles(dataFile).Resolve(relative)));
        image.Alpha(AlphaOption.Set);
        using var pixels = image.GetPixels();
        return new PixelBuffer((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidOperationException("The picture has no pixels."));
    }

    public (int Width, int Height)? Limit(GameMakerData data) => null;

    public EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force) => new IncludedPictureCommand(dataFile, relative, FramePicture.ToPng(picture), force);
}

/// <summary>One glyph of a font: the rectangle it takes on the font's sheet. The size is fixed; the sheet is written back through the font's item command, so the edit is one undo step.</summary>
public sealed class GlyphTarget(string font, int glyph) : IPictureTarget
{
    public string Title => $"{font} #{glyph}";

    public string? NoteKey => "image.limitGlyph";

    private GameMakerFont.Glyph Entry(GameMakerData data) =>
        data.Fonts.FirstOrDefault(f => f.Name.Content == font)?.Glyphs.ElementAtOrDefault(glyph) ?? throw new InvalidOperationException($"Font '{font}' has no glyph {glyph}.");

    private (PixelBuffer Sheet, GameMakerFont.Glyph Glyph) Sheet(GameMakerData data)
    {
        var entry = Entry(data);
        var sheet = new ItemTarget("fonts", font).Read(data);
        if (entry.SourceX + entry.SourceWidth > sheet.Width || entry.SourceY + entry.SourceHeight > sheet.Height) throw new InvalidOperationException($"Glyph {glyph} of '{font}' lies outside the sheet.");
        return (sheet, entry);
    }

    public PixelBuffer Read(GameMakerData data)
    {
        var (sheet, g) = Sheet(data);
        var picture = new PixelBuffer(g.SourceWidth, g.SourceHeight);
        for (var y = 0; y < g.SourceHeight; y++)
            for (var x = 0; x < g.SourceWidth; x++) picture.Set(x, y, sheet.Get(g.SourceX + x, g.SourceY + y));
        return picture;
    }

    public (int Width, int Height)? Limit(GameMakerData data) => (Entry(data).SourceWidth, Entry(data).SourceHeight);

    public EditCommand Apply(GameMakerData data, PixelBuffer picture, bool force)
    {
        var (sheet, g) = Sheet(data);
        if ((picture.Width, picture.Height) != (g.SourceWidth, g.SourceHeight)) throw new InvalidOperationException($"The glyph is {g.SourceWidth}x{g.SourceHeight}; the picture is {picture.Width}x{picture.Height}. A glyph cannot change size here.");
        for (var y = 0; y < picture.Height; y++)
            for (var x = 0; x < picture.Width; x++) sheet.Set(g.SourceX + x, g.SourceY + y, picture.Get(x, y));
        return new ItemTarget("fonts", font).Apply(data, sheet, force);
    }
}
