using G3MLib.Analyzer.Compiler;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>A function declared in a GML text: its tokens, from the function keyword to the closing brace of its body.</summary>
/// <param name="Keyword">The function keyword.</param>
/// <param name="Name">The name token (function name() or name = function()), or -1 for an anonymous function or a method of another value.</param>
/// <param name="Open">The ( of the parameters.</param>
/// <param name="Close">The ) of the parameters.</param>
/// <param name="Body">The { of the body, or -1.</param>
/// <param name="End">The } of the body, or -1.</param>
/// <param name="Constructor">Whether the function is marked constructor.</param>
public sealed record GmlFunction(int Keyword, int Name, int Open, int Close, int Body, int End, bool Constructor);

public static partial class GmlLint
{
    /// <summary>The functions the text declares, in text order.</summary>
    public static IReadOnlyList<GmlFunction> Functions(GmlDoc doc)
    {
        var found = new List<GmlFunction>();
        var count = doc.Tokens.Count;
        for (var i = 0; i < count; i++)
        {
            if (doc.Tokens[i].Kind != GmlTokenKind.Keyword || doc.Slice(i) != "function") continue;
            var name = -1;
            var j = i + 1;
            if (j < count && doc.Tokens[j].Kind == GmlTokenKind.Identifier) name = j++;
            else if (i >= 2 && doc.IsPunct(i - 1, '=') && doc.Tokens[i - 2].Kind == GmlTokenKind.Identifier && !(i >= 3 && doc.IsPunct(i - 3, '.'))) name = i - 2;
            if (j >= count || !doc.IsPunct(j, '(') || doc.PairOf(j) is not (var close and >= 0)) continue;
            var k = close + 1;
            // Inheritance: function Child() : Parent(a) constructor
            if (k < count && doc.IsPunct(k, ':'))
            {
                k++;
                if (k < count && doc.Tokens[k].Kind == GmlTokenKind.Identifier) k++;
                if (k < count && doc.IsPunct(k, '(') && doc.PairOf(k) is var parent and >= 0) k = parent + 1;
            }

            var constructor = k < count && doc.Slice(k) == "constructor";
            if (constructor) k++;
            var body = k < count && doc.IsPunct(k, '{') ? k : -1;
            found.Add(new GmlFunction(i, name, j, close, body, body < 0 ? -1 : doc.PairOf(body), constructor));
        }

        return found;
    }

    /// <summary>The name token of each parameter; a bracket inside a default value is skipped, so only a name that starts a parameter counts.</summary>
    public static IReadOnlyList<(int Token, bool Default)> Parameters(GmlDoc doc, GmlFunction function)
    {
        var found = new List<(int, bool)>();
        var expectName = true;
        for (var j = function.Open + 1; j < function.Close; j++)
        {
            if (IsBracket(doc, j) && doc.PairOf(j) is var pair and > 0 && doc.Text[doc.Start(j)] is '(' or '[' or '{') { j = pair; continue; }
            if (doc.IsPunct(j, ',')) expectName = true;
            else if (expectName && doc.Tokens[j].Kind == GmlTokenKind.Identifier)
            {
                found.Add((j, j + 1 < function.Close && doc.IsPunct(j + 1, '=')));
                expectName = false;
            }
        }

        return found;
    }

    private static bool IsBracket(GmlDoc doc, int token) => doc.Tokens[token].Kind == GmlTokenKind.Separator;

    private static bool IsArgumentName(string name) => name is "argument" or "argument_count" || IsNumberedArgument(name);

    private static bool IsNumberedArgument(string name) => name.StartsWith("argument", StringComparison.Ordinal) && name.Length > 8 && name.AsSpan(8).IndexOfAnyExceptInRange('0', '9') < 0;

    /// <summary>
    /// The fewest arguments a call of the function must pass, or null when that is not certain: GameMaker itself passes undefined for a missing argument,
    /// so a parameter counts as required only when it has no default and the body never treats it as optional (undefined checks, argument_count, argument[]).
    /// </summary>
    public static int? RequiredArguments(GmlDoc doc, GmlFunction function)
    {
        if (function.Body < 0 || function.End < 0) return null;
        var parameters = Parameters(doc, function);
        var required = parameters.Count;
        for (var p = 0; p < parameters.Count; p++)
        {
            if (!parameters[p].Default) continue;
            required = p;
            break;
        }

        for (var j = function.Body; j < function.End; j++)
            if (doc.Tokens[j].Kind == GmlTokenKind.Identifier && IsArgumentName(doc.Slice(j))) return null;
        for (var p = 0; p < required; p++)
        {
            var name = doc.Slice(parameters[p].Token);
            foreach (var t in doc.Named(name))
            {
                if (t <= function.Body || t >= function.End || doc.IsPunct(t - 1, '.')) continue;
                if (TreatedAsOptional(doc, t)) return p;
            }
        }

        return required;
    }

    // is_undefined(p), typeof(p), is_string(p) and the like, p == undefined, undefined != p, p ?? x, p ??= x.
    private static bool TreatedAsOptional(GmlDoc doc, int t)
    {
        var count = doc.Tokens.Count;
        if (t >= 2 && doc.IsPunct(t - 1, '(') && doc.Slice(t - 2) is var call && (call.StartsWith("is_", StringComparison.Ordinal) || call is "typeof" or "instanceof")) return true;
        var next = t + 1 < count ? doc.Slice(t + 1) : "";
        if (next is "??" or "??=") return true;
        if (next is "==" or "!=" && t + 2 < count && doc.Slice(t + 2) == "undefined") return true;
        return t >= 2 && doc.Slice(t - 1) is "==" or "!=" && doc.Slice(t - 2) == "undefined";
    }

    private enum Path { Falls, Ends, Unknown }

    private sealed partial class Run
    {
        private IReadOnlyList<GmlFunction> Declared => field ??= Functions(doc);

        private Dictionary<int, GmlFunction> ByKeyword => field ??= Declared.ToDictionary(f => f.Keyword);

        private static readonly HashSet<string> StatementWords = new(StringComparer.Ordinal)
        {
            "if", "else", "return", "exit", "break", "continue", "throw", "while", "for", "do", "until", "repeat", "with", "switch", "case", "default",
            "try", "catch", "finally", "var", "static", "globalvar", "enum", "delete",
        };

        private void Flow()
        {
            MissingReturns();
            ReadsBeforeAssignment();
            UnusedParameters();
            ShadowedNames();
            Comparisons();
        }

        // The statements from a token up to (not including) a limit: whether the code after them can be reached.
        private Path Block(int from, int to)
        {
            var unknown = false;
            for (var i = from; i < to;)
            {
                var path = Statement(i, to, out var next);
                if (path == Path.Ends) return Path.Ends;
                if (path == Path.Unknown) unknown = true;
                i = Math.Max(next, i + 1);
            }

            return unknown ? Path.Unknown : Path.Falls;
        }

        private Path Statement(int i, int limit, out int next)
        {
            next = limit;
            if (i >= limit) return Path.Falls;
            if (Is(i, ";"))
            {
                next = i + 1;
                return Path.Falls;
            }

            if (Is(i, "{"))
            {
                if (doc.PairOf(i) is not (var close and >= 0) || close >= limit) return Path.Unknown;
                next = close + 1;
                return Block(i + 1, close);
            }

            if (_tokens[i].Kind == GmlTokenKind.Preprocessor)
            {
                next = i + 1;
                return Path.Unknown;
            }

            if (_tokens[i].Kind != GmlTokenKind.Keyword)
            {
                next = EndOf(i, limit);
                return Path.Falls;
            }

            switch (S(i))
            {
                case "return" or "throw":
                    next = EndOf(i, limit);
                    return Path.Ends;
                case "exit" or "break" or "continue":
                    next = Is(i + 1, ";") ? i + 2 : i + 1;
                    return S(i) == "exit" ? Path.Ends : Path.Unknown;
                case "if":
                    {
                        if (!Is(i + 1, "(") || doc.PairOf(i + 1) is not (var close and >= 0) || close + 1 >= limit || _tokens[close + 1].Kind == GmlTokenKind.Operator || IsKeyword(close + 1, "and", "or", "xor", "then"))
                        {
                            next = EndOf(i, limit);
                            return Path.Unknown;
                        }

                        var then = Statement(close + 1, limit, out var after);
                        if (!IsKeyword(after, "else"))
                        {
                            next = after;
                            return then == Path.Unknown ? Path.Unknown : Path.Falls;
                        }

                        var otherwise = Statement(after + 1, limit, out next);
                        return then == Path.Ends && otherwise == Path.Ends ? Path.Ends : then == Path.Unknown || otherwise == Path.Unknown ? Path.Unknown : Path.Falls;
                    }
                case "while" or "repeat" or "with" or "for":
                    {
                        if (!Is(i + 1, "(") || doc.PairOf(i + 1) is not (var close and >= 0) || close + 1 >= limit)
                        {
                            next = EndOf(i, limit);
                            return Path.Unknown;
                        }

                        Statement(close + 1, limit, out next);
                        // A loop that can not end by its condition (while (true), for (;;)) only ends by a jump inside it.
                        return Endless(S(i), i + 1, close) ? Path.Unknown : Path.Falls;
                    }
                case "do":
                    {
                        var body = Statement(i + 1, limit, out var after);
                        if (!IsKeyword(after, "until") || !Is(after + 1, "(") || doc.PairOf(after + 1) is not (var close and >= 0)) return Path.Unknown;
                        next = Is(close + 1, ";") ? close + 2 : close + 1;
                        if (body == Path.Ends) return Path.Ends; // the body runs once before the condition is read
                        return close == after + 3 && S(after + 2) is "false" or "0" ? Path.Unknown : Path.Falls;
                    }
                case "function" when ByKeyword.TryGetValue(i, out var function) && function.End > 0:
                    next = function.End + 1;
                    return Path.Falls;
                case "enum" when Is(i + 2, "{") && doc.PairOf(i + 2) is var end and >= 0:
                    next = Is(end + 1, ";") ? end + 2 : end + 1;
                    return Path.Falls;
                case "switch" or "try" or "catch" or "finally" or "else" or "case" or "default" or "until" or "function":
                    return Path.Unknown;
                default:
                    next = EndOf(i, limit);
                    return Path.Falls;
            }
        }

        private bool Endless(string loop, int open, int close)
        {
            if (loop == "while") return close == open + 2 && S(open + 1) is "true" or "1";
            if (loop != "for") return false;
            // for (init; condition; step): the condition is empty or true.
            var semicolons = new List<int>();
            for (var j = open + 1; j < close; j++)
            {
                if (IsOpener(j) && doc.PairOf(j) is var pair and >= 0) j = pair;
                else if (Is(j, ";")) semicolons.Add(j);
            }

            return semicolons.Count == 2 && (semicolons[1] == semicolons[0] + 1 || semicolons[1] == semicolons[0] + 2 && S(semicolons[0] + 1) is "true" or "1");
        }

        // The token after a simple statement: past its ;, or the } that closes the block, or the keyword that starts the next statement on a line without a ;.
        private int EndOf(int i, int limit)
        {
            for (var j = i; j < limit; j++)
            {
                if (IsOpener(j) && doc.PairOf(j) is var pair and >= 0)
                {
                    j = pair;
                    continue;
                }

                if (Is(j, ";")) return j + 1;
                if (Is(j, "}")) return j;
                if (j > i && _tokens[j].Kind == GmlTokenKind.Keyword && StatementWords.Contains(S(j))) return j;
            }

            return limit;
        }

        // A function that returns a value on some paths and can reach the end of its body, where it returns undefined.
        private void MissingReturns()
        {
            foreach (var function in Declared)
            {
                if (function.Constructor || function.Body < 0 || function.End < 0) continue;
                bool value = false, complex = false;
                for (var j = function.Body + 1; j < function.End && !complex; j++)
                {
                    if (IsKeyword(j, "function") && ByKeyword.TryGetValue(j, out var inner) && inner.End > j)
                    {
                        j = inner.End;
                        continue;
                    }

                    var kind = _tokens[j].Kind;
                    if (kind == GmlTokenKind.Preprocessor || IsKeyword(j, "try", "switch") || kind == GmlTokenKind.Identifier && S(j) is "show_error" or "game_end" or "throw") complex = true;
                    else if (IsKeyword(j, "return") && !Is(j + 1, ";") && !Is(j + 1, "}")) value = true;
                }

                if (!value || complex || Block(function.Body + 1, function.End) != Path.Falls) continue;
                var at = function.Name >= 0 ? function.Name : function.Keyword;
                Add(DiagnosticSeverity.Info, MissingReturn, doc.Start(at), doc.End(at), Loc.Current.Format("code.lint.missingReturn", ("name", S(at))));
            }
        }

        // The token after a whole switch statement or try statement with its catch and finally parts; the end of the text when the brackets do not pair.
        private int Compound(int keyword)
        {
            var j = keyword + 1;
            while (j < _tokens.Count)
            {
                if (Is(j, "(") || Is(j, "{"))
                {
                    if (doc.PairOf(j) is not (var pair and >= 0)) return _tokens.Count;
                    j = pair + 1;
                    if (Is(pair, "}") && !IsKeyword(j, "catch", "finally")) return j;
                }
                else if (IsKeyword(j, "catch", "finally")) j++;
                else return j;
            }

            return _tokens.Count;
        }

        // Tokens inside a loop, a with, a switch, a try or a function body: their order in the text is not the order they run in.
        private bool[] Nested()
        {
            var nested = new bool[_tokens.Count];
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (!IsKeyword(i, "while", "for", "do", "repeat", "with", "switch", "try", "function")) continue;
                if (IsKeyword(i, "function"))
                {
                    if (!ByKeyword.TryGetValue(i, out var function) || function.End < 0) continue;
                    Array.Fill(nested, true, i, function.End - i + 1);
                    continue;
                }

                int next;
                if (S(i) is "switch" or "try") next = Compound(i);
                else Statement(i, _tokens.Count, out next);
                Array.Fill(nested, true, i, Math.Min(next, _tokens.Count) - i);
            }

            return nested;
        }
    }
}
