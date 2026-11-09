namespace G3MStudio.Core.Workspace;

/// <summary>How the collision box of a sprite is found. The numbers are the values the data file stores.</summary>
public enum MaskMode
{
    Automatic = 0,
    Full = 1,
    Manual = 2,
}

/// <summary>The shape of a collision mask. Ellipse and Diamond are written as mask bits; the file stores only rectangle, rotated rectangle or precise.</summary>
public enum MaskShape
{
    Rectangle,
    Rotated,
    Ellipse,
    Diamond,
    Precise,
    PrecisePerFrame,
}

/// <summary>The part of a sprite that collides: pixel columns Left to Right and rows Top to Bottom, both ends included.</summary>
public readonly record struct MaskBox(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left + 1;

    public int Height => Bottom - Top + 1;
}

public enum BoxHandle
{
    Move,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Pure geometry for the sprite editor: collision boxes and masks, origin presets and nine-slice guides.</summary>
public static class SpriteMask
{
    public static MaskBox Full(int width, int height) => new(0, 0, Math.Max(0, width - 1), Math.Max(0, height - 1));

    /// <summary>Keeps the box inside a picture of this size with at least one pixel.</summary>
    public static MaskBox Clamp(MaskBox box, int width, int height)
    {
        var (maxX, maxY) = (Math.Max(0, width - 1), Math.Max(0, height - 1));
        var (left, right) = (Math.Clamp(Math.Min(box.Left, box.Right), 0, maxX), Math.Clamp(Math.Max(box.Left, box.Right), 0, maxX));
        var (top, bottom) = (Math.Clamp(Math.Min(box.Top, box.Bottom), 0, maxY), Math.Clamp(Math.Max(box.Top, box.Bottom), 0, maxY));
        return new MaskBox(left, top, right, bottom);
    }

    /// <summary>The smallest box that holds every solid pixel of every frame; the whole picture when no pixel is solid.</summary>
    public static MaskBox Auto(IEnumerable<Func<int, int, bool>> frames, int width, int height)
    {
        int left = width, top = height, right = -1, bottom = -1;
        foreach (var solid in frames)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!solid(x, y)) continue;
                    (left, right, top, bottom) = (Math.Min(left, x), Math.Max(right, x), Math.Min(top, y), Math.Max(bottom, y));
                }
            }
        }

        return right < 0 ? Full(width, height) : new MaskBox(left, top, right, bottom);
    }

    /// <summary>True when the pixel (x, y) lies inside the shape drawn in the box. Rectangles and precise shapes fill the box.</summary>
    public static bool Inside(MaskShape shape, MaskBox box, int x, int y)
    {
        if (x < box.Left || x > box.Right || y < box.Top || y > box.Bottom) return false;
        if (shape is not (MaskShape.Ellipse or MaskShape.Diamond)) return true;
        var (rx, ry) = (box.Width / 2.0, box.Height / 2.0);
        var (dx, dy) = (Math.Abs(x + 0.5 - (box.Left + rx)) / rx, Math.Abs(y + 0.5 - (box.Top + ry)) / ry);
        return shape == MaskShape.Ellipse ? (dx * dx) + (dy * dy) <= 1 : dx + dy <= 1;
    }

    /// <summary>
    /// The mask bits of <paramref name="area"/> (rows of (width + 7) / 8 bytes, leftmost pixel in the top bit) for a shape drawn in <paramref name="box"/>.
    /// A precise shape keeps the pixels <paramref name="solid"/> reports inside the box.
    /// </summary>
    public static byte[] Bits(MaskShape shape, Func<int, int, bool> solid, MaskBox box, MaskBox area)
    {
        var stride = (area.Width + 7) / 8;
        var bits = new byte[stride * area.Height];
        var precise = shape is MaskShape.Precise or MaskShape.PrecisePerFrame;
        for (var y = 0; y < area.Height; y++)
        {
            for (var x = 0; x < area.Width; x++)
            {
                var (px, py) = (area.Left + x, area.Top + y);
                if (Inside(shape, box, px, py) && (!precise || solid(px, py))) bits[(y * stride) + (x / 8)] |= (byte)(0x80 >> (x % 8));
            }
        }

        return bits;
    }

    /// <summary>The box after dragging <paramref name="handle"/> by (dx, dy) pixels, kept inside the picture and at least one pixel big.</summary>
    public static MaskBox Drag(MaskBox box, BoxHandle handle, int dx, int dy, int width, int height)
    {
        var (maxX, maxY) = (Math.Max(0, width - 1), Math.Max(0, height - 1));
        if (handle == BoxHandle.Move)
        {
            dx = Math.Clamp(dx, -box.Left, maxX - box.Right);
            dy = Math.Clamp(dy, -box.Top, maxY - box.Bottom);
            return new MaskBox(box.Left + dx, box.Top + dy, box.Right + dx, box.Bottom + dy);
        }

        var (left, top, right, bottom) = box;
        if (handle is BoxHandle.Left or BoxHandle.TopLeft or BoxHandle.BottomLeft) left = Math.Clamp(left + dx, 0, right);
        if (handle is BoxHandle.Right or BoxHandle.TopRight or BoxHandle.BottomRight) right = Math.Clamp(right + dx, left, maxX);
        if (handle is BoxHandle.Top or BoxHandle.TopLeft or BoxHandle.TopRight) top = Math.Clamp(top + dy, 0, bottom);
        if (handle is BoxHandle.Bottom or BoxHandle.BottomLeft or BoxHandle.BottomRight) bottom = Math.Clamp(bottom + dy, top, maxY);
        return new MaskBox(left, top, right, bottom);
    }

    /// <summary>The handle under a point within <paramref name="reach"/> pixels of the box edges, or null. Corners win over edges; the inside moves the box.</summary>
    public static BoxHandle? HandleAt(MaskBox box, double x, double y, double reach)
    {
        double l = box.Left, t = box.Top, r = box.Right + 1.0, b = box.Bottom + 1.0;
        if (x < l - reach || x > r + reach || y < t - reach || y > b + reach) return null;
        bool nearL = Math.Abs(x - l) <= reach, nearR = Math.Abs(x - r) <= reach, nearT = Math.Abs(y - t) <= reach, nearB = Math.Abs(y - b) <= reach;
        return (nearL, nearR, nearT, nearB) switch
        {
            (true, _, true, _) => BoxHandle.TopLeft,
            (_, true, true, _) => BoxHandle.TopRight,
            (true, _, _, true) => BoxHandle.BottomLeft,
            (_, true, _, true) => BoxHandle.BottomRight,
            (true, _, _, _) => BoxHandle.Left,
            (_, true, _, _) => BoxHandle.Right,
            (_, _, true, _) => BoxHandle.Top,
            (_, _, _, true) => BoxHandle.Bottom,
            _ => x >= l && x <= r && y >= t && y <= b ? BoxHandle.Move : null,
        };
    }

    /// <summary>The origin of one of the nine presets, numbered row by row from the top left (0) to the bottom right (8).</summary>
    public static (int X, int Y) OriginPreset(int preset, int width, int height) => (preset % 3 * width / 2, preset / 3 * height / 2);

    /// <summary>The nine-slice distances after moving one guide to a pixel position. Left and top count from the near edge, right and bottom from the far edge; opposite guides never cross.</summary>
    public static (int Left, int Top, int Right, int Bottom) MoveGuide((int Left, int Top, int Right, int Bottom) slice, string side, int position, int width, int height) => side switch
    {
        "left" => (Math.Clamp(position, 0, Math.Max(0, width - slice.Right)), slice.Top, slice.Right, slice.Bottom),
        "right" => (slice.Left, slice.Top, Math.Clamp(width - position, 0, Math.Max(0, width - slice.Left)), slice.Bottom),
        "top" => (slice.Left, Math.Clamp(position, 0, Math.Max(0, height - slice.Bottom)), slice.Right, slice.Bottom),
        "bottom" => (slice.Left, slice.Top, slice.Right, Math.Clamp(height - position, 0, Math.Max(0, height - slice.Top))),
        _ => slice,
    };
}
