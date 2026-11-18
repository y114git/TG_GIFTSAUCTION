using G3MLib.Analyzer.Compiler;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public static partial class GmlLint
{
    private sealed partial class Run
    {
        // A var that is only declared and written, never read anywhere in the text.
        private void Unused()
        {
            foreach (var name in _locals)
            {
                if (name == "_") continue;
                var read = false;
                foreach (var t in doc.Named(name))
                {
                    if (_localDeclarations.Contains(t) || Is(t - 1, ".")) continue;
                    if (Is(t + 1, "=") && _tokens[t + 1].Kind == GmlTokenKind.Operator && StatementStart(t)) continue;
                    read = true;
                    break;
                }

                if (read || TemplateWords().Contains(name)) continue;
                var first = doc.Named(name).First(_localDeclarations.Contains);
                Add(DiagnosticSeverity.Info, UnusedLocal, doc.Start(first), doc.End(first), Loc.Current.Format("code.lint.unusedLocal", ("name", name)));
            }
        }

        // The words inside template strings ($"{name}"), which the tokenizer keeps as one token.
        private HashSet<string> TemplateWords()
        {
            if (_templateWords is not null) return _templateWords;
            _templateWords = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (_tokens[i].Kind != GmlTokenKind.TemplateString) continue;
                var text = S(i);
                for (var k = 0; k < text.Length; k++)
                {
                    if (!(char.IsLetter(text[k]) || text[k] == '_')) continue;
                    var from = k;
                    while (k < text.Length && (char.IsLetterOrDigit(text[k]) || text[k] == '_')) k++;
                    _templateWords.Add(text[from..k]);
                }
            }

            return _templateWords;
        }

        // A name this code sets (so it is no unknown name) but reads first, in straight code where the text order is the run order.
        private void ReadsBeforeAssignment()
        {
            // In an event the Create event or another one may set the name first; only a script runs its code in text order alone.
            if (context.InstanceCode) return;
            bool[]? nested = null;
            HashSet<string>? strings = null;
            foreach (var name in _assigned)
            {
                if (Known(name) || IsArgumentName(name) || TemplateWords().Contains(name)) continue;
                var uses = doc.Named(name).Where(t => !Is(t - 1, ".") && !_skip.Contains(t)).ToList();
                if (uses.Count < 2 || IsPlainWrite(uses[0])) continue;
                nested ??= Nested();
                if (uses.Any(t => nested[t])) continue;
                // variable_instance_exists(id, "hp") and the like guard the read.
                strings ??= [.. Enumerable.Range(0, _tokens.Count).Where(t => _tokens[t].Kind == GmlTokenKind.String && S(t).Length > 2).Select(t => S(t)[1..^1])];
                if (strings.Contains(name) || !uses.Skip(1).Any(IsWrite)) continue;
                Add(DiagnosticSeverity.Info, ReadBeforeAssign, doc.Start(uses[0]), doc.End(uses[0]), Loc.Current.Format("code.lint.readBeforeAssign", ("name", name)));
            }
        }

        private bool IsPlainWrite(int t) => Is(t + 1, "=") && _tokens[t + 1].Kind == GmlTokenKind.Operator && StatementStart(t);

        private void UnusedParameters()
        {
            foreach (var function in Declared)
            {
                if (function.Body < 0 || function.End < 0) continue;
                var positional = false;
                for (var j = function.Body; j < function.End && !positional; j++)
                    positional = _tokens[j].Kind == GmlTokenKind.Identifier && IsArgumentName(S(j));
                if (positional) continue;
                foreach (var (token, _) in Parameters(doc, function))
                {
                    var name = S(token);
                    if (name.StartsWith('_')) continue;
                    var used = doc.Named(name).Any(t => t != token && t > function.Open && t < function.End && !Is(t - 1, "."));
                    if (used || TemplateWords().Contains(name)) continue;
                    Add(DiagnosticSeverity.Info, UnusedParameter, doc.Start(token), doc.End(token), Loc.Current.Format("code.lint.unusedParameter", ("name", name)));
                }
            }
        }

        // var image_index or var draw_text: the local hides the built-in for the rest of the code.
        private void ShadowedNames()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in _localDeclarations.Order())
            {
                var name = S(token);
                if (!seen.Add(name) || Named.ContainsKey(name)) continue;
                var key = context.IsBuiltinFunction(name) ? "code.lint.shadowsFunction" : context.IsBuiltinVariable(name) ? "code.lint.shadowsVariable" : null;
                if (key is not null) Add(DiagnosticSeverity.Info, Shadowing, doc.Start(token), doc.End(token), Loc.Current.Format(key, ("name", name)));
            }
        }

        private static readonly HashSet<string> Comparers = new(StringComparer.Ordinal) { "==", "!=", "<", ">", "<=", ">=" };

        private static readonly HashSet<string> Before = new(StringComparer.Ordinal) { "(", ",", "&&", "||", "and", "or", "xor", "return", "if", "while", "until", "=", "?", ":", "{", ";" };

        private static readonly HashSet<string> After = new(StringComparer.Ordinal) { ")", ",", ";", "&&", "||", "and", "or", "xor", "?", "}", "then", ":" };

        // "5" == 5: a string and a number are never equal, and < or > between them stops the game. Only literals and locals set from literals are judged.
        private void Comparisons()
        {
            Dictionary<string, string?>? types = null;
            for (var j = 1; j + 1 < _tokens.Count; j++)
            {
                if (_tokens[j].Kind != GmlTokenKind.Operator || !Comparers.Contains(S(j))) continue;
                int left = j - 1, right = j + 1;
                if (left > 0 && !Before.Contains(S(left - 1))) continue;
                if (right + 1 < _tokens.Count && !After.Contains(S(right + 1))) continue;
                types ??= [];
                var a = TypeOf(left, types);
                var b = TypeOf(right, types);
                if (a is null || b is null || a == b) continue;
                Add(DiagnosticSeverity.Info, StringNumberCompare, doc.Start(left), doc.End(right), Loc.Current["code.lint.stringNumber"]);
            }
        }

        private string? TypeOf(int t, Dictionary<string, string?> types) => _tokens[t].Kind switch
        {
            GmlTokenKind.Number => "real",
            GmlTokenKind.String or GmlTokenKind.TemplateString => "string",
            GmlTokenKind.Identifier when _locals.Contains(S(t)) => types.TryGetValue(S(t), out var known) ? known : types[S(t)] = LocalType(S(t)),
            _ => null,
        };

        // The one literal type every assignment of a local gives it, or null. Null too when the name is a parameter somewhere or is used outside the function
        // its var is in: the same name there may be another variable.
        private string? LocalType(string name)
        {
            var declarations = _localDeclarations.Where(t => S(t) == name).ToList();
            if (declarations.Count == 0) return null;
            var scope = FunctionAt(declarations[0]);
            if (Declared.Any(f => Parameters(doc, f).Any(p => S(p.Token) == name))) return null;
            string? type = null;
            foreach (var t in doc.Named(name))
            {
                if (Is(t - 1, ".")) continue;
                if (FunctionAt(t) != scope) return null;
                var declaration = _localDeclarations.Contains(t);
                if (declaration && !Is(t + 1, "=")) continue;
                if (!declaration && !IsWrite(t)) continue;
                if (!Is(t + 1, "=") || t + 2 >= _tokens.Count) return null;
                var value = t + 2;
                var literal = _tokens[value].Kind switch
                {
                    GmlTokenKind.Number => "real",
                    GmlTokenKind.String => "string",
                    _ => null,
                };
                if (literal is null || value + 1 < _tokens.Count && !(Is(value + 1, ";") || Is(value + 1, ",") || Is(value + 1, "}"))) return null;
                if (type is not null && type != literal) return null;
                type = literal;
            }

            return type;
        }

        // The innermost function whose parameters or body hold the token, or null outside every function. Functions come in text order, so a nested one is found last.
        private GmlFunction? FunctionAt(int t)
        {
            GmlFunction? found = null;
            foreach (var f in Declared)
                if (f.Keyword <= t && t <= (f.End >= 0 ? f.End : f.Close)) found = f;
            return found;
        }
    }
}
