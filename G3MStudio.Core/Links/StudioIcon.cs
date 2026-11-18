using System.Buffers.Binary;

namespace G3MStudio.Core.Links;

/// <summary>
/// The icon of G3MStudio and of its data files. The artwork is the ICO file in the app's Assets folder, embedded here:
/// <see cref="Ico"/> is for Windows, <see cref="Png"/> (the 256 px picture inside that ICO) is for the Linux icon theme.
/// </summary>
public static class StudioIcon
{
    public const string Name = "application-x-gamemaker-data";

    public static byte[] Ico()
    {
        using var stream = typeof(StudioIcon).Assembly.GetManifestResourceStream("G3MStudio.ico") ?? throw new InvalidOperationException("The icon is not embedded.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>The largest picture of the ICO; the artwork stores it as a PNG, which is what an icon theme takes.</summary>
    public static byte[] Png()
    {
        var ico = Ico();
        var count = BinaryPrimitives.ReadInt16LittleEndian(ico.AsSpan(4));
        var best = (Size: 0, Length: 0, Offset: 0);
        for (var i = 0; i < count; i++)
        {
            var entry = ico.AsSpan(6 + i * 16, 16);
            var size = entry[0] == 0 ? 256 : entry[0];
            if (size > best.Size) best = (size, BinaryPrimitives.ReadInt32LittleEndian(entry[8..]), BinaryPrimitives.ReadInt32LittleEndian(entry[12..]));
        }

        return ico.AsSpan(best.Offset, best.Length).ToArray();
    }
}
