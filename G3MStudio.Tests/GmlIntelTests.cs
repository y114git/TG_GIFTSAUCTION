using G3MStudio.Core.ModPack;
using System.Diagnostics;
using G3MLib.Analyzer.Compiler;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlIntelTests
{
    [Fact]
    public void BracketsPairAcrossStringsAndCommentsAndMatchBeforeOrAfterTheCaret()
    {
        const string text = "f(a[1], \")\") // )\n{ x }";
        var doc = GmlDoc.Parse(text);
        Assert.Equal((1, 11), GmlStructure.MatchingBrackets(doc, 1));
        Assert.Equal((11, 1), GmlStructure.MatchingBrackets(doc, 12)); // caret right behind the closing bracket
        Assert.Equal((3, 5), GmlStructure.MatchingBrackets(doc, 3));
        Assert.Null(GmlStructure.MatchingBrackets(doc, 0));
    }

    [Fact]
    public void OutlineListsFunctionsEnumsRegionsAndMacrosAndTheBreadcrumbNests()
    {
        const string text = "#macro M 3\n#region Setup\nenum E { a, b }\nfunction f(a, b = 2) {\n    var x = 1;\n}\nvar g = function() { return 1; };\n#endregion\n";
        var doc = GmlDoc.Parse(text);
        var outline = GmlStructure.Outline(doc);
        Assert.Equal(["M", "Setup", "E", "f", "g"], outline.Select(i => i.Name));
        var inside = GmlStructure.Around(outline, text.IndexOf("var x", StringComparison.Ordinal));
        Assert.Equal(["Setup", "f"], inside.Select(i => i.Name));
        Assert.Empty(GmlStructure.Around(outline, 0));
    }

    [Fact]
    public void FoldingsCoverBracesRegionsAndBlockCommentsButNotSingleLines()
    {
        const string text = "#region R\nif (a) {\n  b();\n}\n/* one\ntwo */\nif (c) { d(); }\n#endregion\n";
        var folds = GmlStructure.Foldings(GmlDoc.Parse(text));
        Assert.Equal(["#region R", "{...}", "/*...*/"], folds.Select(f => f.Name));
    }

    [Fact]
    public void ARenameChangesOnlyTheLocalNotMembersStringsOrComments()
    {
        const string text = "var hp = 1; hp += other.hp; s = \"hp\"; // hp\nreturn hp;";
        var doc = GmlDoc.Parse(text);
        var plan = GmlSymbols.PlanRename(doc, 5, "life", _ => false);
        Assert.NotNull(plan);
        Assert.Empty(plan.Warnings);
        Assert.Equal("var life = 1; life += other.hp; s = \"hp\"; // hp\nreturn life;", GmlSymbols.Apply(text, plan.Edits));
    }

    [Fact]
    public void ARenameWarnsAboutCollisionsAndBadNamesButStillGivesTheEdits()
    {
        const string text = "var a = 1; var b = 2; a = b; draw_text(a);";
        var doc = GmlDoc.Parse(text);
        var clash = GmlSymbols.PlanRename(doc, 5, "b", _ => false);
        Assert.Contains("code.rename.collision", clash!.Warnings);
        Assert.NotEmpty(clash.Edits);
        Assert.Contains("code.rename.collision", GmlSymbols.PlanRename(doc, 5, "draw_x", name => name == "draw_x")!.Warnings);
        Assert.Contains("code.rename.invalid", GmlSymbols.PlanRename(doc, 5, "1x", _ => false)!.Warnings);
        Assert.Contains("code.rename.notLocal", GmlSymbols.PlanRename(doc, text.IndexOf("draw_text", StringComparison.Ordinal), "q", _ => false)!.Warnings);
        Assert.Null(GmlSymbols.PlanRename(doc, 8, "q", _ => false)); // on the number
    }

    [Fact]
    public void AGlobalRenamesBothItsDottedAndItsGlobalvarForm()
    {
        const string text = "global.score = 1; x = global.score + 1;";
        var plan = GmlSymbols.PlanRename(GmlDoc.Parse(text), text.IndexOf("score", StringComparison.Ordinal), "points", _ => false);
        Assert.Equal("global.points = 1; x = global.points + 1;", GmlSymbols.Apply(text, plan!.Edits));
    }

    [Fact]
    public void ADeclarationIsFoundForLocalsAndParameters()
    {
        var doc = GmlDoc.Parse("function f(p, q) { var x = p; }");
        Assert.Equal("p", doc.Slice(GmlSymbols.DeclarationOf(doc, "p")));
        Assert.Equal(doc.Text.IndexOf('x', 18), doc.Start(GmlSymbols.DeclarationOf(doc, "x")));
        Assert.Equal(-1, GmlSymbols.DeclarationOf(doc, "nope"));
    }

    [Fact]
    public void CommentTogglingAddsAndRemovesMarksOnWholeLines()
    {
        const string text = "a();\n    b();\n\nc();";
        var on = GmlLineEdits.ToggleLineComment(text, 0, text.Length);
        var commented = text.Remove(on.Offset, on.Length).Insert(on.Offset, on.Text);
        Assert.Equal("// a();\n//     b();\n\n// c();", commented);
        var off = GmlLineEdits.ToggleLineComment(commented, 0, commented.Length);
        Assert.Equal(text, commented.Remove(off.Offset, off.Length).Insert(off.Offset, off.Text));
    }

    [Fact]
    public void BlockCommentWrapsASelectionAndUnwrapsIt()
    {
        var wrap = GmlLineEdits.ToggleBlockComment("x = 1;", 0, 6);
        Assert.Equal("/*x = 1;*/", wrap.Text);
        var unwrap = GmlLineEdits.ToggleBlockComment("/*x = 1;*/", 0, 10);
        Assert.Equal("x = 1;", unwrap.Text);
    }

    [Fact]
    public void DuplicateAndMoveKeepTheSelectionOnTheMovedLines()
    {
        const string text = "one\ntwo\nthree";
        var copy = GmlLineEdits.DuplicateLines(text, 4, 4);
        Assert.Equal("one\ntwo\ntwo\nthree", text.Insert(copy.Offset, copy.Text));
        Assert.Equal(8, copy.SelectionStart);
        var up = GmlLineEdits.MoveLines(text, 4, 7, up: true)!;
        Assert.Equal("two\none\nthree", text.Remove(up.Offset, up.Length).Insert(up.Offset, up.Text));
        Assert.Equal(0, up.SelectionStart);
        var down = GmlLineEdits.MoveLines(text, 4, 4, up: false)!;
        Assert.Equal("one\nthree\ntwo", text.Remove(down.Offset, down.Length).Insert(down.Offset, down.Text));
        Assert.Null(GmlLineEdits.MoveLines(text, 0, 0, up: true));
        Assert.Null(GmlLineEdits.MoveLines(text, 9, 9, up: false));
    }

    [Fact]
    public void AutoCloseOnlyClosesWhereNothingFollowsAndStepsOverItsOwnCloser()
    {
        Assert.Equal(")", GmlLineEdits.ClosingFor("f", 1, '('));
        Assert.Equal("}", GmlLineEdits.ClosingFor("", 0, '{'));
        Assert.Null(GmlLineEdits.ClosingFor("fx", 1, '(')); // text follows: leave it alone
        Assert.Equal("\"", GmlLineEdits.ClosingFor("s = ", 4, '"'));
        Assert.Null(GmlLineEdits.ClosingFor("don", 3, '\'')); // an apostrophe after a letter
        Assert.True(GmlLineEdits.StepsOver("f()", 2, ')'));
        Assert.False(GmlLineEdits.StepsOver("f()", 1, ')'));
    }

    [Fact]
    public void ColourLiteralsAreReadInGameMakersByteOrder()
    {
        static (byte, byte, byte)? Colour(string text, GmlTokenKind kind) => GmlColors.Parse(text, kind);
        Assert.Equal(((byte)0x00, (byte)0x88, (byte)0xFF), Colour("$FF8800", GmlTokenKind.Number));
        Assert.Equal(((byte)0xFF, (byte)0x00, (byte)0xFF), Colour("0xFF00FF", GmlTokenKind.Number));
        Assert.Equal(((byte)0xFF, (byte)0x00, (byte)0x00), Colour("#FF0000", GmlTokenKind.Preprocessor));
        Assert.Equal(((byte)255, (byte)0, (byte)0), Colour("c_red", GmlTokenKind.Identifier));
        Assert.Null(Colour("$FF", GmlTokenKind.Number)); // a flag, not a colour
        Assert.Null(Colour("c_custom", GmlTokenKind.Identifier));
    }

    [Fact]
    public void TheCallAroundTheCaretIsFoundWithItsArgumentIndex()
    {
        var doc = GmlDoc.Parse("x = draw_text(10, max(a, b), \"s, t\", ");
        Assert.Equal(new CallContext("draw_text", 3), GmlSignatures.CallAt(doc, doc.Text.Length));
        Assert.Equal(new CallContext("max", 1), GmlSignatures.CallAt(doc, doc.Text.IndexOf("b)", StringComparison.Ordinal)));
        Assert.Equal(new CallContext("draw_text", 0), GmlSignatures.CallAt(doc, doc.Text.IndexOf('(', 0) + 1));
        Assert.Null(GmlSignatures.CallAt(GmlDoc.Parse("x = 1;"), 6));
        Assert.Null(GmlSignatures.CallAt(GmlDoc.Parse("f(1); if (a "), 12)); // after the call closed, an if is no call
    }

    [Fact]
    public void ASignatureShowsOptionalAndVariadicParametersAndTheActiveOne()
    {
        var fixedArgs = Signature.FromCounts("make_color_rgb", 3, 3);
        Assert.Equal("make_color_rgb(argument0, argument1, argument2)", fixedArgs.Label);
        var optional = Signature.FromCounts("show_message", 1, 2);
        Assert.Equal("show_message(argument0, [argument1])", optional.Label);
        var many = Signature.FromCounts("max", 1, -1);
        Assert.Equal("max(argument0, ...)", many.Label);
        Assert.Equal(0, many.ActiveParameter(7));
        Assert.Equal(-1, fixedArgs.ActiveParameter(3));
    }

    [Fact]
    public void ALineDiffMarksAddedAndRemovedLines()
    {
        var diff = LineDiff.Compute("a\nb\nc\nd", "a\nB\nc\nd\ne");
        Assert.Equal(["Same a", "Removed b", "Added B", "Same c", "Same d", "Added e"], diff.Select(d => d.Kind + " " + d.Text));
        Assert.Equal(2, diff[1].OldLine);
        Assert.Equal(2, diff[2].NewLine);
        Assert.All(LineDiff.Compute("x", "x"), d => Assert.Equal(DiffKind.Same, d.Kind));
    }

    [Fact]
    public void AnalysisOfAFiveThousandLineEntryIsFastAndLookupsAreInstant()
    {
        var text = string.Join('\n', Enumerable.Range(0, 5000).Select(i => $"var v{i} = scr_call{i % 40}(v{i / 2 + 1}, \"text\", {i}); // line {i}\nif (v{i} > 0) {{ draw_text(0, {i}, v{i}); }}"));
        var parse = Stopwatch.StartNew();
        var doc = GmlDoc.Parse(text);
        _ = GmlStructure.Foldings(doc);
        _ = GmlStructure.Outline(doc);
        parse.Stop();
        // The cost is paid once per typing pause on a background pass, not per key.
        Assert.True(parse.ElapsedMilliseconds < 2000, $"parse took {parse.ElapsedMilliseconds} ms");
        var lookups = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            var at = text.Length / 2 + i;
            _ = GmlStructure.MatchingBrackets(doc, at);
            _ = GmlSignatures.CallAt(doc, at);
            _ = doc.Named("v2500");
            _ = GmlNames.WordAt(text, at);
        }

        lookups.Stop();
        Assert.True(lookups.Elapsed.TotalMilliseconds / 100 < 50, $"a lookup took {lookups.Elapsed.TotalMilliseconds / 100} ms");
    }
}
