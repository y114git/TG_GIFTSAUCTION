using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The picture of a font is a sheet its glyph rectangles point into, measured from the top left of the font's texture page item. So the sheet may move on the
/// pages and grow or shrink like any other picture, as long as the glyph rectangles follow a canvas change and still lie on the sheet. A scale, rotation or
/// skew of the whole sheet would leave them pointing at the wrong pixels, so it is refused.
/// </summary>
public static class FontSheet
{
    /// <summary>Where the top left of the sheet lies after the operations, given where it lay before (null once the sheet was scaled, rotated or skewed).</summary>
    public static (int X, int Y)? Follow((int X, int Y)? shift, PixelBuffer before, PixelSelection? selection, IEnumerable<ImageOp> ops)
    {
        var (width, height) = (before.Width, before.Height);
        foreach (var (index, op) in ops.Index())
        {
            if (shift is not { } s) return null;
            // as in ImageOps.WholeOrSelected: resize and trim always change the whole canvas, and a canvas change drops the selection
            switch (op.Kind)
            {
                case ImageOps.Resize:
                    var (w, h) = (op.Int(0), op.Int(1));
                    shift = (s.X + (w - width) * op.Int(2) / 2, s.Y + (h - height) * op.Int(3) / 2);
                    (width, height) = (w, h);
                    selection = null;
                    break;
                case ImageOps.Crop when selection?.Bounds() is { } box:
                    shift = (s.X - box.X, s.Y - box.Y);
                    (width, height) = (box.Width, box.Height);
                    selection = null;
                    break;
                // a trim is measured on the picture before the edit, so a trim after other ops is refused
                case ImageOps.Trim when index > 0:
                    return null;
                case ImageOps.Trim:
                    if (before.ContentBounds() is { } content)
                    {
                        shift = (s.X - content.X, s.Y - content.Y);
                        (width, height) = (content.Width, content.Height);
                    }

                    selection = null;
                    break;
                case ImageOps.Rotate90 or ImageOps.Rotate or ImageOps.Scale or ImageOps.Skew when selection is null:
                    return null;
            }
        }

        return shift;
    }

    /// <summary>Throws when a glyph moved by the shift would not lie on a sheet of the new size.</summary>
    public static void Check(GameMakerFont font, int width, int height, (int X, int Y)? shift)
    {
        if (shift is not var (dx, dy))
            throw new InvalidOperationException($"The sheet of font '{font.Name?.Content}' was scaled, rotated or skewed, so its glyph rectangles would point at the wrong pixels. Change the canvas size instead.");
        for (var i = 0; i < font.Glyphs.Count; i++)
        {
            var g = font.Glyphs[i];
            if (g.SourceWidth == 0 || g.SourceHeight == 0) continue; // an empty glyph (a space) has no pixels to lose
            if (g.SourceX + dx < 0 || g.SourceY + dy < 0 || g.SourceX + dx + g.SourceWidth > width || g.SourceY + dy + g.SourceHeight > height)
                throw new InvalidOperationException($"Glyph {i} of font '{font.Name?.Content}' would lie outside a sheet of {width}x{height}. Keep the canvas large enough for every glyph.");
        }
    }
}

/// <summary>Moves every glyph rectangle of a font by the same distance, after its sheet's canvas grew or shrank on the top or left side. Empty glyphs stay at 0.</summary>
public sealed class FontSheetShiftCommand(string font, int dx, int dy) : EditCommand
{
    public const string Name = "fontSheetShift";

    public override string Kind => Name;

    public override string Editor => "fonts";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["font"] = font,
        ["dx"] = dx.ToString(CultureInfo.InvariantCulture),
        ["dy"] = dy.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => [new("Fonts", font)];

    public static FontSheetShiftCommand FromArgs(IReadOnlyDictionary<string, string> a) =>
        new(a["font"], int.Parse(a["dx"], CultureInfo.InvariantCulture), int.Parse(a["dy"], CultureInfo.InvariantCulture));

    private GameMakerFont Find(GameMakerData data) => data.Fonts.FirstOrDefault(f => f.Name.Content == font) ?? throw new InvalidOperationException($"Font '{font}' does not exist.");

    public override void Apply(GameMakerData data) => Move(Find(data), dx, dy);

    public override void Revert(GameMakerData data) => Move(Find(data), -dx, -dy);

    private static void Move(GameMakerFont font, int dx, int dy)
    {
        // every glyph is checked before any moves, so a refused shift changes nothing
        if (font.Glyphs.Any(g => g.SourceWidth > 0 && g.SourceHeight > 0 && (g.SourceX + dx is < 0 or > ushort.MaxValue || g.SourceY + dy is < 0 or > ushort.MaxValue)))
            throw new InvalidOperationException($"A glyph of font '{font.Name?.Content}' would leave its sheet.");
        foreach (var g in font.Glyphs)
            if (g.SourceWidth > 0 && g.SourceHeight > 0) (g.SourceX, g.SourceY) = ((ushort)(g.SourceX + dx), (ushort)(g.SourceY + dy));
    }
}
