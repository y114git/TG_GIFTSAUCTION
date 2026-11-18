using G3MLib.Analyzer.Compiler;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public static partial class GmlLint
{
    private sealed partial class Run
    {
        private void Name(int i)
        {
            if (_skip.Contains(i) || Is(i - 1, ".") || doc.IsLiteralPiece(i)) return;
            var name = S(i);
            if (Is(i + 1, "("))
            {
                if (IsKeyword(i - 1, "function")) return;
                Call(i, name);
                return;
            }

            if (IsWrite(i)) ReadOnlyWrite(i, name);
            else if (!Is(i + 1, ".") && !Known(name) && !_assigned.Contains(name) && !IsArgument(name))
                Unknown(i, name, UnknownVariable, "code.lint.unknownVariable");
        }

        private bool Known(string name) => Always.Contains(name) || _declared.Contains(name) || context.IsKnown(name);

        private static bool IsArgument(string name) => name.StartsWith("argument", StringComparison.Ordinal) && name.Length > 8 && name.AsSpan(8).IndexOfAnyExceptInRange('0', '9') < 0;

        // An assignment to the name: name op= value, name++ or ++name, and name = value where = starts a statement (in a condition it compares).
        private bool IsWrite(int i)
        {
            if (i > 0 && _tokens[i - 1].Kind == GmlTokenKind.Operator && S(i - 1) is "++" or "--") return true;
            if (i + 1 >= _tokens.Count || _tokens[i + 1].Kind != GmlTokenKind.Operator || !AssignOps.Contains(S(i + 1))) return false;
            return S(i + 1) != "=" || StatementStart(i);
        }

        private bool StatementStart(int i) => i == 0 || Is(i - 1, ";") || Is(i - 1, "{") || Is(i - 1, "}") || Is(i - 1, ")") || Is(i - 1, ":") || IsKeyword(i - 1, "else", "do", "then");

        private void ReadOnlyWrite(int i, string name)
        {
            if (_declared.Contains(name) || !context.IsReadOnly(name)) return;
            Add(DiagnosticSeverity.Warning, ReadOnly, doc.Start(i), doc.End(i), Loc.Current.Format("code.lint.readOnly", ("name", name)));
        }

        private void Call(int i, string name)
        {
            if (!Known(name) && !_assigned.Contains(name))
            {
                Unknown(i, name, UnknownFunction, "code.lint.unknownFunction");
                return;
            }

            OwnCall(i, name);
            // The text or the game may define its own function with a built-in name; only a plain built-in call is judged.
            if (_declared.Contains(name) || _assigned.Contains(name)) return;
            if (context.IsDeprecated(name)) Add(DiagnosticSeverity.Info, Deprecated, doc.Start(i), doc.End(i), Loc.Current.Format("code.lint.deprecated", ("name", name)));
            if (context.ArgumentRange(name) is not { } range || Arguments(i + 1) is not { } count) return;
            if (count >= range.Min && count <= range.Max) return;
            var expected = range.Max == int.MaxValue ? Loc.Current.Format("code.lint.atLeast", ("min", range.Min))
                : range.Min == range.Max ? range.Min.ToString(System.Globalization.CultureInfo.CurrentCulture)
                : Loc.Current.Format("code.lint.between", ("min", range.Min), ("max", range.Max));
            var close = doc.PairOf(i + 1);
            Add(DiagnosticSeverity.Warning, ArgumentCount, doc.Start(i), doc.End(close), Loc.Current.Format("code.lint.argumentCount", ("name", name), ("expected", expected), ("count", count)));
        }

        // The number of arguments of the call whose ( is the token, or null when the brackets do not pair or a comma trails.
        private int? Arguments(int open)
        {
            var close = doc.PairOf(open);
            if (close < 0) return null;
            if (close == open + 1) return 0;
            if (Is(close - 1, ",")) return null;
            var count = 1;
            for (var j = open + 1; j < close; j++)
            {
                if (IsOpener(j) && doc.PairOf(j) is var pair and >= 0) j = pair;
                else if (Is(j, ",")) count++;
            }

            return count;
        }

        private bool IsOpener(int j) => doc.IsOpener(j);

        private void Unknown(int i, string name, string code, string key)
        {
            var message = Loc.Current.Format(key, ("name", name));
            QuickFix? fix = null;
            if (_suggestions++ < MostSuggestions && GmlQuickFixes.Nearest(name, context.Names.Concat(_declared)).FirstOrDefault() is { } near)
            {
                message += " " + Loc.Current.Format("code.lint.didYouMean", ("name", near));
                fix = new QuickFix("code.fix.rename", near, [new TextEdit(doc.Start(i), name.Length, near)]);
            }

            Add(DiagnosticSeverity.Warning, code, doc.Start(i), doc.End(i), message, fix);
        }

        private Dictionary<string, GmlFunction?> Named => field ??= Declared.Where(f => f.Name >= 0).GroupBy(f => S(f.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Skip(1).Any() ? null : g.First(), StringComparer.Ordinal); // declared twice: which one runs is not known here

        private readonly Dictionary<string, GmlFunction?> _callable = new(StringComparer.Ordinal);

        // RequiredArguments reads the whole body: once per function, not once per call.
        private readonly Dictionary<GmlFunction, int?> _required = [];

        private GmlFunction? FunctionNamed(string name)
        {
            if (_callable.TryGetValue(name, out var known)) return known;
            var found = Named.GetValueOrDefault(name);
            // Set again elsewhere (name = something else): the call may not reach this function.
            if (found is not null && doc.Named(name).Count(t => !Is(t - 1, ".") && IsWrite(t)) != (Is(found.Name + 1, "=") ? 1 : 0)) found = null;
            return _callable[name] = found;
        }

        // A call of a function of this code or of the game: constructors need new, and required parameters need arguments.
        private void OwnCall(int i, string name)
        {
            if (IsKeyword(i - 1, "new") || Is(i - 1, ":")) return;
            bool constructor;
            int? required;
            if (FunctionNamed(name) is { } function)
            {
                constructor = function.Constructor;
                required = constructor ? null : _required.TryGetValue(function, out var counted) ? counted : _required[function] = RequiredArguments(doc, function);
            }
            else if (_declared.Contains(name) || _assigned.Contains(name)) return;
            else
            {
                constructor = context.IsOwnConstructor(name);
                required = constructor ? null : context.OwnRequired(name);
            }

            if (constructor)
            {
                Add(DiagnosticSeverity.Warning, MissingNew, doc.Start(i), doc.End(i), Loc.Current.Format("code.lint.missingNew", ("name", name)), new QuickFix("code.fix.addNew", "", [new TextEdit(doc.Start(i), 0, "new ")]));
                return;
            }

            if (required is not { } least || Arguments(i + 1) is not { } count || count >= least) return;
            var close = doc.PairOf(i + 1);
            Add(DiagnosticSeverity.Warning, ArgumentCount, doc.Start(i), doc.End(close), Loc.Current.Format("code.lint.argumentCount", ("name", name), ("expected", Loc.Current.Format("code.lint.atLeast", ("min", least))), ("count", count)));
        }
    }
}
