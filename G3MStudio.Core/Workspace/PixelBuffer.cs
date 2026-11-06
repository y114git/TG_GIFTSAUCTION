namespace G3MStudio.Core.Workspace;

/// <summary>One pixel, straight (not premultiplied) alpha.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static readonly Rgba Clear = new(0, 0, 0, 0);

    /// <summary>Reads #RRGGBB or #RRGGBBAA; null for anything else.</summary>
    public static Rgba? Parse(string? text)
    {
        if (text is null || text.Length is not (7 or 9) || text[0] != '#' || !uint.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v)) return null;
        return text.Length == 7 ? new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255) : new Rgba((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public override string ToString() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    /// <summary>This colour drawn over another (straight alpha).</summary>
    public Rgba Over(Rgba under)
    {
        if (A == 255 || under.A == 0) return this;
        if (A == 0) return under;
        var outAlpha = A + under.A * (255 - A) / 255;
        return new Rgba(Mix(R, under.R, A, under.A, outAlpha), Mix(G, under.G, A, under.A, outAlpha), Mix(B, under.B, A, under.A, outAlpha), (byte)outAlpha);
    }

    private static byte Mix(byte top, byte bottom, int topAlpha, int bottomAlpha, int outAlpha) => (byte)((top * topAlpha * 255 + bottom * bottomAlpha * (255 - topAlpha)) / (outAlpha * 255));
}

public enum BrushShape { Square, Round }

/// <summary>An area of a picture: its top left pixel and its size.</summary>
public readonly record struct Box(int X, int Y, int Width, int Height);

/// <summary>
/// A picture as RGBA bytes with the drawing tools of the image editor (brush, line, shapes, fill, selection transforms, adjustments; see the partial files).
/// Pencil tools set pixels exactly (no blending), so an erase is a stamp of clear. Every write goes through <see cref="Clip"/> when a selection is set and
/// is tracked as a dirty rectangle, so the editor uploads and redraws only what changed. Pure data, so the tools are tested without a window.
/// </summary>
public sealed partial class PixelBuffer
{
    private int _minX = int.MaxValue, _minY = int.MaxValue, _maxX = -1, _maxY = -1;

    public PixelBuffer(int width, int height, byte[]? rgba = null)
    {
        if (width < 1 || height < 1 || (long)width * height * 4 > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(width), "The picture size is not usable.");
        (Width, Height) = (width, height);
        Data = rgba ?? new byte[width * height * 4];
        if (Data.Length != width * height * 4) throw new ArgumentException("The pixel data does not match the size.", nameof(rgba));
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Row by row, four bytes a pixel: red, green, blue, alpha.</summary>
    public byte[] Data { get; }

    /// <summary>When set, writes outside the selection are ignored.</summary>
    public PixelSelection? Clip { get; set; }

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Rgba Get(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return new Rgba(Data[i], Data[i + 1], Data[i + 2], Data[i + 3]);
    }

    public void Set(int x, int y, Rgba color)
    {
        if (!Contains(x, y) || (Clip is { } clip && !clip.Contains(x, y))) return;
        var i = (y * Width + x) * 4;
        (Data[i], Data[i + 1], Data[i + 2], Data[i + 3]) = (color.R, color.G, color.B, color.A);
        (_minX, _minY, _maxX, _maxY) = (Math.Min(_minX, x), Math.Min(_minY, y), Math.Max(_maxX, x), Math.Max(_maxY, y));
    }

    /// <summary>The rectangle changed since the last call (null when nothing changed); the call resets it.</summary>
    public (int X, int Y, int Width, int Height)? TakeDirty()
    {
        if (_maxX < 0) return null;
        var area = (_minX, _minY, _maxX - _minX + 1, _maxY - _minY + 1);
        (_minX, _minY, _maxX, _maxY) = (int.MaxValue, int.MaxValue, -1, -1);
        return area;
    }

    internal void MarkDirty(int x0, int y0, int x1, int y1)
    {
        (_minX, _minY, _maxX, _maxY) = (Math.Min(_minX, Math.Max(0, x0)), Math.Min(_minY, Math.Max(0, y0)), Math.Max(_maxX, Math.Min(Width - 1, x1)), Math.Max(_maxY, Math.Min(Height - 1, y1)));
    }

    public byte[] Snapshot() => (byte[])Data.Clone();

    public void Restore(byte[] snapshot)
    {
        Buffer.BlockCopy(snapshot, 0, Data, 0, Data.Length);
        MarkDirty(0, 0, Width - 1, Height - 1);
    }

    public PixelBuffer Copy() => new(Width, Height, Snapshot());
}
