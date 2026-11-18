using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>
/// Finds, from the tokens of one script, the names GameMaker colours apart from other variables: locals (declared with var or static) and globals
/// (global.name and globalvar). It reads tokens only, so a local of another event or a name that is both stays on the side of the first reading.
/// shortcut: one pass per script text with no scope blocks, so a local keeps its colour after the block it was declared in; fine until the editor has a real scope model.
/// </summary>
public static class GmlScopes
{
    public const string Local = "Local";
    public const string Global = "Global";

    /// <summary>Token index to <see cref="Local"/> or <see cref="Global"/>, for identifier tokens only.</summary>
    public static Dictionary<int, string> Find(string text, IReadOnlyList<GmlToken> tokens)
    {
        string T(int i) => text[tokens[i].Span.StartOffset..tokens[i].Span.EndOffset];
        var (locals, globals) = Declarations(text, tokens);
        var result = new Dictionary<int, string>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != GmlTokenKind.Identifier) continue;
            var name = T(i);
            var member = i > 0 && T(i - 1) == ".";
            if (member && i > 1 && T(i - 2) == "global") result[i] = Global;
            else if (!member && locals.ContainsKey(name)) result[i] = Local;
            else if (!member && globals.ContainsKey(name)) result[i] = Global;
        }

        return result;
    }

    /// <summary>The declared local names (var, static, function parameters) and global names (globalvar) with the token index of their first declaration.</summary>
    public static (Dictionary<string, int> Locals, Dictionary<string, int> Globals) Declarations(string text, IReadOnlyList<GmlToken> tokens)
    {
        string T(int i) => text[tokens[i].Span.StartOffset..tokens[i].Span.EndOffset];
        var locals = new Dictionary<string, int>(StringComparer.Ordinal);
        var globals = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != GmlTokenKind.Keyword) continue;
            var word = T(i);
            if (word is "var" or "static") Names(text, tokens, i, j => locals.TryAdd(T(j), j));
            else if (word == "globalvar") Names(text, tokens, i, j => globals.TryAdd(T(j), j));
            else if (word == "function") Parameters(i, locals);
        }

        return (locals, globals);

        // The names in the brackets after function (and an optional name): the identifier that starts each comma separated part.
        void Parameters(int keyword, Dictionary<string, int> into)
        {
            var open = keyword + 1;
            if (open < tokens.Count && tokens[open].Kind == GmlTokenKind.Identifier) open++;
            if (open >= tokens.Count || T(open) != "(") return;
            var depth = 0;
            var expectName = true;
            for (var j = open; j < tokens.Count; j++)
            {
                var piece = T(j);
                if (tokens[j].Kind == GmlTokenKind.Separator && piece[0] is '(' or '[' or '{') depth++; // [# [? [| [$ [@ open too
                else if (tokens[j].Kind == GmlTokenKind.Separator && piece is ")" or "]" or "}")
                {
                    if (--depth == 0) return;
                }
                else if (depth == 1 && piece == ",") expectName = true;
                else if (depth == 1 && expectName && tokens[j].Kind == GmlTokenKind.Identifier)
                {
                    into.TryAdd(piece, j);
                    expectName = false;
                }
            }
        }
    }

    /// <summary>The token of every name a var statement declares, so a name declared by var in two functions is found in both.</summary>
    public static HashSet<int> VarTokens(string text, IReadOnlyList<GmlToken> tokens)
    {
        var found = new HashSet<int>();
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].Kind == GmlTokenKind.Keyword && text.AsSpan(tokens[i].Span.StartOffset, tokens[i].Span.EndOffset - tokens[i].Span.StartOffset).SequenceEqual("var")) Names(text, tokens, i, j => found.Add(j));
        return found;
    }

    // The names after var, static or globalvar: the identifier at the start of each comma separated part, up to the end of the statement.
    private static void Names(string text, IReadOnlyList<GmlToken> tokens, int keyword, Action<int> name)
    {
        var depth = 0;
        var expectName = true;
        for (var j = keyword + 1; j < tokens.Count; j++)
        {
            var kind = tokens[j].Kind;
            var piece = text[tokens[j].Span.StartOffset..tokens[j].Span.EndOffset];
            if (kind == GmlTokenKind.Separator && piece[0] is '(' or '[' or '{') depth++; // [# [? [| [$ [@ open too
            else if (kind == GmlTokenKind.Separator && piece is ")" or "]" or "}")
            {
                if (--depth < 0) return;
            }
            else if (depth == 0 && kind == GmlTokenKind.Separator && piece == ";") return;
            else if (depth == 0 && piece == ",") expectName = true;
            else if (depth == 0 && piece == "=") expectName = false;
            else if (depth == 0 && expectName && kind == GmlTokenKind.Identifier)
            {
                name(j);
                expectName = false;
            }
            else if (depth == 0 && kind == GmlTokenKind.Keyword && piece != "function" && piece != "new") return;
        }
    }
}
