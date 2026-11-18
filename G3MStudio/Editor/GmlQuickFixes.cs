using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>One offered fix: a language key with its arguments for the menu text, and the edits to run.</summary>
public sealed record QuickFix(string Key, string Argument, IReadOnlyList<TextEdit> Edits);

/// <summary>Simple fixes for the name at the caret, from tokens and the list of known names: spelling, a missing declaration, commenting out a line.</summary>
public static class GmlQuickFixes
{
    private const int MostDistance = 2;

    public static IReadOnlyList<QuickFix> For(GmlDoc doc, int offset, Func<string, bool> isKnown, IEnumerable<string> knownNames, bool lineHasProblem)
    {
        var fixes = new List<QuickFix>();
        var token = doc.TokenAt(offset);
        if (token >= 0 && doc.Tokens[token].Kind == GmlTokenKind.Identifier && !(token > 0 && doc.IsPunct(token - 1, '.')))
        {
            var name = doc.Slice(token);
            var declared = GmlSymbols.DeclarationOf(doc, name) >= 0;
            if (!declared && !isKnown(name) && !IsCall(doc, token))
            {
                foreach (var near in Nearest(name, knownNames).Take(3)) fixes.Add(new QuickFix("code.fix.rename", near, [new TextEdit(doc.Start(token), name.Length, near)]));
                if (DeclareLocal(doc, name, token) is { } declare) fixes.Add(declare);
                fixes.Add(new QuickFix("code.fix.declareGlobal", name, [new TextEdit(doc.Start(token), 0, "global.")]));
            }
            else if (!declared && !isKnown(name))
            {
                foreach (var near in Nearest(name, knownNames).Take(3)) fixes.Add(new QuickFix("code.fix.rename", near, [new TextEdit(doc.Start(token), name.Length, near)]));
            }
        }

        if (lineHasProblem && CommentLine(doc, offset) is { } comment) fixes.Add(comment);
        return fixes;
    }

    private static bool IsCall(GmlDoc doc, int token) => token + 1 < doc.Tokens.Count && doc.IsPunct(token + 1, '(');

    private static string LineBreak(GmlDoc doc) => doc.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>The line ending of the line that holds the offset, so the new line ends like its neighbours; the ending most used in the text for a last line without one.</summary>
    private static string EndingOfLineAt(GmlDoc doc, int offset)
    {
        var end = doc.Text.IndexOf('\n', offset);
        return end < 0 ? LineBreak(doc) : end > 0 && doc.Text[end - 1] == '\r' ? "\r\n" : "\n";
    }

    /// <summary>
    /// "var name = 0;" on its own line above the statement that holds the first use, with the indentation of that statement (tabs and spaces copied as they are). When the statement
    /// does not start its line, the declaration goes in front of it on the same line. No fix when there is no safe place: inside a struct or a parameter list, or after else or case.
    /// </summary>
    internal static QuickFix? DeclareLocal(GmlDoc doc, string name, int token)
    {
        if (StatementHead(doc, token) is not (>= 0 and var head)) return null;
        var text = doc.Text;
        var at = doc.Start(head);
        var lineStart = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
        var indent = text[lineStart..at];
        var declaration = "var " + name + " = 0;";
        var edit = indent.All(c => c is ' ' or '\t')
            ? new TextEdit(lineStart, 0, indent + declaration + EndingOfLineAt(doc, at))
            : new TextEdit(at, 0, declaration + " ");
        return new QuickFix("code.fix.declareLocal", name, [edit]);
    }

    private static int Previous(GmlDoc doc, int token)
    {
        var i = token - 1;
        while (i >= 0 && doc.Tokens[i].Kind == GmlTokenKind.Comment) i--;
        return i;
    }

    private static bool Is(GmlDoc doc, int token, char c) => token >= 0 && doc.IsPunct(token, c);

    private static bool IsWord(GmlDoc doc, int token, params string[] words) => token >= 0 && doc.Tokens[token].Kind == GmlTokenKind.Keyword && words.Contains(doc.Slice(token));

    private static bool IsCloser(GmlDoc doc, int token) => doc.Tokens[token].Kind == GmlTokenKind.Separator && doc.Text[doc.Start(token)] is ')' or ']' or '}';

    /// <summary>The innermost bracket that is open at the token (its opener), or -1 at the top level.</summary>
    private static int EnclosingOpener(GmlDoc doc, int token)
    {
        var open = new Stack<int>();
        for (var i = 0; i < token; i++)
        {
            if (doc.IsOpener(i)) open.Push(i);
            else if (IsCloser(doc, i) && open.Count > 0) open.Pop();
        }

        return open.Count > 0 ? open.Peek() : -1;
    }

    // A { that opens a block (after ), else, do, try, finally, constructor or another statement) and not a struct literal (after =, (, comma, : or return).
    private static bool OpensBlock(GmlDoc doc, int brace)
    {
        var before = Previous(doc, brace);
        return before < 0 || Is(doc, before, ')') || Is(doc, before, ';') || Is(doc, before, '{') || Is(doc, before, '}') || IsWord(doc, before, "else", "do", "try", "finally", "constructor");
    }

    private static bool InParameterList(GmlDoc doc, int paren)
    {
        var before = Previous(doc, paren);
        return IsWord(doc, before, "function") || (before >= 0 && doc.Tokens[before].Kind == GmlTokenKind.Identifier && IsWord(doc, Previous(doc, before), "function"));
    }

    /// <summary>The first token of the statement that holds the token, outside every bracket the token is in; -1 when a declaration cannot go above it without risk.</summary>
    private static int StatementHead(GmlDoc doc, int token)
    {
        var at = token;
        for (var climb = 0; climb < 32; climb++)
        {
            var open = EnclosingOpener(doc, at);
            if (open >= 0 && !doc.IsPunct(open, '{'))
            {
                if (doc.Text[doc.Start(open)] == '(' && InParameterList(doc, open)) return -1;
                at = open; // inside ( or [: the statement that owns them (a for, an if, a call) is the one to put the declaration above
                continue;
            }

            if (open >= 0 && !OpensBlock(doc, open)) return -1;
            var head = BlockHead(doc, at, open + 1);
            return head >= 0 && StartsStatement(doc, head) ? head : -1;
        }

        return -1;
    }

    // Walks back from the token to the end of the statement before it (a ;, or a } that is followed by a new statement) or to the start of the block.
    private static int BlockHead(GmlDoc doc, int token, int floor)
    {
        var head = token;
        for (var i = Previous(doc, token); i >= floor; i = Previous(doc, i))
        {
            if (Is(doc, i, ';')) break;
            if (Is(doc, i, ')') || Is(doc, i, ']') || Is(doc, i, '}'))
            {
                // A } followed by a word ends a block statement. Followed by anything else it closes a struct or function inside the statement.
                if (Is(doc, i, '}') && doc.Tokens[head].Kind is GmlTokenKind.Identifier or GmlTokenKind.Keyword && !IsWord(doc, head, "instanceof")) break;
                var pair = doc.PairOf(i);
                if (pair < 0) return -1;
                head = i = pair;
                continue;
            }

            head = i;
        }

        return head;
    }

    private static bool StartsStatement(GmlDoc doc, int head)
    {
        var kind = doc.Tokens[head].Kind;
        if (kind == GmlTokenKind.Identifier) return true;
        if (kind == GmlTokenKind.Keyword) return !IsWord(doc, head, "else", "until", "catch", "finally", "case", "default");
        return kind == GmlTokenKind.Operator && doc.Slice(head) is "++" or "--";
    }

    // "// " in front of the code of the line, after its indentation. Nothing for an empty line, a line that is a comment already, or a line inside a block comment.
    private static QuickFix? CommentLine(GmlDoc doc, int offset)
    {
        if (LineBounds(doc.Text, offset) is not var (start, end)) return null;
        var first = start;
        while (first < end && doc.Text[first] is ' ' or '\t') first++;
        if (first >= end || doc.Text[first] == '\r' || (first + 1 < end && doc.Text[first] == '/' && doc.Text[first + 1] == '/')) return null;
        var inside = doc.TokenAt(first);
        if (inside >= 0 && doc.Tokens[inside].Kind == GmlTokenKind.Comment && doc.Start(inside) < first && first < doc.End(inside)) return null;
        return new QuickFix("code.fix.commentLine", "", [new TextEdit(first, 0, "// ")]);
    }

    private static (int Start, int End)? LineBounds(string text, int offset)
    {
        if (offset < 0 || offset > text.Length) return null;
        var start = offset == 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;
        var end = text.IndexOf('\n', offset);
        return (start, end < 0 ? text.Length : end);
    }

    /// <summary>Known names within two edits of the word, closest first. Names of very different length are skipped without computing a distance.</summary>
    public static IEnumerable<string> Nearest(string word, IEnumerable<string> names) =>
        names.Where(n => Math.Abs(n.Length - word.Length) <= MostDistance && n != word)
            .Select(n => (Name: n, Distance: Distance(word, n)))
            .Where(x => x.Distance <= MostDistance)
            .OrderBy(x => x.Distance).ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.Name);

    public static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1));
                diagonal = above;
            }
        }

        return row[b.Length];
    }
}
