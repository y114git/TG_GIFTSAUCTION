using System.Text.RegularExpressions;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>The frame size, the offset of the first frame and the gaps between frames, all in pixels.</summary>
public readonly record struct FrameGrid(int FrameWidth, int FrameHeight, int OffsetX, int OffsetY, int GapX, int GapY);

/// <summary>Cuts a sheet or strip picture into sprite frames: by frame size or by frame count, row by row from the top left. Also reads the frames of an animated GIF.</summary>
public static partial class SheetSplit
{
    private const int MaxFrames = 1024;
    private const uint MaxSide = 8192;

    /// <summary>The frame count a file name asks for with the _stripN convention ("hero_strip8.png" is 8 frames in one row), or null.</summary>
    public static int? StripCount(string path) =>
        StripName().Match(Path.GetFileNameWithoutExtension(path)) is { Success: true } m && int.TryParse(m.Groups[1].Value, out var n) && n > 0 ? n : null;

    [GeneratedRegex(@"_strip(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex StripName();

    /// <summary>The frame size that fits <paramref name="columns"/> by <paramref name="rows"/> frames into a sheet, after the offset and the gaps; at least 1 by 1.</summary>
    public static (int Width, int Height) SizeForCount(int sheetWidth, int sheetHeight, int columns, int rows, int offsetX, int offsetY, int gapX, int gapY) =>
        (Math.Max(1, (sheetWidth - offsetX - gapX * (Math.Max(1, columns) - 1)) / Math.Max(1, columns)), Math.Max(1, (sheetHeight - offsetY - gapY * (Math.Max(1, rows) - 1)) / Math.Max(1, rows)));

    /// <summary>How many whole frames fit across and down; a frame that would cross the sheet edge does not count, and a sheet smaller than one frame gives 0.</summary>
    public static (int Columns, int Rows) GridFor(int sheetWidth, int sheetHeight, int frameWidth, int frameHeight, int offsetX, int offsetY, int gapX, int gapY) =>
        (Math.Max(0, (sheetWidth - offsetX + gapX) / Math.Max(1, frameWidth + gapX)), Math.Max(0, (sheetHeight - offsetY + gapY) / Math.Max(1, frameHeight + gapY)));

    /// <summary>The frames of the grid, row by row, at most <paramref name="count"/> of them (0 for all). Cells that do not lie fully inside the sheet are left out.</summary>
    public static List<PixelBuffer> Cut(PixelBuffer sheet, FrameGrid grid, int count = 0)
    {
        var (frameWidth, frameHeight, offsetX, offsetY, gapX, gapY) = grid;
        if (frameWidth < 1 || frameHeight < 1) throw new ArgumentOutOfRangeException(nameof(grid), "A frame needs a size.");
        var (columns, rows) = GridFor(sheet.Width, sheet.Height, frameWidth, frameHeight, offsetX, offsetY, gapX, gapY);
        var limit = Math.Min(MaxFrames, count > 0 ? count : columns * rows);
        var frames = new List<PixelBuffer>();
        for (var i = 0; i < columns * rows && frames.Count < limit; i++)
            frames.Add(sheet.Cropped(offsetX + i % columns * (frameWidth + gapX), offsetY + i / columns * (frameHeight + gapY), frameWidth, frameHeight));
        return frames;
    }

    /// <summary>
    /// The frames of a picture file: every frame of an animated GIF (each one drawn over the previous like a viewer does), or the one picture.
    /// A single picture goes through <see cref="ReplaceFrameCommand.ReadPicture"/>, which checks the file signature, so a file that claims to be another type is refused.
    /// </summary>
    public static List<PixelBuffer> Read(byte[] bytes)
    {
        if (bytes is not [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..])
        {
            using var picture = ReplaceFrameCommand.ReadPicture(bytes);
            return [ToBuffer(picture)];
        }

        var settings = new MagickReadSettings { Format = MagickFormat.Gif };
        var info = new MagickImageInfo(bytes, settings);
        if (info.Width > MaxSide || info.Height > MaxSide) throw new InvalidDataException($"The picture is larger than {MaxSide} pixels on a side.");
        using var images = new MagickImageCollection(bytes, settings);
        if (images.Count > MaxFrames) throw new InvalidDataException($"The GIF has more than {MaxFrames} frames.");
        images.Coalesce();
        return [.. images.Select(ToBuffer)];
    }

    private static PixelBuffer ToBuffer(IMagickImage<byte> image)
    {
        using var copy = image.Clone();
        copy.Alpha(AlphaOption.Set);
        using var pixels = copy.GetPixels();
        return new PixelBuffer((int)copy.Width, (int)copy.Height, pixels.ToByteArray(PixelMapping.RGBA) ?? throw new InvalidDataException("The picture has no pixels."));
    }
}
