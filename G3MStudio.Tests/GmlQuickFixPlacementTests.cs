using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlQuickFixPlacementTests
{
    private static readonly string[] NoNames = [];

    private static IReadOnlyList<QuickFix> Fixes(string text, string at, bool problem = false)
    {
        var doc = GmlDoc.Parse(text);
        return GmlQuickFixes.For(doc, text.IndexOf(at, StringComparison.Ordinal) + 1, _ => false, NoNames, problem);
    }

    private static string? Declared(string text, string at = "mystery")
    {
        var fix = Fixes(text, at).FirstOrDefault(f => f.Key == "code.fix.declareLocal");
        return fix is null ? null : GmlSymbols.Apply(text, fix.Edits);
    }

    [Fact]
    public void TheDeclarationGetsTheIndentationOfTheStatementItGoesAbove()
    {
        Assert.Equal("if (a) {\n\t\tvar mystery = 0;\n\t\tfoo = mystery;\n}", Declared("if (a) {\n\t\tfoo = mystery;\n}"));
        Assert.Equal("function f() {\n   var mystery = 0;\n   x = mystery + 1;\n}", Declared("function f() {\n   x = mystery + 1;\n}"));
        Assert.Equal("x = 1;\nvar mystery = 0;\ny = mystery;", Declared("x = 1;\ny = mystery;"));
    }

    [Fact]
    public void AUseInsideAStatementThatSpansLinesPutsTheDeclarationAboveTheWholeStatement()
    {
        Assert.Equal("{\n    var mystery = 0;\n    foo(\n        a,\n        mystery\n    );\n}", Declared("{\n    foo(\n        a,\n        mystery\n    );\n}"));
        Assert.Equal("  var mystery = 0;\n  for (var i = 0; i < mystery; i++) {\n    a();\n  }", Declared("  for (var i = 0; i < mystery; i++) {\n    a();\n  }"));
        Assert.Equal("a = 1;\nvar mystery = 0;\nx = [\n  1,\n  mystery\n];", Declared("a = 1;\nx = [\n  1,\n  mystery\n];"));
    }

    [Fact]
    public void AStatementThatDoesNotStartItsLineGetsTheDeclarationInFrontOnTheSameLine()
    {
        Assert.Equal("a = 1; var mystery = 0; b = mystery;", Declared("a = 1; b = mystery;"));
    }

    [Fact]
    public void TheNewLineEndsLikeTheLinesAroundIt()
    {
        Assert.Equal("{\r\n  var mystery = 0;\r\n  x = mystery;\r\n}", Declared("{\r\n  x = mystery;\r\n}"));
        Assert.Equal("a;\nvar mystery = 0;\r\nb = mystery;\r\n", Declared("a;\nb = mystery;\r\n")); // the new line ends like the line it sits above
    }

    [Fact]
    public void NoDeclarationIsOfferedWhereItWouldChangeTheMeaningOrBreakTheCode()
    {
        Assert.Null(Declared("s = { a: mystery };")); // inside a struct literal
        Assert.Null(Declared("function f(a = mystery) { }")); // in a parameter list
        Assert.Null(Declared("if (a) { b(); } else c = mystery;")); // a body that belongs to else
        Assert.Null(Declared("switch (v) {\n  case 1: x = mystery;\n}")); // after a case label
        Assert.Null(Declared("do { a(); } until (mystery);")); // part of the closing of a do
    }

    [Fact]
    public void TheOtherFixesStayAvailableWhereNoDeclarationIsOffered()
    {
        var keys = Fixes("s = { a: mystery };", "mystery").Select(f => f.Key).ToList();
        Assert.DoesNotContain("code.fix.declareLocal", keys);
        Assert.Contains("code.fix.declareGlobal", keys);
    }

    [Fact]
    public void CommentingALineKeepsItsIndentationAndLeavesEmptyAndCommentedLinesAlone()
    {
        const string code = "{\n\t\tx = mystery;\n}";
        var comment = Fixes(code, "mystery", problem: true).Single(f => f.Key == "code.fix.commentLine");
        Assert.Equal("{\n\t\t// x = mystery;\n}", GmlSymbols.Apply(code, comment.Edits));
        Assert.DoesNotContain(Fixes("{\n  // x = mystery;\n}", "mystery", problem: true), f => f.Key == "code.fix.commentLine");
        Assert.DoesNotContain(Fixes("/* a\n  x = mystery;\n*/", "mystery", problem: true), f => f.Key == "code.fix.commentLine");
    }
}
