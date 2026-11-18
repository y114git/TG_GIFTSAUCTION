namespace G3MStudio.Editor;

/// <summary>Grows a selection one syntax step at a time: the word, the inside of the brackets around it, the brackets with their content, the line, the whole text.</summary>
public static class GmlSelection
{
    /// <summary>The next larger selection that contains the given one, or null when the whole text is selected already.</summary>
    public static (int Start, int End)? Expand(GmlDoc doc, int start, int end)
    {
        var text = doc.Text;
        var candidates = new List<(int Start, int End)>();
        var (a, b) = (start, end);
        while (a > 0 && IsWord(text[a - 1])) a--;
        while (b < text.Length && IsWord(text[b])) b++;
        if (b > a && text[start..end].All(IsWord)) candidates.Add((a, b));
        for (var i = 0; i < doc.Tokens.Count; i++)
        {
            if (doc.Tokens[i].Kind != G3MLib.Analyzer.Compiler.GmlTokenKind.Separator || doc.End(i) - doc.Start(i) != 1 || text[doc.Start(i)] is not ('(' or '[' or '{')) continue;
            var close = doc.PairOf(i);
            if (close <= i) continue;
            candidates.Add((doc.End(i), doc.Start(close)));
            candidates.Add((doc.Start(i), doc.End(close)));
        }

        var lineStart = start <= 0 ? 0 : text.LastIndexOf('\n', Math.Min(start, text.Length) - 1) + 1;
        var lineEnd = text.IndexOf('\n', end);
        candidates.Add((lineStart, lineEnd < 0 ? text.Length : lineEnd > 0 && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd));
        candidates.Add((0, text.Length));
        var larger = candidates.Where(c => c.Start <= start && c.End >= end && (c.Start < start || c.End > end)).ToList();
        return larger.Count == 0 ? null : larger.MinBy(c => c.End - c.Start);
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
}
