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
                fixes.Add(new QuickFix("code.fix.declareLocal", name, [new TextEdit(StatementStart(doc, token), 0, "var " + name + " = 0;" + LineBreak(doc))]));
                fixes.Add(new QuickFix("code.fix.declareGlobal", name, [new TextEdit(doc.Start(token), 0, "global.")]));
            }
            else if (!declared && !isKnown(name))
            {
                foreach (var near in Nearest(name, knownNames).Take(3)) fixes.Add(new QuickFix("code.fix.rename", near, [new TextEdit(doc.Start(token), name.Length, near)]));
            }
        }

        if (lineHasProblem && LineBounds(doc.Text, offset) is var (start, _)) fixes.Add(new QuickFix("code.fix.commentLine", "", [new TextEdit(start, 0, "// ")]));
        return fixes;
    }

    private static bool IsCall(GmlDoc doc, int token) => token + 1 < doc.Tokens.Count && doc.IsPunct(token + 1, '(');

    private static string LineBreak(GmlDoc doc) => doc.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    // The start of the line the token is on, so the declaration lands above its first use with the same indentation left to the user.
    private static int StatementStart(GmlDoc doc, int token) => doc.Start(token) is var at && at > 0 ? doc.Text.LastIndexOf('\n', at - 1) + 1 : 0;

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
