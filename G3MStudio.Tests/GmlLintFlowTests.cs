using G3MLib.Analyzer.Compiler;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>The checks of GmlLint.Flow.cs: each one fires on the case it is for and stays quiet on the look-alikes that are fine.</summary>
public sealed class GmlLintFlowTests
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "show_debug_message", "array_length", "draw_text", "is_undefined", "x", "y", "image_index", "speed", "instance_destroy", "irandom", "string", "real",
        "variable_instance_exists", "variable_instance_set", "id", "obj_enemy", "show_error", "scr_spawn", "Shape", "point_distance",
    };

    private static readonly GmlLintContext Context = new(
        Known.Contains,
        _ => null,
        _ => false,
        _ => false,
        Known,
        IsBuiltinFunction: name => name is "draw_text" or "string" or "real" or "point_distance",
        IsBuiltinVariable: name => name is "x" or "y" or "image_index" or "speed",
        OwnRequired: name => name == "scr_spawn" ? 2 : null,
        IsOwnConstructor: name => name == "Shape");

    private static IReadOnlyList<LintIssue> Lint(string text) => GmlLint.Analyze(GmlDoc.Parse(text), Context);

    private static IEnumerable<LintIssue> Of(string text, string code) => Lint(text).Where(i => i.Diagnostic.Code == code);

    private static string Marked(string text, LintIssue issue) => text[issue.Diagnostic.Span.StartOffset..issue.Diagnostic.Span.EndOffset];

    [Theory]
    [InlineData("function f(a) {\n    if (a) return 1;\n}")]
    [InlineData("function f(a) {\n    if (a) { return 1; } else { show_debug_message(a); }\n}")]
    [InlineData("function f(a) {\n    for (var i = 0; i < a; i++) if (i == 3) return i;\n}")]
    [InlineData("f = function(a) {\n    if (a > 1) return a;\n    a += 1;\n}")]
    public void AFunctionThatCanEndWithoutAValueIsAHint(string text)
    {
        var issue = Assert.Single(Of(text, GmlLint.MissingReturn));
        Assert.Equal(DiagnosticSeverity.Info, issue.Diagnostic.Severity);
        Assert.Equal("f", Marked(text, issue));
    }

    [Theory]
    [InlineData("function f(a) {\n    if (a) return 1;\n    return 2;\n}")]
    [InlineData("function f(a) {\n    if (a) return 1; else return 2;\n}")]
    [InlineData("function f(a) {\n    if (a) { return 1; }\n    else if (a > 2) { return 2; }\n    else { throw \"no\"; }\n}")]
    [InlineData("function f(a) {\n    while (true) { if (a) return 1; }\n}")]
    [InlineData("function f(a) {\n    for (;;) { if (a) return 1; }\n}")]
    [InlineData("function f(a) {\n    switch (a) { case 1: return 1; }\n}")]
    [InlineData("function f(a) {\n    try { return a; } catch (e) { show_debug_message(e); }\n}")]
    [InlineData("function f(a) {\n    if (a) return 1;\n    show_error(\"bad\", true);\n}")]
    [InlineData("function f(a) {\n    if (a) return;\n    show_debug_message(a);\n}")]
    [InlineData("function f(a) {\n    if (a) exit;\n    return 2;\n}")]
    [InlineData("function V(a) constructor {\n    value = a;\n}")]
    [InlineData("function f(a) {\n    var g = function() { return 1; };\n    g();\n}")]
    [InlineData("function f(a) {\n    if (a) return 1\n    return 2\n}")]
    [InlineData("function f(a) {\n    do { if (a) return 1; } until (false);\n}")]
    [InlineData("if (x) return 1;\ny = 2;")]
    public void AFunctionThatAlwaysReturnsOrCanNotBeJudgedIsLeftAlone(string text) => Assert.Empty(Of(text, GmlLint.MissingReturn));

    [Fact]
    public void ANameReadBeforeThisCodeFirstSetsItIsAHint()
    {
        const string text = "show_debug_message(counter);\ncounter = 0;";
        var issue = Assert.Single(Of(text, GmlLint.ReadBeforeAssign));
        Assert.Equal("counter", Marked(text, issue));
        Assert.Equal(DiagnosticSeverity.Info, issue.Diagnostic.Severity);
        Assert.Single(Of("counter += 1;\ncounter = 0;", GmlLint.ReadBeforeAssign)); // += reads first
    }

    [Theory]
    [InlineData("counter = 0;\nshow_debug_message(counter);")]
    [InlineData("x = 1;\nshow_debug_message(x);\nx = 2;")] // known to the game
    [InlineData("var c;\nshow_debug_message(c);\nc = 1;")] // a local
    [InlineData("repeat (3) { if (counter > 0) show_debug_message(counter); counter = 1; }")]
    [InlineData("function f() { return counter; }\ncounter = 1;")]
    [InlineData("with (obj_enemy) { show_debug_message(counter); }\ncounter = 1;")]
    [InlineData("if (variable_instance_exists(id, \"counter\")) show_debug_message(counter);\ncounter = 1;")]
    [InlineData("show_debug_message(counter);")] // set nowhere: the unknown-name warning says it
    public void NamesSetFirstOrInCodeWhoseOrderIsUnknownAreLeftAlone(string text) => Assert.Empty(Of(text, GmlLint.ReadBeforeAssign));

    [Fact]
    public void AParameterThatIsNeverUsedIsAHint()
    {
        const string text = "function f(a, b) {\n    return a;\n}";
        var issue = Assert.Single(Of(text, GmlLint.UnusedParameter));
        Assert.Equal("b", Marked(text, issue));
        Assert.Equal(DiagnosticSeverity.Info, issue.Diagnostic.Severity);
    }

    [Theory]
    [InlineData("function f(a, _b) { return a; }")]
    [InlineData("function f(a, b) { return argument[1]; }")]
    [InlineData("function f(a, b) { return argument_count; }")]
    [InlineData("function f(a, b = a) { return b; }")]
    [InlineData("function f(a) { show_debug_message($\"{a}\"); }")]
    [InlineData("function V(a) constructor { self.a = a; }")]
    public void UsedOrDeliberatelyIgnoredParametersAreLeftAlone(string text) => Assert.Empty(Of(text, GmlLint.UnusedParameter));

    [Fact]
    public void ALocalNamedLikeABuiltInIsAHint()
    {
        var function = Assert.Single(Of("var draw_text = 1;\nshow_debug_message(draw_text);", GmlLint.Shadowing));
        Assert.Contains("draw_text", function.Diagnostic.Message, StringComparison.Ordinal);
        Assert.Single(Of("var speed = 2;\nshow_debug_message(speed);", GmlLint.Shadowing));
        Assert.Empty(Of("var spd = 2;\nshow_debug_message(spd);", GmlLint.Shadowing));
        Assert.Empty(Of("function draw_text(a) {}\nvar t = 1;", GmlLint.Shadowing));
        Assert.Empty(Of("function f(x) { return x; }", GmlLint.Shadowing)); // parameters are not judged
    }

    [Fact]
    public void AConstructorCalledWithoutNewIsAWarningWithAFix()
    {
        const string text = "function Point(a) constructor { value = a; }\nvar p = Point(1);\nshow_debug_message(p);";
        var issue = Assert.Single(Of(text, GmlLint.MissingNew));
        Assert.Equal(DiagnosticSeverity.Warning, issue.Diagnostic.Severity);
        Assert.Equal("Point", Marked(text, issue));
        Assert.Contains("new Point(1)", GmlSymbols.Apply(text, issue.Fix!.Edits), StringComparison.Ordinal);
        Assert.Single(Of("var s = Shape();\nshow_debug_message(s);", GmlLint.MissingNew)); // a constructor of another script
    }

    [Theory]
    [InlineData("function Point(a) constructor { value = a; }\nvar p = new Point(1);\nshow_debug_message(p);")]
    [InlineData("function Base() constructor {}\nfunction Child() : Base() constructor {}")]
    [InlineData("var s = new Shape();\nshow_debug_message(s);")]
    [InlineData("function Point(a) { return a; }\nPoint(1);")]
    public void ProperConstructorCallsAndPlainFunctionsAreLeftAlone(string text) => Assert.Empty(Of(text, GmlLint.MissingNew));

    [Fact]
    public void AnOwnFunctionCalledWithTooFewRequiredArgumentsIsAWarning()
    {
        const string text = "function add(a, b, c = 0) { return a + b + c; }\nadd(1);";
        var issue = Assert.Single(Of(text, GmlLint.ArgumentCount));
        Assert.Equal("add(1)", Marked(text, issue));
        Assert.Single(Of("scr_spawn(1);", GmlLint.ArgumentCount)); // a script of the game that needs two
    }

    [Theory]
    [InlineData("function add(a, b, c = 0) { return a + b + c; }\nadd(1, 2);")]
    [InlineData("function add(a, b) { if (is_undefined(b)) b = 0; return a + b; }\nadd(1);")]
    [InlineData("function add(a, b) { b ??= 0; return a + b; }\nadd(1);")]
    [InlineData("function add(a, b) { if (b == undefined) return a; return a + b; }\nadd(1);")]
    [InlineData("function add(a, b) { return argument_count > 1 ? a + b : a; }\nadd(1);")]
    [InlineData("function add(a, b) { return a + b; }\nadd = function(n) { return n; };\nadd(1);")]
    [InlineData("scr_spawn(1, 2);")]
    [InlineData("other.scr_spawn(1);")]
    public void EnoughArgumentsOrOptionalHandlingAreLeftAlone(string text) => Assert.Empty(Of(text, GmlLint.ArgumentCount));

    [Theory]
    [InlineData("if (\"5\" == 5) x = 1;")]
    [InlineData("var s = \"a\";\nif (s == 1) x = 1;")]
    [InlineData("var n = 3;\nvar t = \"3\";\nshow_debug_message(n != t);")]
    public void AStringComparedWithANumberIsAHint(string text) => Assert.Single(Of(text, GmlLint.StringNumberCompare));

    [Theory]
    [InlineData("if (\"5\" == string(5)) x = 1;")]
    [InlineData("var s = \"a\";\ns = 1;\nif (s == 1) x = 1;")]
    [InlineData("var s = \"a\";\nif (s + \"b\" == 1) x = 1;")]
    [InlineData("if (x == 5) x = 1;")]
    [InlineData("if (real(\"5\") == 5) x = 1;")]
    public void ComparisonsOfUnknownOrMatchingTypesAreLeftAlone(string text) => Assert.Empty(Of(text, GmlLint.StringNumberCompare));

    [Theory]
    [InlineData("variable_instance_set(id, \"stamina\", 5);\nshow_debug_message(stamina);")]
    [InlineData("var s = { power: 1, get: function() { return power; } };\nshow_debug_message(s);")]
    public void NamesMadeByVariableSettersAndStructKeysAreNotUnknown(string text) => Assert.DoesNotContain(Lint(text), i => i.Diagnostic.Code == GmlLint.UnknownVariable);

    [Fact]
    public void ProjectSymbolsFindMacrosEnumsGlobalvarsAndDynamicNames()
    {
        var found = GmlProjectSymbols.Extract("gml_Script_cfg", "#macro LIMIT 7\nenum Mode { a, b }\nglobalvar best, worst;\nvariable_instance_set(id, \"stamina\", 1);\nvariable_global_set(\"level\", 2);\nvariable_instance_set(id, name, 1);\n");
        Assert.Equal(["LIMIT macro", "Mode enum", "best global", "worst global", "stamina variable", "level global"], found.Select(s => $"{s.Name} {s.Kind}"));
        Assert.Equal("#macro LIMIT 7", found[0].Preview);
        Assert.Empty(GmlProjectSymbols.Extract("gml_Script_plain", "x = 1;\n"));
    }

    [Fact]
    public void RequiredArgumentsComeFromTheDeclaration()
    {
        var doc = GmlDoc.Parse("function f(a, b, c = 1) { return a + b + c; }\nfunction g(a, b) { if (is_undefined(b)) b = 1; return a + b; }\nfunction h() { return argument0; }");
        var functions = GmlLint.Functions(doc);
        Assert.Equal([2, 1, null], functions.Select(f => GmlLint.RequiredArguments(doc, f)));
    }
}
