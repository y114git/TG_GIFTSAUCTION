using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>The call the caret is inside of: the function name and the zero based index of the argument being typed.</summary>
public readonly record struct CallContext(string Name, int Argument);

/// <summary>A function as the hint shows it. <paramref name="Parameters"/> are the argument names, the first <paramref name="Required"/> of them must be given.</summary>
public sealed record Signature(string Name, IReadOnlyList<string> Parameters, int Required, bool Variadic)
{
    /// <summary>name(a, b, [c], ...) with the optional ones in square brackets.</summary>
    public string Label => Name + "(" + string.Join(", ", Parameters.Select((p, i) => i < Required ? p : "[" + p + "]").Concat(Variadic ? ["..."] : [])) + ")";

    /// <summary>The label cut into pieces for display; the piece of the parameter being typed is marked active.</summary>
    public IReadOnlyList<(string Text, bool Active)> Segments(int argument)
    {
        var pieces = new List<(string, bool)> { (Name + "(", false) };
        var active = argument < Parameters.Count ? argument : -1;
        for (var i = 0; i < Parameters.Count; i++)
        {
            if (i > 0) pieces.Add((", ", false));
            pieces.Add((i < Required ? Parameters[i] : "[" + Parameters[i] + "]", i == active));
        }

        if (Variadic)
        {
            if (Parameters.Count > 0) pieces.Add((", ", false));
            pieces.Add(("...", argument >= Parameters.Count));
        }

        pieces.Add((")", false));
        return pieces;
    }

    /// <summary>The parameter the argument index points at; the last one for a variadic function, -1 when the call has too many arguments.</summary>
    public int ActiveParameter(int argument) => argument < Parameters.Count ? argument : Variadic && Parameters.Count > 0 ? Parameters.Count - 1 : -1;

    /// <summary>A signature from the argument counts a function is known by; the names are argument0, argument1 and so on.</summary>
    public static Signature FromCounts(string name, int min, int max)
    {
        // The library marks "any number" with a huge or negative maximum; more than 32 named arguments is never real.
        var variadic = max is < 0 or > 32;
        var shown = Math.Clamp(variadic ? min : max, 0, 32);
        return new Signature(name, [.. Enumerable.Range(0, shown).Select(i => "argument" + i)], Math.Min(min, shown), variadic);
    }
}

/// <summary>Finds the call around the caret, for the parameter hint that shows while arguments are typed.</summary>
public static class GmlSignatures
{
    public static CallContext? CallAt(GmlDoc doc, int offset) => Scan(doc, BeforeOffset(doc, offset));

    /// <summary>The signature of a function declared in this text (function name(a, b = 1)), with its real parameter names; null when there is none. Parameters with a default are optional.</summary>
    public static Signature? FromDoc(GmlDoc doc, string name)
    {
        foreach (var token in doc.Named(name))
        {
            var viaKeyword = token > 0 && doc.Tokens[token - 1].Kind == GmlTokenKind.Keyword && doc.Slice(token - 1) == "function";
            var viaAssign = token + 2 < doc.Tokens.Count && doc.IsPunct(token + 1, '=') && doc.Slice(token + 2) == "function";
            if (!viaKeyword && !viaAssign) continue;
            var open = token + (viaAssign ? 3 : 1);
            if (open < doc.Tokens.Count && doc.IsPunct(open, '(')) return Parameters(doc, name, open);
        }

        return null;
    }

    private static Signature Parameters(GmlDoc doc, string name, int open)
    {
        var names = new List<string>();
        int depth = 0, required = -1;
        var expectName = true;
        for (var j = open; j < doc.Tokens.Count; j++)
        {
            var kind = doc.Tokens[j].Kind;
            var bracket = kind == GmlTokenKind.Separator ? doc.Text[doc.Start(j)] : ' ';
            if (bracket is '(' or '[' or '{') depth++;
            else if (bracket is ')' or ']' or '}')
            {
                if (--depth == 0) break;
            }
            else if (depth == 1 && doc.IsPunct(j, ',')) expectName = true;
            else if (depth == 1 && expectName && kind == GmlTokenKind.Identifier)
            {
                names.Add(doc.Slice(j));
                expectName = false;
            }
            else if (depth == 1 && doc.IsPunct(j, '=') && required < 0) required = names.Count - 1;
        }

        return new Signature(name, names, required < 0 ? names.Count : required, false);
    }

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
