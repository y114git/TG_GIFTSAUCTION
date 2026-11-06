using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>The call the caret is inside of: the function name and the zero based index of the argument being typed.</summary>
public readonly record struct CallContext(string Name, int Argument);

/// <summary>A function as the hint shows it. <paramref name="Parameters"/> are the argument names, the first <paramref name="Required"/> of them must be given.</summary>
public sealed record Signature(string Name, IReadOnlyList<string> Parameters, int Required, bool Variadic)
{
    /// <summary>name(a, b, [c], ...) with the optional ones in square brackets.</summary>
    public string Label => Name + "(" + string.Join(", ", Parameters.Select((p, i) => i < Required ? p : "[" + p + "]").Concat(Variadic ? ["..."] : [])) + ")";

    /// <summary>The parameter the argument index points at; the last one for a variadic function, -1 when the call has too many arguments.</summary>
    public int ActiveParameter(int argument) => argument < Parameters.Count ? argument : Variadic && Parameters.Count > 0 ? Parameters.Count - 1 : -1;

    /// <summary>A signature from the argument counts a function is known by; the names are argument0, argument1 and so on.</summary>
    public static Signature FromCounts(string name, int min, int max)
    {
        var shown = max < 0 ? min : max;
        return new Signature(name, [.. Enumerable.Range(0, shown).Select(i => "argument" + i)], Math.Min(min, shown), max < 0);
    }
}

/// <summary>Finds the call around the caret, for the parameter hint that shows while arguments are typed.</summary>
public static class GmlSignatures
{
    public static CallContext? CallAt(GmlDoc doc, int offset) => Scan(doc, BeforeOffset(doc, offset));

    private static int BeforeOffset(GmlDoc doc, int offset)
    {
        int low = 0, high = doc.Tokens.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (doc.End(mid) <= offset) low = mid + 1; else high = mid;
        }

        return low - 1;
    }

    private static CallContext? Scan(GmlDoc doc, int from)
    {
        int depth = 0, commas = 0;
        for (var i = from; i >= 0; i--)
        {
            var kind = doc.Tokens[i].Kind;
            if (kind != GmlTokenKind.Separator) continue;
            var c = doc.Text[doc.Start(i)];
            if (c is ')' or ']' or '}') depth++;
            else if (c is '(' or '[' or '{')
            {
                if (depth > 0) depth--;
                else if (c == '(' && i > 0 && doc.Tokens[i - 1].Kind == GmlTokenKind.Identifier) return new CallContext(doc.Slice(i - 1), commas);
                else if (c != '(') return null;
                else commas = 0;
            }
            else if (depth == 0 && c == ',') commas++;
            else if (depth == 0 && c == ';') return null;
        }

        return null;
    }
}
