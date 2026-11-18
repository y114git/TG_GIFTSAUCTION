using System.Text;
using System.Text.RegularExpressions;

namespace G3MStudio.Core.ModPack;

/// <summary>One row of the side-by-side code view: the old line, the new line, or both; a null side is an empty cell.</summary>
public sealed record SideRow(DiffLine? Left, DiffLine? Right)
{
    public bool Differs => Left?.Kind != DiffKind.Same;
}

/// <summary>The two layouts of a code difference: side by side and unified.</summary>
public static partial class CodeView
{
    /// <summary>The line diff of two texts; with <paramref name="ignoreWhitespace"/> lines that differ only in white space are equal (the text shown is the new one).</summary>
    public static IReadOnlyList<DiffLine> Lines(string oldText, string newText, bool ignoreWhitespace)
    {
        if (!ignoreWhitespace) return LineDiff.Compute(oldText, newText);
        var (a, b) = (oldText.Split('\n'), newText.Split('\n'));
        var lines = LineDiff.Compute(Squeeze(oldText), Squeeze(newText));
        return [.. lines.Select(l => l with { Text = l.Kind == DiffKind.Removed ? a[l.OldLine - 1] : b[l.NewLine - 1] })];
    }

    private static string Squeeze(string text) => string.Join('\n', text.Split('\n').Select(l => Blanks().Replace(l.Trim(), " ")));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Blanks();

    /// <summary>A removed run and the added run after it share rows, so a changed line sits opposite its old version.</summary>
    public static IReadOnlyList<SideRow> SideBySide(IReadOnlyList<DiffLine> lines)
    {
        var rows = new List<SideRow>();
        List<DiffLine> gone = [], came = [];
        void Flush()
        {
            rows.AddRange(Enumerable.Range(0, Math.Max(gone.Count, came.Count)).Select(n => new SideRow(n < gone.Count ? gone[n] : null, n < came.Count ? came[n] : null)));
            gone = [];
            came = [];
        }

        foreach (var line in lines)
        {
            if (line.Kind == DiffKind.Same)
            {
                Flush();
                rows.Add(new SideRow(line, line));
            }
            else (line.Kind == DiffKind.Removed ? gone : came).Add(line);
        }

        Flush();
        return rows;
    }

    /// <summary>Unified text: removed lines with "-", added with "+", and <paramref name="context"/> unchanged lines around each change, a "..." line for what is left out.</summary>
    public static string Unified(IReadOnlyList<DiffLine> lines, int context)
    {
        var near = new bool[lines.Count];
        for (var c = 0; c < lines.Count; c++)
            if (lines[c].Kind != DiffKind.Same)
                for (var k = Math.Max(0, c - context); k <= Math.Min(lines.Count - 1, c + context); k++) near[k] = true;
        var text = new StringBuilder();
        var skipped = false;
        foreach (var (line, i) in lines.Select((l, i) => (l, i)))
        {
            if (line.Kind == DiffKind.Same && !near[i])
            {
                skipped = true;
                continue;
            }

            if (skipped) text.Append("...\n");
            skipped = false;
            text.Append(line.Kind == DiffKind.Added ? "+ " : line.Kind == DiffKind.Removed ? "- " : "  ").Append(line.Text).Append('\n');
        }

        if (skipped) text.Append("...\n");
        return text.ToString();
    }
}
