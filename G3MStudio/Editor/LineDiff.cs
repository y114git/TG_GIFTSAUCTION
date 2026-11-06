namespace G3MStudio.Editor;

public enum DiffKind
{
    Same,
    Added,
    Removed,
}

/// <param name="Kind">Whether the line is in both texts, only in the new one or only in the old one.</param>
/// <param name="OldLine">One based line number in the old text, 0 for an added line.</param>
/// <param name="NewLine">One based line number in the new text, 0 for a removed line.</param>
/// <param name="Text">The line without its line break.</param>
public sealed record DiffLine(DiffKind Kind, int OldLine, int NewLine, string Text);

/// <summary>A line diff between two texts (the code in the editor against the code in the data file). Equal lines at both ends are cut off first; the middle uses the longest common subsequence, and a middle too large for that is shown as one removed and one added block.</summary>
public static class LineDiff
{
    private const long MostCells = 4_000_000;

    public static IReadOnlyList<DiffLine> Compute(string oldText, string newText)
    {
        var (a, b) = (oldText.Split('\n'), newText.Split('\n'));
        int head = 0, tail = 0;
        while (head < a.Length && head < b.Length && a[head] == b[head]) head++;
        while (tail < a.Length - head && tail < b.Length - head && a[^(tail + 1)] == b[^(tail + 1)]) tail++;
        var result = new List<DiffLine>();
        for (var i = 0; i < head; i++) result.Add(new DiffLine(DiffKind.Same, i + 1, i + 1, a[i]));
        var (am, bm) = (a[head..(a.Length - tail)], b[head..(b.Length - tail)]);
        Middle(result, am, bm, head);
        for (var i = tail; i > 0; i--) result.Add(new DiffLine(DiffKind.Same, a.Length - i + 1, b.Length - i + 1, a[^i]));
        return result;
    }

    private static void Middle(List<DiffLine> result, string[] a, string[] b, int offset)
    {
        if ((long)(a.Length + 1) * (b.Length + 1) > MostCells)
        {
            result.AddRange(a.Select((t, i) => new DiffLine(DiffKind.Removed, offset + i + 1, 0, t)));
            result.AddRange(b.Select((t, i) => new DiffLine(DiffKind.Added, 0, offset + i + 1, t)));
            return;
        }

        var table = new int[a.Length + 1][];
        for (var i = 0; i <= a.Length; i++) table[i] = new int[b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                table[i][j] = a[i] == b[j] ? table[i + 1][j + 1] + 1 : Math.Max(table[i + 1][j], table[i][j + 1]);
        int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y])
            {
                result.Add(new DiffLine(DiffKind.Same, offset + x + 1, offset + y + 1, a[x]));
                x++;
                y++;
            }
            else if (y >= b.Length || (x < a.Length && table[x + 1][y] >= table[x][y + 1]))
            {
                result.Add(new DiffLine(DiffKind.Removed, offset + x + 1, 0, a[x]));
                x++;
            }
            else
            {
                result.Add(new DiffLine(DiffKind.Added, 0, offset + y + 1, b[y]));
                y++;
            }
        }
    }
}
