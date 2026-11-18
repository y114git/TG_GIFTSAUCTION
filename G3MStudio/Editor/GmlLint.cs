using G3MLib.Analyzer.Compiler;

namespace G3MStudio.Editor;

/// <summary>One finding of the code check: the diagnostic for the Problems panel and the squiggle, and the fix it offers (or null).</summary>
public sealed record LintIssue(GmlDiagnostic Diagnostic, QuickFix? Fix = null);

/// <summary>
/// What the code check may ask about names outside the text. Every lookup must be safe to call from a worker thread.
/// </summary>
/// <param name="IsKnown">Whether a name exists outside the text: a built-in name, a resource, a variable or function the game already uses, an extension function.</param>
/// <param name="ArgumentRange">The fewest and most arguments a built-in function takes, or null when that is not known for sure.</param>
/// <param name="IsReadOnly">Whether a built-in variable or constant can not be assigned.</param>
/// <param name="IsDeprecated">Whether a built-in function is deprecated.</param>
/// <param name="Names">The names to suggest for a misspelt one.</param>
/// <param name="IsBuiltinFunction">Whether a name is a built-in function (not one of the game's own), for the shadowing hint.</param>
/// <param name="IsBuiltinVariable">Whether a name is a built-in instance or global variable, for the shadowing hint.</param>
/// <param name="OwnRequired">The fewest arguments a function of the game (declared in another script) needs, or null when that is not known for sure.</param>
/// <param name="IsOwnConstructor">Whether a function of the game (declared in another script) is a constructor.</param>
/// <param name="Escapes">Whether strings take backslash escapes (GameMaker Studio 2 and later).</param>
/// <param name="InstanceCode">Whether the code runs in an instance (an object event, room or creation code, a timeline): its variables are often set by another event first.</param>
public sealed record GmlLintContext(
    Func<string, bool> IsKnown,
    Func<string, (int Min, int Max)?> ArgumentRange,
    Func<string, bool> IsReadOnly,
    Func<string, bool> IsDeprecated,
    IReadOnlyCollection<string> Names,
    Func<string, bool> IsBuiltinFunction,
    Func<string, bool> IsBuiltinVariable,
    Func<string, int?> OwnRequired,
    Func<string, bool> IsOwnConstructor,
    bool Escapes = true,
    bool InstanceCode = false);

/// <summary>
/// Checks a GML text beyond what the parser reports: unknown names, argument counts of built-in functions, read-only assignments, unused locals,
/// unreachable code, deprecated functions, = in conditions, unknown string escapes, empty bodies, repeated case values, and (GmlLint.Flow.cs) missing return values,
/// reads before the first assignment, unused parameters, shadowed built-in names, constructor calls without new and string to number comparisons.
/// Reads tokens only and runs on a worker. It reports only what it is sure of: a name it can not judge is left alone.
/// Run it on text the parser accepts; its bracket pairs are not trusted otherwise.
/// </summary>
public static partial class GmlLint
{
    public const string UnknownFunction = "G3M101";
    public const string ArgumentCount = "G3M102";
    public const string ReadOnly = "G3M103";
    public const string UnknownVariable = "G3M104";
    public const string UnusedLocal = "G3M105";
    public const string Unreachable = "G3M106";
    public const string Deprecated = "G3M107";
    public const string AssignInCondition = "G3M108";
    public const string UnknownEscape = "G3M109";
    public const string EmptyBody = "G3M110";
    public const string DuplicateCase = "G3M111";
    public const string MissingReturn = "G3M112";
    public const string ReadBeforeAssign = "G3M113";
    public const string UnusedParameter = "G3M114";
    public const string Shadowing = "G3M115";
    public const string MissingNew = "G3M116";
    public const string StringNumberCompare = "G3M117";

    // Names that are always there, whatever the lists of the game say.
    private static readonly HashSet<string> Always = new(StringComparer.Ordinal)
    {
        "true", "false", "undefined", "noone", "all", "global", "self", "other", "pi", "infinity", "NaN", "constructor", "argument", "argument_count",
    };

    private static readonly HashSet<string> AssignOps = new(StringComparer.Ordinal) { "=", "+=", "-=", "*=", "/=", "%=", "|=", "&=", "^=", "<<=", ">>=", "??=", "++", "--" };

    // Functions that take format arguments since GameMaker 2023, which the lists do not show yet.
    private static readonly HashSet<string> FormatFunctions = new(StringComparer.Ordinal) { "string", "show_debug_message", "string_ext", "show_debug_message_ext" };

    // Most unknown names checked for a spelling suggestion per run, so a text full of them stays fast.
    private const int MostSuggestions = 30;

    public static IReadOnlyList<LintIssue> Analyze(GmlDoc doc, GmlLintContext context, CancellationToken cancellationToken = default)
    {
        // Every check looks at neighbouring tokens, so comments are left out; the tokens keep their spans, so offsets still point into the text.
        if (doc.Tokens.Any(t => t.Kind == GmlTokenKind.Comment)) doc = GmlDoc.FromTokens(doc.Text, [.. doc.Tokens.Where(t => t.Kind != GmlTokenKind.Comment)]);
        var run = new Run(doc, context, cancellationToken);
        run.All();
        return run.Issues;
    }

    /// <summary>The highest argument count accepted when the reference and the compiler list are both considered. Used by the workspace context.</summary>
    public static (int Min, int Max)? Combine(string name, (int Min, int Max)? reference, (int Min, int Max)? compiler)
    {
        if (reference is not { } r) return null;
        // 0 to int.MaxValue in the compiler list means "any count": it has no opinion.
        var (min, max) = compiler is { } c && !(c.Min == 0 && c.Max == int.MaxValue) ? (Math.Min(r.Min, c.Min), Math.Max(r.Max, c.Max)) : r;
        return FormatFunctions.Contains(name) ? (min, int.MaxValue) : (min, max);
    }

    private sealed partial class Run(GmlDoc doc, GmlLintContext context, CancellationToken cancellationToken)
    {
        public readonly List<LintIssue> Issues = [];
        private readonly IReadOnlyList<GmlToken> _tokens = doc.Tokens;
        private readonly HashSet<string> _declared = new(StringComparer.Ordinal);
        private readonly HashSet<string> _assigned = new(StringComparer.Ordinal);
        private readonly HashSet<int> _skip = [];
        private readonly HashSet<string> _locals = new(StringComparer.Ordinal);
        private readonly HashSet<int> _localDeclarations = [];
        private int[]? _lineStarts;
        private int _suggestions;
        private HashSet<string>? _templateWords;

        public void All()
        {
            Declarations();
            DynamicNames();
            for (var i = 0; i < _tokens.Count; i++)
            {
                if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                var kind = _tokens[i].Kind;
                if (kind == GmlTokenKind.Identifier) Name(i);
                else if (kind == GmlTokenKind.Keyword) Keyword(i);
                else if (kind == GmlTokenKind.String && context.Escapes) Escapes(i);
            }

            Unused();
            Flow();
        }

        private string S(int i) => doc.Slice(i);

        private bool Is(int i, string text) => i >= 0 && i < _tokens.Count && _tokens[i].Kind != GmlTokenKind.String && _tokens[i].Kind != GmlTokenKind.TemplateString && S(i) == text;

        private bool IsKeyword(int i, params string[] words) => i >= 0 && i < _tokens.Count && _tokens[i].Kind == GmlTokenKind.Keyword && words.Contains(S(i));

        private int LineEnd(int offset)
        {
            var end = doc.Text.IndexOf('\n', offset);
            return end < 0 ? doc.Text.Length : end;
        }

        private void Add(DiagnosticSeverity severity, string code, int start, int end, string message, QuickFix? fix = null)
        {
            var (startLine, startColumn) = Position(start);
            var (endLine, endColumn) = Position(end);
            Issues.Add(new LintIssue(new GmlDiagnostic(severity, code, message, new SourceSpan(start, end, startLine, startColumn, endLine, endColumn), null), fix));
        }

        private (int Line, int Column) Position(int offset)
        {
            if (_lineStarts is null)
            {
                var starts = new List<int> { 0 };
                for (var k = 0; k < doc.Text.Length; k++)
                    if (doc.Text[k] == '\n') starts.Add(k + 1);
                _lineStarts = [.. starts];
            }

            var index = Array.BinarySearch(_lineStarts, offset);
            if (index < 0) index = ~index - 1;
            return (index + 1, offset - _lineStarts[index] + 1);
        }
    }
}
