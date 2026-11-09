namespace G3MStudio.Editor;

/// <summary>One replacement of a text and the selection to put back afterwards.</summary>
public sealed record LineEdit(int Offset, int Length, string Text, int SelectionStart, int SelectionEnd);

/// <summary>Line operations of the code editor as pure text functions (comment, duplicate, move, auto close). The editor applies the result as one document change, so one undo step reverts it.</summary>
public static class GmlLineEdits
{
    private static string NewLine(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static int LineStart(string text, int offset) => offset <= 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;

    private static int LineEnd(string text, int offset)
    {
        var end = text.IndexOf('\n', offset);
        if (end < 0) return text.Length;
        return end > 0 && text[end - 1] == '\r' ? end - 1 : end;
    }

    // Length of the line break that starts at offset (0 at the end of the text).
    private static int BreakAt(string text, int offset) => offset >= text.Length ? 0 : text[offset] == '\r' && offset + 1 < text.Length && text[offset + 1] == '\n' ? 2 : 1;

    // Length of the line break that ends right before offset.
    private static int BreakBefore(string text, int offset) => offset <= 0 ? 0 : offset >= 2 && text[offset - 2] == '\r' && text[offset - 1] == '\n' ? 2 : 1;

    // The whole lines a selection touches: a selection that ends at the very start of a line does not include that line.
    private static (int Start, int End) Block(string text, int selStart, int selEnd)
    {
        var last = selEnd > selStart && selEnd > 0 && text[selEnd - 1] == '\n' ? selEnd - 1 : selEnd;
        return (LineStart(text, selStart), LineEnd(text, Math.Max(last, LineStart(text, selStart))));
    }

    /// <summary>Comments every line of the selection with //, or removes the comment marks when every non-empty line has one.</summary>
    public static LineEdit ToggleLineComment(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var lines = text[start..end].Split('\n');
        var content = lines.Select(l => l.TrimEnd('\r')).ToArray();
        var filled = content.Where(l => l.Trim().Length > 0).ToArray();
        var remove = filled.Length > 0 && filled.All(l => l.TrimStart().StartsWith("//", StringComparison.Ordinal));
        var indent = filled.Length == 0 ? 0 : filled.Min(l => l.Length - l.TrimStart().Length);
        lines = [.. lines.Select((line, i) => content[i].Trim().Length == 0 ? line : (remove ? Uncomment(content[i]) : content[i].Insert(indent, "// ")) + (line.EndsWith('\r') ? "\r" : ""))];

        var replaced = string.Join('\n', lines);
        return new LineEdit(start, end - start, replaced, selStart == selEnd ? Math.Clamp(selStart + replaced.Length - (end - start), start, start + replaced.Length) : start, start + replaced.Length);
    }

    /// <summary>Puts a tab in front of every non-empty line the selection touches.</summary>
    public static LineEdit Indent(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var replaced = string.Join('\n', text[start..end].Split('\n').Select(l => l.TrimEnd('\r').Length == 0 ? l : "\t" + l));
        return new LineEdit(start, end - start, replaced, start, start + replaced.Length);
    }

    /// <summary>Takes one tab or up to four spaces off the front of every line the selection touches.</summary>
    public static LineEdit Outdent(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var replaced = string.Join('\n', text[start..end].Split('\n').Select(l => l.StartsWith('\t') ? l[1..] : l[Math.Min(l.TakeWhile(c => c == ' ').Take(4).Count(), l.Length)..]));
        return new LineEdit(start, end - start, replaced, start, start + replaced.Length);
    }

    private static string Uncomment(string line)
    {
        var at = line.IndexOf("//", StringComparison.Ordinal);
        var cut = at + 2 < line.Length && line[at + 2] == ' ' ? 3 : 2;
        return line.Remove(at, cut);
    }

    /// <summary>Wraps the selection (or the caret line) in /* */, or takes the marks off a selection that already has them.</summary>
    public static LineEdit ToggleBlockComment(string text, int selStart, int selEnd)
    {
        if (selStart == selEnd) (selStart, selEnd) = (LineStart(text, selStart), LineEnd(text, selStart));
        var selected = text[selStart..selEnd];
        if (selected.Length >= 4 && selected.StartsWith("/*", StringComparison.Ordinal) && selected.EndsWith("*/", StringComparison.Ordinal))
        {
            var inner = selected[2..^2];
            return new LineEdit(selStart, selEnd - selStart, inner, selStart, selStart + inner.Length);
        }

        var wrapped = "/*" + selected + "*/";
        return new LineEdit(selStart, selEnd - selStart, wrapped, selStart, selStart + wrapped.Length);
    }

    /// <summary>Copies the lines of the selection below themselves; the selection moves to the copy.</summary>
    public static LineEdit DuplicateLines(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var nl = NewLine(text);
        var copy = nl + text[start..end];
        return new LineEdit(end, 0, copy, selStart + copy.Length, selEnd + copy.Length);
    }

    /// <summary>Swaps the lines of the selection with the line above (or below). Null at the first (or last) line.</summary>
    public static LineEdit? MoveLines(string text, int selStart, int selEnd, bool up)
    {
        var (start, end) = Block(text, selStart, selEnd);
        if (up)
        {
            if (start == 0) return null;
            var previous = LineStart(text, start - 1);
            var aboveEnd = LineEnd(text, previous);
            var (above, brk, block) = (text[previous..aboveEnd], text[aboveEnd..start], text[start..end]);
            var shift = above.Length + brk.Length;
            return new LineEdit(previous, end - previous, block + brk + above, selStart - shift, selEnd - shift);
        }

        if (end >= text.Length) return null;
        var nextStart = end + BreakAt(text, end);
        var nextEnd = LineEnd(text, nextStart);
        var (below, brkDown, moved) = (text[nextStart..nextEnd], text[end..nextStart], text[start..end]);
        var down = below.Length + brkDown.Length;
        return new LineEdit(start, nextEnd - start, below + brkDown + moved, selStart + down, selEnd + down);
    }

    /// <summary>Removes the lines of the selection with their line breaks; the caret goes to the start of the line that takes their place.</summary>
    public static LineEdit DeleteLines(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        // The last line has no break after it: take the one before it instead.
        var (from, to) = end < text.Length ? (start, end + BreakAt(text, end)) : (start - BreakBefore(text, start), end);
        return new LineEdit(from, to - from, "", from, from);
    }

    /// <summary>Joins the selected lines into one, with one space where each break and the indentation after it were. A caret alone joins its line with the next. Null when there is nothing to join.</summary>
    public static LineEdit? JoinLines(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        if (selStart == selEnd) end = end < text.Length ? LineEnd(text, end + BreakAt(text, end)) : end;
        var lines = text[start..end].Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        if (lines.Length < 2) return null;
        var first = lines[0].TrimEnd();
        var joined = first + string.Concat(lines.Skip(1).Select(l => l.Trim().Length == 0 ? "" : " " + l.Trim()));
        return new LineEdit(start, end - start, joined, start + first.Length, start + first.Length);
    }

    /// <summary>Sorts the selected lines (case insensitive); lines that are already in that order are put in the reverse order instead. Null for a single line.</summary>
    public static LineEdit? SortLines(string text, int selStart, int selEnd)
    {
        var (start, end) = Block(text, selStart, selEnd);
        var lines = text[start..end].Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        if (lines.Length < 2) return null;
        var ascending = lines.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var replaced = string.Join(NewLine(text), lines.SequenceEqual(ascending) ? ascending.Reverse() : ascending);
        return new LineEdit(start, end - start, replaced, start, start + replaced.Length);
    }

    /// <summary>The edits that remove the spaces and tabs at the end of every line of the selection, or of the whole text when nothing is selected. One edit per line, so the caret and anchors stay. Empty when nothing changes.</summary>
    public static IReadOnlyList<TextEdit> TrimTrailingWhitespace(string text, int selStart, int selEnd)
    {
        var (start, end) = selStart == selEnd ? (0, text.Length) : Block(text, selStart, selEnd);
        var edits = new List<TextEdit>();
        for (var at = start; at <= end;)
        {
            var lineEnd = Math.Min(LineEnd(text, at), end);
            var keep = lineEnd;
            while (keep > at && text[keep - 1] is ' ' or '\t') keep--;
            if (keep < lineEnd) edits.Add(new TextEdit(keep, lineEnd - keep, ""));
            if (lineEnd >= end) break;
            at = lineEnd + BreakAt(text, lineEnd);
        }

        return edits;
    }

    public enum CaseMode { Upper, Lower, Title }

    /// <summary>Changes the case of the selection, or of the word at the caret when nothing is selected. Null when nothing would change.</summary>
    public static LineEdit? ConvertCase(string text, int selStart, int selEnd, CaseMode mode)
    {
        if (selStart == selEnd)
        {
            while (selStart > 0 && IsWord(text[selStart - 1])) selStart--;
            while (selEnd < text.Length && IsWord(text[selEnd])) selEnd++;
        }

        var selected = text[selStart..selEnd];
        var changed = mode switch
        {
            CaseMode.Upper => selected.ToUpperInvariant(),
            CaseMode.Lower => selected.ToLowerInvariant(),
            _ => string.Concat(selected.Select((c, i) => i == 0 || !IsWord(selected[i - 1]) ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c))),
        };
        return changed == selected ? null : new LineEdit(selStart, selEnd - selStart, changed, selStart, selEnd);
    }

    private static bool IsCloser(char c) => c is ')' or ']' or '}' or ';' or ',' || char.IsWhiteSpace(c);

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>The text to put after a typed opening bracket or quote, or null to type it alone. The text is the state before the character is inserted at <paramref name="offset"/>.</summary>
    public static string? ClosingFor(string text, int offset, char typed)
    {
        var next = offset < text.Length ? text[offset] : '\n';
        var previous = offset > 0 ? text[offset - 1] : '\n';
        return typed switch
        {
            '(' or '[' or '{' => IsCloser(next) ? GmlDoc.Closer(typed).ToString() : null,
            '"' or '\'' => IsCloser(next) && !IsWord(previous) && previous != typed ? typed.ToString() : null,
            _ => null,
        };
    }

    /// <summary>True when the typed closing bracket or quote should only step over the identical character that follows the caret.</summary>
    public static bool StepsOver(string text, int offset, char typed) => typed is ')' or ']' or '}' or '"' or '\'' && offset < text.Length && text[offset] == typed;
}
