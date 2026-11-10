using System.Globalization;
using System.Text;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The bytes of a file as rows of a hex view: offset, 16 bytes in groups of four, and the ASCII text. Reads on demand from the file (one block is kept), so a
/// file of hundreds of megabytes costs no memory. The file itself is never written until <see cref="Commit"/>: byte patches are kept apart, shown in the rows
/// and can be undone and redone until then.
/// </summary>
public sealed class HexSource : IDisposable
{
    public const int RowBytes = 16;
    private const int BlockSize = 64 * 1024;

    private FileStream _stream;
    private DateTime _writtenAt;
    private byte[] _block = [];
    private long _blockStart = -1;
    private readonly Dictionary<long, byte> _patched = [];
    private readonly List<(long Offset, byte[] Old, byte[] New)> _undo = [];
    private readonly List<(long Offset, byte[] Old, byte[] New)> _redo = [];

    public HexSource(string path)
    {
        _stream = Open(path);
        Length = _stream.Length;
        _writtenAt = File.GetLastWriteTimeUtc(path);
    }

    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);

    public long Length { get; }

    /// <summary>How many bytes differ from the file until the patches are committed.</summary>
    public int PatchedCount => _patched.Count;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool IsPatched(long offset) => _patched.ContainsKey(offset);

    /// <summary>Overwrites bytes at the offset (the file does not grow); false when they would pass the end. One call is one undo step.</summary>
    public bool Patch(long offset, byte[] bytes)
    {
        if (bytes.Length == 0 || offset < 0 || offset + bytes.Length > Length) return false;
        var old = Read(offset, bytes.Length);
        if (old.Length != bytes.Length) return false; // the file was shortened under the view
        _undo.Add((offset, old, bytes));
        _redo.Clear();
        Apply(offset, bytes);
        return true;
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(step);
        Apply(step.Offset, step.Old);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(step);
        Apply(step.Offset, step.New);
    }

    // A byte put back to what the file holds is no patch any more.
    private void Apply(long offset, byte[] bytes)
    {
        var onDisk = ReadDisk(offset, bytes.Length);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i < onDisk.Length && onDisk[i] == bytes[i]) _patched.Remove(offset + i);
            else _patched[offset + i] = bytes[i];
        }
    }

    /// <summary>
    /// Writes the patched bytes into a copy of the file, which then replaces it (the first commit keeps the original as <c>.bak</c>), and forgets the undo steps (the file is the new baseline). Refuses with an
    /// <see cref="IOException"/> when the file has another length or write time than when it was opened: the patches belong to bytes that are gone.
    /// </summary>
    public void Commit(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != Length || info.LastWriteTimeUtc != _writtenAt) throw new IOException("The file changed after the hex view opened it. Close the view and open it again.");
        var temp = path + ".hex.tmp";
        try
        {
            File.Copy(path, temp, true);
            using (var file = new FileStream(temp, FileMode.Open, FileAccess.Write))
            {
                foreach (var (offset, value) in _patched.OrderBy(p => p.Key))
                {
                    file.Position = offset;
                    file.WriteByte(value);
                }
            }

            if (!File.Exists(path + ".bak")) File.Copy(path, path + ".bak");
            // Windows will not replace a file this view still has open; the view reads the new bytes through a new handle.
            _stream.Dispose();
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            _stream.Dispose(); // still open when the copy or the write failed above
            _stream = Open(path);
        }

        _writtenAt = File.GetLastWriteTimeUtc(path);
        _patched.Clear();
        _undo.Clear();
        _redo.Clear();
        _blockStart = -1;
    }

    public int RowCount => (int)Math.Min(int.MaxValue, (Length + RowBytes - 1) / RowBytes);

    /// <summary>The bytes at the offset (fewer at the end of the file), with the patches in place.</summary>
    public byte[] Read(long offset, int count)
    {
        var bytes = ReadDisk(offset, count);
        if (_patched.Count > 0)
        {
            for (var i = 0; i < bytes.Length; i++)
            {
                if (_patched.TryGetValue(offset + i, out var value)) bytes[i] = value;
            }
        }

        return bytes;
    }

    private byte[] ReadDisk(long offset, int count)
    {
        if (offset < 0 || offset >= Length) return [];
        var wanted = (int)Math.Min(count, Length - offset);
        if (_blockStart < 0 || offset < _blockStart || offset + wanted > _blockStart + _block.Length)
        {
            try
            {
                var start = offset / BlockSize * BlockSize;
                var block = new byte[(int)Math.Min(BlockSize * 2L, Length - start)];
                _stream.Position = start;
                _stream.ReadExactly(block);
                (_blockStart, _block) = (start, block);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // The file was saved over, shortened or locked since it was opened: the row shows no bytes instead of taking the window down.
                _blockStart = -1;
                return [];
            }
        }

        return _block.AsSpan((int)(offset - _blockStart), wanted).ToArray();
    }

    /// <summary>"00A41C20  58 59 5A 57  00 00 00 00  ...  XYZW....", the width of the offset following the size of the file.</summary>
    public string Row(int index)
    {
        var offset = (long)index * RowBytes;
        var bytes = Read(offset, RowBytes);
        var text = new StringBuilder();
        text.Append(offset.ToString(Length > 0xFFFFFFFF ? "X12" : "X8", CultureInfo.InvariantCulture)).Append("  ");
        for (var i = 0; i < RowBytes; i++)
        {
            text.Append(i < bytes.Length ? bytes[i].ToString("X2", CultureInfo.InvariantCulture) : "  ").Append(i % 4 == 3 ? "  " : " ");
        }

        foreach (var b in bytes) text.Append(b is >= 32 and < 127 ? (char)b : '.');
        return text.ToString();
    }

    /// <summary>The offset of the first match at or after <paramref name="from"/>, or -1.</summary>
    public long Find(byte[] pattern, long from)
    {
        if (pattern.Length == 0 || from >= Length) return -1;
        var buffer = new byte[1024 * 1024 + pattern.Length];
        for (var at = Math.Max(0, from); at < Length; at += 1024 * 1024)
        {
            int read;
            try
            {
                _stream.Position = at;
                read = _stream.Read(buffer, 0, (int)Math.Min(buffer.Length, Length - at));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return -1;
            }
            var found = buffer.AsSpan(0, read).IndexOf(pattern);
            if (found >= 0) return at + found;
        }

        return -1;
    }

    public static bool TryParseOffset(string? text, out long offset)
    {
        offset = 0;
        text = text?.Trim().Replace("_", "", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(text)) return false;
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset) && offset >= 0
            : long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out offset);
    }

    /// <summary>Hex bytes ("DE AD BE EF", two or more pairs) or else the text as UTF-8; null for an empty box.</summary>
    public static byte[]? ParsePattern(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 2 && tokens.All(t => t.Length == 2 && byte.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)))
            return [.. tokens.Select(t => byte.Parse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture))];
        return Encoding.UTF8.GetBytes(text);
    }

    /// <summary>Hex byte pairs ("DE AD", "DEAD" or "0xDE 0xAD") as bytes; null when empty or not hex.</summary>
    public static byte[]? ParseHexBytes(string? text)
    {
        var digits = (text ?? "").Replace("0x", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "", StringComparison.Ordinal);
        if (digits.Length == 0 || digits.Length % 2 != 0 || !digits.All(char.IsAsciiHexDigit)) return null;
        return Convert.FromHexString(digits);
    }

    /// <summary>The chunks of a data file in file order (name and offset of the chunk header), found by walking the headers only; empty when the file is not a FORM file or a header is cut off.</summary>
    public IReadOnlyList<(string Name, long Offset)> Chunks()
    {
        var found = new List<(string, long)>();
        if (Length < 8 || System.Text.Encoding.ASCII.GetString(Read(0, 4)) != "FORM") return found;
        var end = Math.Min(Length, 8L + BitConverter.ToUInt32(Read(4, 4)));
        for (var at = 8L; at + 8 <= end;)
        {
            var header = Read(at, 8);
            found.Add((System.Text.Encoding.ASCII.GetString(header, 0, 4), at));
            at += 8L + BitConverter.ToUInt32(header, 4);
        }

        return found;
    }

    /// <summary>One line per chunk: the offset of its header in hex, its name and the length of its data, in file order (UTMT-FILE-10; the chunk level, items inside a chunk are not listed).</summary>
    public string OffsetMap()
    {
        var chunks = Chunks();
        var text = new System.Text.StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            var next = i + 1 < chunks.Count ? chunks[i + 1].Offset : Math.Min(Length, 8L + BitConverter.ToUInt32(Read(4, 4)));
            text.Append("0x").Append(chunks[i].Offset.ToString("X8", CultureInfo.InvariantCulture)).Append(' ').Append(chunks[i].Name).Append(' ').Append(next - chunks[i].Offset - 8).Append('\n');
        }

        return text.ToString();
    }

    public void Dispose() => _stream.Dispose();
}
