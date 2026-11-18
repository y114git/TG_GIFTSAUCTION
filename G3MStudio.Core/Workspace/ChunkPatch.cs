
namespace G3MStudio.Core.Workspace;

/// <summary>
/// The raw bytes of one chunk of a data file on disk: copied out to a file, or replaced from a file. A replacement of another length moves every chunk after it,
/// and the offsets stored inside the file are not adjusted, so the game can fail to load it.
/// </summary>
public static class ChunkPatch
{
    /// <summary>Reads only the chunk headers of the data file at <paramref name="path"/>, so a large file is not loaded; the names come in file order.</summary>
    public static IReadOnlyList<string> Names(string path)
    {
        using var source = new HexSource(path);
        return [.. source.Chunks().Select(c => c.Name)];
    }

    /// <summary>Copies the data of the chunk, without its eight byte header, to the stream; a chunk over 2 GB streams like any other.</summary>
    public static void Export(string path, string name, Stream to)
    {
        long offset, length;
        using (var source = new HexSource(path)) (offset, length) = Find(source, name);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Copy(input, to, offset + 8, offset + 8 + length);
    }

    /// <summary>The chunk with a new length is written to a temporary file next to the original, which then replaces it; the length in the FORM header follows. The original is copied to <c>.bak</c> first, replacing an older backup.</summary>
    public static void Replace(string path, string name, byte[] data)
    {
        long offset, length, total;
        using (var source = new HexSource(path))
        {
            (offset, length) = Find(source, name);
            total = source.Length;
        }

        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            {
                Copy(input, output, 0, offset + 4);
                output.Write(BitConverter.GetBytes((uint)data.Length));
                output.Write(data);
                Copy(input, output, offset + 8 + length, total);
                output.Position = 4;
                output.Write(BitConverter.GetBytes((uint)(output.Length - 8)));
            }

            File.Copy(path, path + ".bak", true);
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static (long Offset, long Length) Find(HexSource source, string name)
    {
        foreach (var (chunk, offset) in source.Chunks())
        {
            if (chunk == name) return (offset, BitConverter.ToUInt32(source.Read(offset + 4, 4)));
        }

        throw new InvalidOperationException($"The file has no {name} chunk.");
    }

    private static void Copy(Stream from, Stream to, long start, long end)
    {
        var buffer = new byte[81920];
        from.Position = start;
        for (var left = end - start; left > 0;)
        {
            var read = from.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
            if (read == 0) throw new EndOfStreamException("The file is shorter than its chunk headers say.");
            to.Write(buffer, 0, read);
            left -= read;
        }
    }
}
