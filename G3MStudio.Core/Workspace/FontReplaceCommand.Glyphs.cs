using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using ImageMagick;
using ImageMagick.Drawing;

namespace G3MStudio.Core.Workspace;

#pragma warning disable CA1001, CA2000 // the images belong to the page, which the model disposes; a prepared drawing is disposed when it is applied or replaced
public sealed partial class FontReplaceCommand
{
    private static State Capture(GameMakerFont f)
    {
        var item = f.Texture ?? throw new InvalidOperationException("The font has no texture.");
        return new State(item.TexturePage.TextureData.Image, (item.TargetWidth, item.TargetHeight), f.EmSize, f.Ascender, f.LineHeight, [.. f.Glyphs.Select(g => new GlyphRect(g.SourceX, g.SourceY, g.SourceWidth, g.SourceHeight, g.Shift, g.Offset))]);
    }

    private static void Put(GameMakerFont f, State state)
    {
        f.Texture.TexturePage.TextureData.Image = state.Image;
        (f.Texture.TargetWidth, f.Texture.TargetHeight) = state.Size;
        (f.EmSize, f.Ascender, f.LineHeight) = (state.EmSize, state.Ascender, state.LineHeight);
        foreach (var (glyph, r) in f.Glyphs.Zip(state.Glyphs)) (glyph.SourceX, glyph.SourceY, glyph.SourceWidth, glyph.SourceHeight, glyph.Shift, glyph.Offset) = (r.X, r.Y, r.Width, r.Height, r.Shift, r.Offset);
    }
    public override void Apply(GameMakerData data)
    {
        var target = Find(data);
        _before ??= Capture(target);
        if (_after is not null)
        {
            Put(target, _after);
            return;
        }

        var item = target.Texture;
        // A glyph added or removed, or another edit of the same texture page, since Prepare makes the drawing stale: it is made again.
        Drawing ready;
        if (_prepared is { } prepared && prepared.Rects.Count == target.Glyphs.Count && ReferenceEquals(prepared.PageBefore, item.TexturePage.TextureData.Image))
        {
            ready = prepared;
        }
        else
        {
            _prepared?.Atlas.Dispose();
            ready = Draw(target, false);
        }

        _prepared = null;
        var (atlas, rects, ascender, lineHeight, page, _) = ready;
        using (atlas)
        {
            if (page is null)
            {
                item.ReplaceTexture(atlas);
            }
            else
            {
                item.TexturePage.TextureData.Image = page;
                (item.TargetWidth, item.TargetHeight) = ((ushort)atlas.Width, (ushort)atlas.Height);
            }
        }

        foreach (var (glyph, r) in target.Glyphs.Zip(rects)) (glyph.SourceX, glyph.SourceY, glyph.SourceWidth, glyph.SourceHeight, glyph.Shift, glyph.Offset) = (r.X, r.Y, r.Width, r.Height, r.Shift, r.Offset);
        (target.Ascender, target.LineHeight) = (ascender, target.LineHeight == 0 ? 0 : lineHeight);
        _after = Capture(target);
    }
    private static void Place(MagickImage atlas, string fontPath, float size, string text, (int Width, int Height) cellSize, (int X, int Baseline) pen, int spread, (int X, int Y) at)
    {
        using var cell = new MagickImage(MagickColors.Transparent, (uint)cellSize.Width, (uint)cellSize.Height);
        new Drawables().Font(fontPath).FontPointSize(size).FillColor(MagickColors.White).Text(pen.X, pen.Baseline, text).Draw(cell); // no gravity: the y of Text is the baseline
        if (spread > 0)
        {
            using var field = ToDistanceField(cell, spread);
            atlas.Composite(field, at.X, at.Y, CompositeOperator.Copy);
        }
        else
        {
            atlas.Composite(cell, at.X, at.Y, CompositeOperator.Over);
        }
    }

    /// <summary>The cell as GameMaker's SDF sheets hold it: white, with the signed distance in the alpha (see <see cref="SdfEncoder"/>).</summary>
    private static MagickImage ToDistanceField(MagickImage cell, int spread)
    {
        int width = (int)cell.Width, height = (int)cell.Height;
        using var pixels = cell.GetPixels();
        var rgba = pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidOperationException("The glyph could not be read.");
        var coverage = new byte[width * height];
        for (var i = 0; i < coverage.Length; i++) coverage[i] = rgba[i * 4 + 3];
        var distance = SdfEncoder.Encode(coverage, width, height, spread);
        var white = new byte[rgba.Length];
        for (var i = 0; i < distance.Length; i++) (white[i * 4], white[i * 4 + 1], white[i * 4 + 2], white[i * 4 + 3]) = (255, 255, 255, distance[i]);
        var image = new MagickImage();
        image.ReadPixels(white, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA));
        return image;
    }

    private (List<GlyphRect> Rects, uint Ascender, uint LineHeight) Render(GameMakerFont target, MagickImage atlas)
    {
        int width = (int)atlas.Width, height = (int)atlas.Height;
        var path = Path.Combine(Path.GetTempPath(), "g3mstudio-font-" + Guid.NewGuid().ToString("N") + ".ttf");
        File.WriteAllBytes(path, FontFile);
        try
        {
            var size = Math.Max(1, target.EmSize);
            var spread = (int)Math.Min(target.SDFSpread, 64);
            var pad = spread; // an SDF glyph keeps the spread around it, as the cells of a sheet made by the IDE do
            using var probe = new MagickImage(MagickColors.Transparent, 8, 8);
            probe.Settings.Font = path;
            probe.Settings.FontPointsize = size;
            var rects = new List<GlyphRect>();
            int x = 1, y = 1, rowHeight = 0;
            // Every glyph of a GameMaker font has the same cell height and the same baseline in it; the runtime draws a cell from the top of the line.
            var line = probe.FontTypeMetrics("Hg") ?? throw new InvalidDataException("The font could not be read.");
            var ascent = Math.Ceiling(line.Ascent);
            var lineHeight = Math.Ceiling(Math.Max(line.TextHeight, line.Ascent - line.Descent));
            var h = (int)lineHeight + 2 * pad;
            foreach (var glyph in target.Glyphs)
            {
                var text = char.ConvertFromUtf32(glyph.Character == 0 ? ' ' : glyph.Character);
                var metrics = probe.FontTypeMetrics(text) ?? throw new InvalidDataException("The font could not be read.");
                var w = Math.Max(1, (int)Math.Ceiling(metrics.TextWidth)) + 2 * pad;
                if (x + w + 1 > width) (x, y, rowHeight) = (1, y + rowHeight + 1, 0);
                if (y + h + 1 > height) throw new InvalidOperationException("The glyphs do not fit the font's texture at this size. Make the size smaller in the property list.");

                Place(atlas, path, size, text, (w, h), (pad, pad + (int)ascent), spread, (x, y));

                rects.Add(new GlyphRect((ushort)x, (ushort)y, (ushort)w, (ushort)h, (short)Math.Round(metrics.TextWidth, MidpointRounding.AwayFromZero), (short)-pad));
                (x, rowHeight) = (x + w + 1, Math.Max(rowHeight, h));
            }

            return (rects, (uint)ascent, (uint)lineHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
