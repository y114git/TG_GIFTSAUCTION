using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

public static partial class GmlLint
{
    private sealed partial class Run
    {
        // Everything the text itself declares or assigns, and the tokens that are names in a declaration rather than uses.
        private void Declarations()
        {
            var (locals, globals) = GmlScopes.Declarations(doc.Text, _tokens);
            foreach (var name in locals.Keys.Concat(globals.Keys)) _declared.Add(name);
            for (var i = 0; i < _tokens.Count; i++)
            {
                var kind = _tokens[i].Kind;
                var word = kind is GmlTokenKind.Keyword or GmlTokenKind.Preprocessor ? S(i) : "";
                if (word is "#region" or "#endregion" or "#define")
                {
                    // The rest of the line is a label, not code.
                    var line = LineEnd(doc.End(i));
                    for (var j = i + 1; j < _tokens.Count && doc.Start(j) < line; j++) _skip.Add(j);
                    if (word == "#define" && i + 1 < _tokens.Count) _declared.Add(S(i + 1));
                }
                else if (word == "#macro" && i + 1 < _tokens.Count)
                {
                    // #macro NAME value, or #macro Config:NAME value.
                    var name = i + 3 < _tokens.Count && Is(i + 2, ":") ? i + 3 : i + 1;
                    for (var j = i + 1; j <= name; j++) _skip.Add(j);
                    _declared.Add(S(name));
                }
                else if (word == "enum" && i + 2 < _tokens.Count && _tokens[i + 1].Kind == GmlTokenKind.Identifier && Is(i + 2, "{"))
                {
                    _declared.Add(S(i + 1));
                    _skip.Add(i + 1);
                    var close = doc.PairOf(i + 2);
                    for (var j = i + 3; j < close; j++)
                        if (_tokens[j].Kind == GmlTokenKind.Identifier && (Is(j - 1, "{") || Is(j - 1, ","))) _skip.Add(j);
                }
                else if (word == "function" && i + 1 < _tokens.Count && _tokens[i + 1].Kind == GmlTokenKind.Identifier)
                {
                    _declared.Add(S(i + 1));
                    _skip.Add(i + 1);
                }
                else if (word == "catch" && i + 2 < _tokens.Count && Is(i + 1, "(") && _tokens[i + 2].Kind == GmlTokenKind.Identifier)
                {
                    _declared.Add(S(i + 2));
                    _skip.Add(i + 2);
                }
                else if (word is "var" or "static") DeclaredLocals(i, word == "var");
                else if (kind == GmlTokenKind.Identifier)
                {
                    if (i + 1 < _tokens.Count && _tokens[i + 1].Kind == GmlTokenKind.Operator && AssignOps.Contains(S(i + 1))) _assigned.Add(S(i));
                    else if (i > 0 && _tokens[i - 1].Kind == GmlTokenKind.Operator && S(i - 1) is "++" or "--") _assigned.Add(S(i));
                    // A struct literal key: { name: or , name:. Inside the methods of the struct the key reads as a plain name.
                    if (Is(i + 1, ":") && (Is(i - 1, "{") || Is(i - 1, ",")))
                    {
                        _skip.Add(i);
                        _declared.Add(S(i));
                    }
                }
            }
        }

        // The names a var or static statement declares; var names are checked for use later.
        private void DeclaredLocals(int keyword, bool isVar)
        {
            var depth = 0;
            var expectName = true;
            for (var j = keyword + 1; j < _tokens.Count; j++)
            {
                var kind = _tokens[j].Kind;
                var piece = S(j);
                if (doc.IsOpener(j)) depth++;
                else if (kind == GmlTokenKind.Separator && piece is ")" or "]" or "}")
                {
                    if (--depth < 0) return;
                }
                else if (depth == 0 && piece == ";") return;
                else if (depth == 0 && piece == ",") expectName = true;
                else if (depth == 0 && piece == "=") expectName = false;
                else if (depth == 0 && expectName && kind == GmlTokenKind.Identifier)
                {
                    _declared.Add(piece);
                    if (isVar)
                    {
                        _locals.Add(piece);
                        _localDeclarations.Add(j);
                    }

                    expectName = false;
                }
                else if (depth == 0 && kind == GmlTokenKind.Keyword && piece != "function" && piece != "new") return;
            }
        }

        private static readonly HashSet<string> DynamicSetters = new(StringComparer.Ordinal) { "variable_instance_set", "variable_struct_set", "struct_set", "variable_global_set" };

        // variable_instance_set(id, "hp", 1) and the like create the name; reading it later is no unknown name.
        private void DynamicNames()
        {
            for (var i = 0; i + 2 < _tokens.Count; i++)
            {
                if (_tokens[i].Kind != GmlTokenKind.Identifier || !DynamicSetters.Contains(S(i)) || !Is(i + 1, "(") || doc.PairOf(i + 1) is not (var close and >= 0)) continue;
                var wanted = S(i) == "variable_global_set" ? 0 : 1;
                var argument = 0;
                for (var j = i + 2; j < close; j++)
                {
                    if (IsOpener(j) && doc.PairOf(j) is var pair and >= 0) { j = pair; continue; }
                    if (Is(j, ",")) { argument++; continue; }
                    if (argument == wanted && _tokens[j].Kind == GmlTokenKind.String && (Is(j + 1, ",") || j + 1 == close) && S(j) is { Length: > 2 } literal && GmlSymbols.IsValidName(literal[1..^1]))
                        _declared.Add(literal[1..^1]);
                    if (argument > wanted) break;
                }
            }
        }
    }
}
