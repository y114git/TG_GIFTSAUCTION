using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>Where a glyph sits on the font's sheet and how far it moves the pen.</summary>
public readonly record struct GlyphRect(ushort X, ushort Y, ushort Width, ushort Height, short Shift, short Offset);

/// <summary>
/// Draws the glyphs of a font again from a TrueType or OpenType file at the font's size: every glyph the font already has is rendered (white on transparent),
/// packed into the font's own texture region and given its new rectangle and advance. The old sheet and the old glyph values are kept, so Undo puts them back.
/// shortcut: one sharp bitmap per glyph with no kerning and no signed distance field spread, and the glyphs must fit the existing region (a smaller size if not); upgrade when a font can get a bigger texture page.
/// </summary>
#pragma warning disable CA1001, CA2000 // the images belong to the page, which the model disposes; a prepared drawing is disposed when it is applied or replaced
public sealed partial class FontReplaceCommand(string font, byte[] fontFile) : EditCommand
{
    public const string Name = "fontReplace";
    public const int MaxFontBytes = 16 * 1024 * 1024;

    private sealed record State(GMImage Image, (ushort Width, ushort Height) Size, float EmSize, uint Ascender, uint LineHeight, List<GlyphRect> Glyphs);

    private State? _before;
    private State? _after;

    private byte[] FontFile => fontFile;

    public override string Kind => Name;

    public override string Editor => "fonts";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["font"] = font, ["file"] = Convert.ToBase64String(fontFile) };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Fonts", font)];

    public static FontReplaceCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["font"], Convert.FromBase64String(a["file"]));

    /// <summary>True for the first bytes of a TrueType, OpenType or font collection file.</summary>
    public static bool LooksLikeFont(byte[] b) => b.Length > 12 && (b is [0x00, 0x01, 0x00, 0x00, ..] or [(byte)'O', (byte)'T', (byte)'T', (byte)'O', ..] or [(byte)'t', (byte)'r', (byte)'u', (byte)'e', ..] or [(byte)'t', (byte)'t', (byte)'c', (byte)'f', ..]);

    private GameMakerFont Find(GameMakerData data) => data.Fonts.FirstOrDefault(f => f.Name.Content == font) ?? throw new InvalidOperationException($"Font '{font}' does not exist.");

    private readonly record struct Drawing(MagickImage Atlas, List<GlyphRect> Rects, uint Ascender, uint LineHeight, GMImage? Page, GMImage? PageBefore);

    private Drawing? _prepared;

    /// <summary>
    /// Draws the glyphs and builds the new texture page ahead of <see cref="Apply"/>, which takes up to a second for a large font. Call with the model locked
    /// for reading, from any thread; the edit under the write lock then only swaps the page in. Without it, Apply does the work itself (the journal replay).
    /// </summary>
    public void Prepare(GameMakerData data)
    {
        _prepared?.Atlas.Dispose();
        _prepared = Draw(Find(data), true);
    }

    private Drawing Draw(GameMakerFont target, bool composePage)
    {
        if (!LooksLikeFont(fontFile) || fontFile.Length > MaxFontBytes) throw new InvalidDataException("The file is not a TrueType or OpenType font, or it is larger than 16 MiB.");
        var item = target.Texture ?? throw new InvalidOperationException("The font has no texture.");
        var atlas = new MagickImage(MagickColors.Transparent, item.SourceWidth, item.SourceHeight);
        try
        {
            var (rects, ascender, lineHeight) = Render(target, atlas);
            if (!composePage) return new Drawing(atlas, rects, ascender, lineHeight, null, null);
            // The same steps as the library's ReplaceTexture, up to the swap of the page picture, so they can run without the write lock.
            var data = item.TexturePage.TextureData;
            lock (data)
            {
                using var final = TextureWorker.ResizeImage(atlas, item.SourceWidth, item.SourceHeight);
                using var worker = new TextureWorker();
                var embedded = worker.GetEmbeddedTexture(item.TexturePage);
                embedded.Composite(final, item.SourceX, item.SourceY, CompositeOperator.Copy);
                return new Drawing(atlas, rects, ascender, lineHeight, GMImage.FromMagickImage(embedded).ConvertToFormat(data.Image.Format), data.Image);
            }
        }
        catch
        {
            atlas.Dispose();
            throw;
        }
    }

    public override void Revert(GameMakerData data) => Put(Find(data), _before ?? throw new InvalidOperationException("The font was never replaced."));
}
