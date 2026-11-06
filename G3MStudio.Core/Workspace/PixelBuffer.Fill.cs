using System.Runtime.InteropServices;

namespace G3MStudio.Core.Workspace;

public sealed partial class PixelBuffer
{
    private Span<uint> Words => MemoryMarshal.Cast<byte, uint>(Data.AsSpan());

    // Colours are near when no channel differs by more than the tolerance; two fully transparent pixels are always the same colour.
    private static bool Near(uint a, uint b, int tolerance, bool colourOnly)
    {
        if (a == b) return true;
        var (aa, ba) = ((byte)(a >> 24), (byte)(b >> 24));
        if (aa == 0 && ba == 0 && !colourOnly) return true;
        var alpha = colourOnly ? 0 : Math.Abs(aa - ba);
        return Math.Max(alpha, Math.Max(Math.Abs((int)(a & 255) - (int)(b & 255)), Math.Max(Math.Abs((int)((a >> 8) & 255) - (int)((b >> 8) & 255)), Math.Abs((int)((a >> 16) & 255) - (int)((b >> 16) & 255))))) <= tolerance;
    }

    /// <summary>
    /// Marks (1) the pixels near the colour at the point: the area connected to it in four directions, or every pixel of the picture. The nearness is
    /// computed once in a tight loop, then the area is found by whole runs with vectorised searches, so a 2048 x 2048 picture of one colour takes a few milliseconds.
    /// </summary>
    internal byte[] FloodMask(int x, int y, int tolerance, bool contiguous, bool colourOnly = false)
    {
        var words = Words;
        var target = words[y * Width + x];
        var match = new byte[Width * Height];
        if (tolerance == 0 && !colourOnly)
        {
            var at = 0;
            foreach (var word in words) match[at++] = word == target || ((word | target) >> 24 == 0) ? (byte)1 : (byte)0;
        }
        else
        {
            var at = 0;
            foreach (var word in words) match[at++] = Near(word, target, tolerance, colourOnly) ? (byte)1 : (byte)0;
        }

        if (!contiguous) return match;
        var pending = new Stack<(int X, int Y)>();
        pending.Push((x, y));
        while (pending.Count > 0)
        {
            var (px, py) = pending.Pop();
            var row = match.AsSpan(py * Width, Width);
            if (row[px] != 1) continue;
            var before = row[..px].LastIndexOfAnyExcept((byte)1);
            var after = row[px..].IndexOfAnyExcept((byte)1);
            int left = before + 1, right = after < 0 ? Width - 1 : px + after - 1;
            row[left..(right + 1)].Fill(2);
            if (py > 0) QueueRuns(match.AsSpan((py - 1) * Width, Width), py - 1, left, right, pending);
            if (py < Height - 1) QueueRuns(match.AsSpan((py + 1) * Width, Width), py + 1, left, right, pending);
        }

        match.AsSpan().Replace((byte)1, (byte)0);
        match.AsSpan().Replace((byte)2, (byte)1);
        return match;
    }

    // Pushes one seed for each run of matching pixels (1) in a neighbouring row, between the two columns.
    private static void QueueRuns(Span<byte> row, int y, int left, int right, Stack<(int, int)> pending)
    {
        var x = left;
        while (x <= right)
        {
            var start = row[x..(right + 1)].IndexOf((byte)1);
            if (start < 0) return;
            x += start;
            pending.Push((x, y));
            var end = row[x..(right + 1)].IndexOfAnyExcept((byte)1);
            if (end < 0) return;
            x += end;
        }
    }

    /// <summary>Paints the area near the colour of the clicked pixel (connected, or everywhere when not contiguous).</summary>
    public void Fill(int x, int y, Rgba color, int tolerance = 0, bool contiguous = true)
    {
        if (!Contains(x, y)) return;
        var mask = FloodMask(x, y, tolerance, contiguous);
        FillMask(mask, color);
    }

    /// <summary>Paints every pixel the mask marks.</summary>
    internal void FillMask(byte[] mask, Rgba color)
    {
        for (var y = 0; y < Height; y++)
        {
            var row = mask.AsSpan(y * Width, Width);
            var x = 0;
            while (x < Width)
            {
                var start = row[x..].IndexOfAnyExcept((byte)0);
                if (start < 0) break;
                x += start;
                var length = row[x..].IndexOf((byte)0);
                var end = length < 0 ? Width : x + length;
                Span(y, x, end - 1, color);
                x = end;
            }
        }
    }

    /// <summary>Paints the selected pixels.</summary>
    public void FillSelection(PixelSelection selection, Rgba color) => FillMask(selection.Mask, color);

    /// <summary>Changes every pixel near <paramref name="from"/> to <paramref name="to"/>; with <see cref="Rgba.Clear"/> it removes the colour.</summary>
    public void Replace(Rgba from, Rgba to, int tolerance)
    {
        var words = Words;
        var target = ((uint)from.A << 24) | ((uint)from.B << 16) | ((uint)from.G << 8) | from.R;
        var mask = new byte[Width * Height];
        var at = 0;
        foreach (var word in words) mask[at++] = Near(word, target, tolerance, false) ? (byte)1 : (byte)0;
        FillMask(mask, to);
    }
}
