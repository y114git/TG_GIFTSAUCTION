using System.Runtime.CompilerServices;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Reads and writes rectangles of texture pages as RGBA pixels. A page an edit writes to is kept as raw BGRA in memory, so the next edit of it costs a copy of the page
/// instead of a decode and an encode of the whole page; <see cref="EncodeAll"/> puts every such page back in its format before the file is written.
/// A raw page costs four bytes a pixel, so <see cref="Idle"/> and <see cref="PutBack"/> let the app encode pages nobody wrote to for a while, or the oldest ones
/// once all raw pages together pass a cap, during a long session.
/// The page image is never changed in place: every write makes a new image, so an undo that keeps the old one still has it.
/// </summary>
public static class PagePixels
{
    private static readonly ConditionalWeakTable<GameMakerEmbeddedTexture, StrongBox<GMImage.ImageFormat>> Formats = [];

    // When each page was last written as raw pixels (Environment.TickCount64).
    private static readonly ConditionalWeakTable<GameMakerEmbeddedTexture, StrongBox<long>> Written = [];

    /// <summary>A raw page nobody wrote to for this long is encoded back by <see cref="Idle"/>.</summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(2);

    /// <summary>The raw pages together may take this many bytes; past it the oldest are encoded back even when they are not idle yet.</summary>
    public const long RawCap = 256L << 20;

    /// <summary>True when the rectangle lies on the page.</summary>
    public static bool Holds(GameMakerEmbeddedTexture page, int x, int y, int width, int height) =>
        page.TextureData?.Image is { } image && x >= 0 && y >= 0 && width > 0 && height > 0 && x + width <= image.Width && y + height <= image.Height;

    /// <summary>The page as raw BGRA; the stored format is remembered for <see cref="EncodeAll"/>.</summary>
    private static GMImage Raw(GameMakerEmbeddedTexture page)
    {
        var image = page.TextureData?.Image ?? throw new InvalidOperationException("The texture page has no picture.");
        if (image.Format == GMImage.ImageFormat.RawBgra) return image;
        Formats.AddOrUpdate(page, new StrongBox<GMImage.ImageFormat>(image.Format));
        return Decode(image);
    }

    // The last pages decoded for reading, keyed by the stored image; a write makes a new image, so an entry is never stale. They are not counted in RawCap,
    // so ForgetDecoded drops them on the idle tick and when a file closes.
    private static readonly List<(GMImage Stored, GMImage Raw)> Decoded = [];
    private const int DecodedLimit = 3;

    /// <summary>Drops the pages decoded for reading; the next read decodes again.</summary>
    public static void ForgetDecoded()
    {
        lock (Decoded) Decoded.Clear();
    }

    internal static bool IsDecoded(GMImage stored)
    {
        lock (Decoded) return Decoded.Exists(d => ReferenceEquals(d.Stored, stored));
    }

    /// <summary>A new page (kept as raw pixels) is stored in the format of <paramref name="like"/>.</summary>
    public static void Remember(GameMakerEmbeddedTexture page, GameMakerEmbeddedTexture like)
    {
        var format = Formats.TryGetValue(like, out var known) ? known.Value : like.TextureData?.Image?.Format ?? GMImage.ImageFormat.Png;
        if (format != GMImage.ImageFormat.RawBgra) Formats.AddOrUpdate(page, new StrongBox<GMImage.ImageFormat>(format));
    }

    /// <summary>A rectangle of the page as RGBA pixels. The page itself is not changed.</summary>
    public static PixelBuffer Read(GameMakerEmbeddedTexture page, int x, int y, int width, int height)
    {
        var stored = page.TextureData?.Image ?? throw new InvalidOperationException("The texture page has no picture.");
        return Cut(Decode(stored), x, y, width, height);
    }

    // The page as raw pixels for reading, through the cache of the last decoded pages.
    internal static GMImage Decode(GMImage stored)
    {
        if (stored.Format == GMImage.ImageFormat.RawBgra) return stored;
        GMImage? raw;
        lock (Decoded) raw = Decoded.FirstOrDefault(d => ReferenceEquals(d.Stored, stored)).Raw;
        if (raw is not null) return raw;
        raw = stored.ConvertToRawBgra();
        lock (Decoded)
        {
            Decoded.Insert(0, (stored, raw));
            if (Decoded.Count > DecodedLimit) Decoded.RemoveAt(Decoded.Count - 1);
        }

        return raw;
    }

    private static PixelBuffer Cut(GMImage raw, int x, int y, int width, int height)
    {
        var picture = new PixelBuffer(width, height);
        var bgra = raw.GetRawImageData();
        for (var row = 0; row < height; row++) Swap(bgra.Slice(((y + row) * raw.Width + x) * 4, width * 4), picture.Data.AsSpan(row * width * 4, width * 4));
        return picture;
    }

    /// <summary>Writes pictures at their places on the page as one new page image, and clears the given rectangles first (a frame that moved away leaves no pixels behind).</summary>
    public static void Write(GameMakerEmbeddedTexture page, IEnumerable<(int X, int Y, PixelBuffer Picture)> pictures, IEnumerable<Box>? clear = null)
    {
        page.TextureData.Image = Draw(Raw(page), pictures, clear);
        Touch(page);
    }

    /// <summary>Marks the page as just written (an undo that puts a raw image back counts too).</summary>
    internal static void Touch(GameMakerEmbeddedTexture page) => Written.AddOrUpdate(page, new StrongBox<long>(Environment.TickCount64));

    /// <summary>The page grown or cut to a new size (top left stays), as raw BGRA, with the pictures written on it.</summary>
    public static void Resize(GameMakerEmbeddedTexture page, int width, int height, IEnumerable<(int X, int Y, PixelBuffer Picture)>? pictures = null, IEnumerable<Box>? clear = null)
    {
        var raw = Raw(page);
        var sized = new GMImage(width, height);
        var from = raw.GetRawImageData();
        var to = sized.GetRawImageData();
        var w = Math.Min(width, raw.Width);
        for (var row = 0; row < Math.Min(height, raw.Height); row++) from.Slice(row * raw.Width * 4, w * 4).CopyTo(to.Slice(row * width * 4, w * 4));
        page.TextureData.Image = Draw(sized, pictures ?? [], clear, false);
        page.TextureWidth = width;
        page.TextureHeight = height;
        Touch(page);
    }

    private static GMImage Draw(GMImage raw, IEnumerable<(int X, int Y, PixelBuffer Picture)> pictures, IEnumerable<Box>? clear, bool copy = true)
    {
        var next = raw;
        if (copy)
        {
            next = new GMImage(raw.Width, raw.Height);
            raw.GetRawImageData().CopyTo(next.GetRawImageData());
        }

        var bgra = next.GetRawImageData();
        foreach (var box in clear ?? [])
        {
            for (var row = Math.Max(0, box.Y); row < Math.Min(next.Height, box.Y + box.Height); row++)
            {
                var x0 = Math.Max(0, box.X);
                var x1 = Math.Min(next.Width, box.X + box.Width);
                if (x1 > x0) bgra.Slice((row * next.Width + x0) * 4, (x1 - x0) * 4).Clear();
            }
        }

        foreach (var (x, y, picture) in pictures)
        {
            if (x < 0 || y < 0 || x + picture.Width > next.Width || y + picture.Height > next.Height) throw new InvalidOperationException("The picture does not fit on the texture page.");
            for (var row = 0; row < picture.Height; row++) Swap(picture.Data.AsSpan(row * picture.Width * 4, picture.Width * 4), bgra.Slice(((y + row) * next.Width + x) * 4, picture.Width * 4));
        }

        return next;
    }

    // BGRA and RGBA differ in the order of red and blue only, so one swap works both ways.
    private static void Swap(ReadOnlySpan<byte> from, Span<byte> to)
    {
        from.CopyTo(to);
        foreach (ref var pixel in System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(to)) pixel = (pixel & 0xFF00FF00u) | ((pixel >> 16) & 0xFFu) | ((pixel & 0xFFu) << 16);
    }

    /// <summary>Encodes every page kept as raw pixels back in its format (the format it was read in, else the format of the other pages, else PNG). Call before writing the file.</summary>
    public static void EncodeAll(GameMakerData data)
    {
        var fallback = Fallback(data);
        foreach (var page in data.EmbeddedTextures)
        {
            if (page.TextureData?.Image is not { Format: GMImage.ImageFormat.RawBgra } raw) continue;
            page.TextureData.Image = raw.ConvertToFormat(FormatOf(page, fallback));
        }
    }

    private static GMImage.ImageFormat Fallback(GameMakerData data) =>
        data.EmbeddedTextures.Select(t => t.TextureData?.Image?.Format).FirstOrDefault(f => f is not null and not GMImage.ImageFormat.RawBgra and not GMImage.ImageFormat.Unknown) ?? GMImage.ImageFormat.Png;

    private static GMImage.ImageFormat FormatOf(GameMakerEmbeddedTexture page, GMImage.ImageFormat fallback) => Formats.TryGetValue(page, out var format) ? format.Value : fallback;

    /// <summary>
    /// The raw pages to encode back (read under the data file's read lock): every one not written for <paramref name="idle"/>, then the least recently written
    /// until the rest fit in <paramref name="cap"/> bytes. Only pages an edit wrote count.
    /// </summary>
    public static IReadOnlyList<RawPage> Idle(GameMakerData data, TimeSpan idle, long cap)
    {
        var now = Environment.TickCount64;
        var fallback = Fallback(data);
        var raw = new List<(GameMakerEmbeddedTexture Page, GMImage Image, long At)>();
        foreach (var page in data.EmbeddedTextures)
            if (page.TextureData?.Image is { Format: GMImage.ImageFormat.RawBgra } image && Written.TryGetValue(page, out var at)) raw.Add((page, image, at.Value));
        var total = raw.Sum(p => (long)p.Image.Width * p.Image.Height * 4);
        var picked = new List<RawPage>();
        foreach (var (page, image, at) in raw.OrderBy(p => p.At))
        {
            if (now - at < idle.TotalMilliseconds && total <= cap) continue;
            picked.Add(new RawPage(page, image, FormatOf(page, fallback)));
            total -= (long)image.Width * image.Height * 4;
        }

        return picked;
    }

    /// <summary>The page encoded in its format; reads only the raw image, which no write changes in place, so it can run off the lock.</summary>
    public static GMImage Encode(RawPage page) => page.Raw.ConvertToFormat(page.Format);

    /// <summary>Puts the encoded image in place (under the write lock) when the page still holds the raw image it came from; false when an edit wrote it since.</summary>
    public static bool PutBack(RawPage page, GMImage encoded)
    {
        if (!ReferenceEquals(page.Page.TextureData?.Image, page.Raw)) return false;
        page.Page.TextureData.Image = encoded;
        Written.Remove(page.Page);
        return true;
    }
}

/// <summary>One raw page to encode back: the raw image it holds now and the format it goes back to (<see cref="PagePixels.Idle"/>).</summary>
public sealed record RawPage(GameMakerEmbeddedTexture Page, GMImage Raw, GMImage.ImageFormat Format);
