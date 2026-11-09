namespace G3MStudio.Core.Workspace;

/// <summary>Whole-picture transforms. Each returns a new picture; nearest neighbour only, so pixel art keeps its hard edges.</summary>
public sealed partial class PixelBuffer
{
    public PixelBuffer FlippedHorizontally()
    {
        var next = new PixelBuffer(Width, Height);
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++) Buffer.BlockCopy(Data, (y * Width + x) * 4, next.Data, (y * Width + Width - 1 - x) * 4, 4);
        return next;
    }

    public PixelBuffer FlippedVertically()
    {
        var next = new PixelBuffer(Width, Height);
        for (var y = 0; y < Height; y++) Buffer.BlockCopy(Data, y * Width * 4, next.Data, (Height - 1 - y) * Width * 4, Width * 4);
        return next;
    }

    public PixelBuffer Rotated90(bool clockwise)
    {
        var next = new PixelBuffer(Height, Width);
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var (nx, ny) = clockwise ? (Height - 1 - y, x) : (y, Width - 1 - x);
                Buffer.BlockCopy(Data, (y * Width + x) * 4, next.Data, (ny * Height + nx) * 4, 4);
            }

        return next;
    }

    /// <summary>Rotated by any angle (clockwise, degrees) into the smallest picture that holds the result.</summary>
    public PixelBuffer Rotated(double degrees)
    {
        double radians = degrees * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
        var width = (int)Math.Ceiling(Math.Abs(Width * cos) + Math.Abs(Height * sin) - 1e-9);
        var height = (int)Math.Ceiling(Math.Abs(Width * sin) + Math.Abs(Height * cos) - 1e-9);
        var next = new PixelBuffer(Math.Max(1, width), Math.Max(1, height));
        double cx = Width / 2.0, cy = Height / 2.0, nx = next.Width / 2.0, ny = next.Height / 2.0;
        for (var y = 0; y < next.Height; y++)
            for (var x = 0; x < next.Width; x++)
            {
                double dx = x + 0.5 - nx, dy = y + 0.5 - ny;
                var (sx, sy) = ((int)Math.Floor(cx + dx * cos + dy * sin), (int)Math.Floor(cy - dx * sin + dy * cos));
                if (Contains(sx, sy)) Buffer.BlockCopy(Data, (sy * Width + sx) * 4, next.Data, (y * next.Width + x) * 4, 4);
            }

        return next;
    }

    public PixelBuffer Scaled(int width, int height)
    {
        var next = new PixelBuffer(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) Buffer.BlockCopy(Data, ((y * Height / height) * Width + x * Width / width) * 4, next.Data, (y * width + x) * 4, 4);
        return next;
    }

    /// <summary>Shears the rows sideways: the middle row stays, a row <c>d</c> below it moves by <c>d * tan(degrees)</c>.</summary>
    public PixelBuffer SkewedHorizontally(double degrees)
    {
        var tan = Math.Tan(degrees * Math.PI / 180);
        int Shift(int y) => (int)Math.Round((y - (Height - 1) / 2.0) * tan);
        var (low, high) = (Enumerable.Range(0, Height).Min(Shift), Enumerable.Range(0, Height).Max(Shift));
        var next = new PixelBuffer(Width + high - low, Height);
        for (var y = 0; y < Height; y++) Buffer.BlockCopy(Data, y * Width * 4, next.Data, (y * next.Width + Shift(y) - low) * 4, Width * 4);
        return next;
    }

    /// <summary>A new canvas size; the picture sits at the anchor (0, 1 or 2 for left or top, middle, right or bottom) and is cropped or padded with clear pixels.</summary>
    public PixelBuffer Resized(int width, int height, int anchorX, int anchorY)
    {
        var next = new PixelBuffer(width, height);
        next.Place(this, (width - Width) * anchorX / 2, (height - Height) * anchorY / 2);
        return next;
    }

    public PixelBuffer Cropped(int x, int y, int width, int height)
    {
        var next = new PixelBuffer(width, height);
        next.Place(this, -x, -y, false);
        return next;
    }

    /// <summary>The smallest rectangle that holds every pixel with some alpha, or null for a picture that is fully clear.</summary>
    public Box? ContentBounds()
    {
        int x0 = Width, y0 = Height, x1 = -1, y1 = -1;
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (Data[(y * Width + x) * 4 + 3] == 0) continue;
                (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
            }

        return x1 < 0 ? null : new Box(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>The picture without its clear border (the same picture when it has no pixels).</summary>
    public PixelBuffer Trimmed() => ContentBounds() is var (x, y, w, h) ? Cropped(x, y, w, h) : this;

    /// <summary>Draws another picture with its top left at the point. Ignores <see cref="Clip"/>, since it moves whole pieces; with <paramref name="blend"/> false it copies the pixels.</summary>
    public void Place(PixelBuffer source, int x, int y, bool blend = true)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y), x1 = Math.Min(Width, x + source.Width), y1 = Math.Min(Height, y + source.Height);
        if (x1 <= x0 || y1 <= y0) return;
        var from = source.Words;
        var to = Words;
        for (var py = y0; py < y1; py++)
        {
            var row = from.Slice((py - y) * source.Width + x0 - x, x1 - x0);
            var target = to.Slice(py * Width + x0, x1 - x0);
            if (!blend) row.CopyTo(target);
            else BlendRow(row, target);
        }

        MarkDirty(x0, y0, x1 - 1, y1 - 1);
    }

    private static void BlendRow(Span<uint> row, Span<uint> target)
    {
        var i = 0;
        foreach (var word in row)
        {
            var alpha = word >> 24;
            if (alpha == 255 || (alpha != 0 && target[i] >> 24 == 0)) target[i] = word;
            else if (alpha != 0) target[i] = Pack(Unpack(word).Over(Unpack(target[i])));
            i++;
        }
    }

    private static Rgba Unpack(uint word) => new((byte)word, (byte)(word >> 8), (byte)(word >> 16), (byte)(word >> 24));

    private static uint Pack(Rgba c) => ((uint)c.A << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;

    /// <summary>A copy of the selected pixels in the selection's bounding box (everything else clear), with that box.</summary>
    public (PixelBuffer Pixels, Box Box)? Extract(PixelSelection? selection)
    {
        if ((selection is null ? new Box(0, 0, Width, Height) : selection.Bounds()) is not { } box) return null;
        var pixels = new PixelBuffer(box.Width, box.Height);
        var from = Words;
        var to = pixels.Words;
        for (var y = 0; y < box.Height; y++)
        {
            var row = from.Slice((box.Y + y) * Width + box.X, box.Width);
            var mask = selection is null ? [] : selection.Mask.AsSpan((box.Y + y) * Width + box.X, box.Width);
            if (selection is null || mask.IndexOf((byte)0) < 0) row.CopyTo(to.Slice(y * box.Width, box.Width));
            else
                for (var x = 0; x < box.Width; x++)
                    if (mask[x] != 0) to[y * box.Width + x] = row[x];
        }

        return (pixels, box);
    }

    /// <summary>Sets the selected pixels to clear (everything when the selection is null), ignoring <see cref="Clip"/>.</summary>
    public void ClearSelected(PixelSelection? selection)
    {
        if (selection is null) Array.Clear(Data);
        else
        {
            var words = Words;
            for (var y = 0; y < Height; y++)
            {
                var mask = selection.Mask.AsSpan(y * Width, Width);
                if (mask.IndexOf((byte)1) < 0) continue;
                var x = 0;
                while (x < Width)
                {
                    var start = mask[x..].IndexOf((byte)1);
                    if (start < 0) break;
                    x += start;
                    var length = mask[x..].IndexOf((byte)0);
                    var end = length < 0 ? Width : x + length;
                    words.Slice(y * Width + x, end - x).Clear();
                    x = end;
                }
            }
        }

        MarkDirty(0, 0, Width - 1, Height - 1);
    }

    /// <summary>A picture the size of the box that is opaque white where the selection holds a pixel (everywhere without a selection); it goes through the same transforms as the pixels do.</summary>
    internal static PixelBuffer OpaqueWhere(PixelSelection? selection, Box box)
    {
        var picture = new PixelBuffer(box.Width, box.Height);
        var words = picture.Words;
        for (var y = 0; y < box.Height; y++)
        {
            var mask = selection is null ? [] : selection.Mask.AsSpan((box.Y + y) * selection.Width + box.X, box.Width);
            if (selection is null || mask.IndexOf((byte)0) < 0) words.Slice(y * box.Width, box.Width).Fill(uint.MaxValue);
            else
                for (var x = 0; x < box.Width; x++)
                    if (mask[x] != 0) words[y * box.Width + x] = uint.MaxValue;
        }

        return picture;
    }

    /// <summary>The selection of the pixels that have some alpha, with the picture's top left at the point of a canvas of the given size.</summary>
    internal PixelSelection ToSelection(int canvasWidth, int canvasHeight, int ox, int oy)
    {
        var selection = new PixelSelection(canvasWidth, canvasHeight);
        var words = Words;
        for (var y = 0; y < Height; y++)
        {
            if ((uint)(oy + y) >= (uint)canvasHeight) continue;
            for (var x = 0; x < Width; x++)
                if (words[y * Width + x] >> 24 != 0 && (uint)(ox + x) < (uint)canvasWidth) selection.Mask[(oy + y) * canvasWidth + ox + x] = 1;
        }

        return selection;
    }
}
