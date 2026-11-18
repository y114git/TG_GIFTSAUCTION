using G3MLib.Analyzer.Compiler;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public static partial class GmlLint
{
    private sealed partial class Run
    {
        private void Keyword(int i)
        {
            switch (S(i))
            {
                case "return" or "exit" or "break" or "continue" or "throw":
                    AfterJump(i);
                    break;
                case "if" or "while" or "until":
                    Condition(i);
                    if (S(i) != "until") EmptyBodyAfter(i);
                    break;
                case "for" or "with" or "repeat":
                    EmptyBodyAfter(i);
                    break;
                case "switch":
                    Cases(i);
                    break;
            }
        }

        // Code right after return, exit, break, continue or throw in the same block never runs.
        private void AfterJump(int i)
        {
            // Only a jump that is a statement of its own block counts: after if (...) or else it is the body of that statement.
            if (!(i == 0 || Is(i - 1, ";") || Is(i - 1, "{") || Is(i - 1, "}") || Is(i - 1, ":"))) return;
            int end;
            if (S(i) is "exit" or "break" or "continue") end = Is(i + 1, ";") ? i + 1 : i;
            else if (EndOfStatement(i) is { } found) end = found;
            else return;
            var next = end + 1;
            if (next >= _tokens.Count || _skip.Contains(next) || Is(next, "}") || IsKeyword(next, "case", "default", "function", "enum") || _tokens[next].Kind == GmlTokenKind.Preprocessor) return;
            var start = doc.Start(next);
            Add(DiagnosticSeverity.Warning, Unreachable, start, Math.Max(LineEnd(start), doc.End(next)), Loc.Current["code.lint.unreachable"]);
        }

        // The ; that ends the statement starting at the token, or null when the block closes first (no ; to be sure of).
        private int? EndOfStatement(int i)
        {
            for (var j = i + 1; j < _tokens.Count; j++)
            {
                if (IsOpener(j) && doc.PairOf(j) is var pair and >= 0) j = pair;
                else if (Is(j, ";")) return j;
                else if (Is(j, "}") || _tokens[j].Kind == GmlTokenKind.Keyword && S(j) is not ("new" or "function" or "self" or "other" or "global" or "noone" or "all" or "true" or "false" or "undefined" or "not" or "and" or "or" or "xor" or "div" or "mod")) return null;
            }

            return null;
        }

        // if (a = b): GameMaker compares there, but == says so; offered as a hint with a fix.
        private void Condition(int keyword)
        {
            if (!Is(keyword + 1, "(") || doc.PairOf(keyword + 1) is not (var close and >= 0)) return;
            for (var j = keyword + 2; j < close; j++)
            {
                if (IsOpener(j) && doc.PairOf(j) is var pair and >= 0) j = pair;
                else if (_tokens[j].Kind == GmlTokenKind.Operator && S(j) == "=")
                    Add(DiagnosticSeverity.Info, AssignInCondition, doc.Start(j), doc.End(j), Loc.Current["code.lint.assignInCondition"], new QuickFix("code.fix.useEquals", "", [new TextEdit(doc.Start(j), 1, "==")]));
            }
        }

        // if (...); with the ; right after the condition: the body is empty and the next statement always runs.
        private void EmptyBodyAfter(int keyword)
        {
            if (S(keyword) == "while" && Is(keyword - 1, "}")) return;
            if (!Is(keyword + 1, "(") || doc.PairOf(keyword + 1) is not (var close and >= 0) || !Is(close + 1, ";")) return;
            var semicolon = close + 1;
            Add(DiagnosticSeverity.Warning, EmptyBody, doc.Start(semicolon), doc.End(semicolon), Loc.Current["code.lint.emptyBody"], new QuickFix("code.fix.removeSemicolon", "", [new TextEdit(doc.Start(semicolon), 1, "")]));
        }

        // A case value that an earlier case of the same switch already has can never be reached.
        private void Cases(int keyword)
        {
            if (!Is(keyword + 1, "(") || doc.PairOf(keyword + 1) is not (var close and >= 0) || !Is(close + 1, "{") || doc.PairOf(close + 1) is not (var end and >= 0)) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var j = close + 2; j < end; j++)
            {
                if (IsOpener(j) && doc.PairOf(j) is var pair and >= 0) { j = pair; continue; }
                if (!IsKeyword(j, "case")) continue;
                // Only a single literal or name (with an optional minus) is compared; an expression may differ at run time.
                var first = Is(j + 1, "-") ? j + 2 : j + 1;
                if (first >= end || !Is(first + 1, ":") || _tokens[first].Kind is not (GmlTokenKind.Number or GmlTokenKind.String or GmlTokenKind.Identifier)) continue;
                var value = (first == j + 2 ? "-" : "") + S(first);
                if (!seen.Add(value)) Add(DiagnosticSeverity.Warning, DuplicateCase, doc.Start(j + 1), doc.End(first), Loc.Current["code.lint.duplicateCase"]);
            }
        }

        // A backslash before a character that is no escape: GameMaker drops or keeps it, either way not what was meant ("C:\new" holds a line break).
        private void Escapes(int i)
        {
            var start = doc.Start(i);
            var text = doc.Text;
            if (text[start] != '"') return;
            var end = doc.End(i) - 1;
            for (var k = start + 1; k < end; k++)
            {
                if (text[k] != '\\') continue;
                var c = text[k + 1];
                if (c is not ('n' or 'r' or 't' or 'b' or 'f' or 'v' or 'a' or 'x' or 'u' or '\\' or '"' or '\'' or '\n' or '\r' or '/' or (>= '0' and <= '7')))
                    Add(DiagnosticSeverity.Warning, UnknownEscape, k, k + 2, Loc.Current.Format("code.lint.escape", ("escape", text.Substring(k, 2))));
                k++;
            }
        }
    }
}
