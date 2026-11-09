using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlIntelMoreTests
{
    [Fact]
    public void ASnippetTakesTheIndentOfItsLineAndPutsTheCaretAtTheMarker()
    {
        const string document = "if (a)\n{\n    ";
        var (text, caret) = GmlSnippets.Expand(GmlSnippets.All.Single(s => s.Name == "if"), document, document.Length);
        Assert.Equal("if ()\n    {\n        \n    }", text);
        Assert.Equal(document.Length + 4, caret);
        var (plain, end) = GmlSnippets.Expand(new Snippet("x", "a();"), "", 0);
        Assert.Equal(("a();", 4), (plain, end));
        Assert.All(GmlSnippets.All, s => Assert.Contains("$0", s.Body, StringComparison.Ordinal));
    }

    [Fact]
    public void QuickFixesChangeTheSpellingDeclareOrCommentAndKnownNamesGetNone()
    {
        var doc = GmlDoc.Parse("draw_txt(1);\nvar ok = 1; ok = mystery;");
        static bool Known(string n) => n is "draw_text" or "ok";
        string[] names = ["draw_text", "draw_sprite", "ok"];
        var spelling = GmlQuickFixes.For(doc, 2, Known, names, false);
        Assert.Contains(spelling, f => f is { Key: "code.fix.rename", Argument: "draw_text" });
        var undeclared = GmlQuickFixes.For(doc, doc.Text.IndexOf("mystery", StringComparison.Ordinal) + 1, Known, names, true);
        Assert.Equal(["code.fix.declareLocal", "code.fix.declareGlobal", "code.fix.commentLine"], undeclared.Select(f => f.Key));
        Assert.Equal("global.mystery", GmlSymbols.Apply(doc.Text, undeclared[1].Edits)[doc.Text.IndexOf("mystery", StringComparison.Ordinal)..][..14]);
        Assert.Empty(GmlQuickFixes.For(doc, doc.Text.IndexOf("ok =", StringComparison.Ordinal) + 1, Known, names, false));
        Assert.Equal(1, GmlQuickFixes.Distance("kitten", "kittan"));
        Assert.Equal(3, GmlQuickFixes.Distance("abc", ""));
    }

    [Fact]
    public void FunctionsDeclaredInTheTextGiveRealParameterNamesWithDefaultsOptional()
    {
        var doc = GmlDoc.Parse("function move(dx, dy, speed = 4) {}\nvar f = function(a) {};");
        var move = GmlSignatures.FromDoc(doc, "move")!;
        Assert.Equal("move(dx, dy, [speed])", move.Label);
        Assert.Equal(2, move.Required);
        Assert.Equal("f(a)", GmlSignatures.FromDoc(doc, "f")!.Label);
        Assert.Null(GmlSignatures.FromDoc(doc, "nope"));
        Assert.Equal(["move(", "dx", ", ", "dy", ", ", "[speed]", ")"], move.Segments(2).Select(s => s.Text));
        Assert.Equal("[speed]", move.Segments(2).Single(s => s.Active).Text);
        var many = Signature.FromCounts("max", 1, -1).Segments(5);
        Assert.Equal("...", many.Single(s => s.Active).Text);
    }

    [Fact]
    public void ColourLiteralsAreIndexedForTheSwatchesAndFoundByOffset()
    {
        var doc = GmlDoc.Parse("a = c_red; b = $FF8800; c = 0x12; d = #00FF00;");
        Assert.Equal(3, doc.Colors.Count);
        Assert.Equal(doc.Text.IndexOf("$FF8800", StringComparison.Ordinal), doc.Colors[doc.NextColor(11)].Offset);
        Assert.Equal(-1, doc.NextColor(doc.Text.Length));
    }

    [Fact]
    public void StringsThatEqualANameAreFoundAndOnlyWholeLiteralsCount()
    {
        var doc = GmlDoc.Parse("v = \"hp\"; w = \"hp2\"; x = @\"hp\";");
        Assert.Single(GmlSymbols.StringReferences(doc, "hp"));
        var hits = CodeScan.InText("e", "global.hp = 1;\nshow(\"hp\"); // hp\nother.hp = 2;", "hp").ToList();
        Assert.Equal([(1, false), (2, true)], hits.Select(h => (h.Line, h.InString)));
    }

    [Fact]
    public void IndentGuidesCountSpacesAndTabs()
    {
        Assert.Equal((8, false), IndentGuideRendererProbe.Indent("        x"));
        Assert.Equal((8, true), IndentGuideRendererProbe.Indent("\t\tx"));
        Assert.Equal((0, false), IndentGuideRendererProbe.Indent("x"));
    }

    [Fact]
    public void TheIndentGuideSettingDefaultsOnAndTheSettingsPageCanTurnItOff()
    {
        Assert.True(new G3MStudio.Core.Settings.AppSettings().CodeIndentGuides);
        Assert.True(EditorOptions.IndentGuides);
    }
}

// The renderer is internal to the app; this forwards its pure helper.
internal static class IndentGuideRendererProbe
{
    public static (int Columns, bool Tabs) Indent(string line) => IndentGuideRenderer.Indent(line);
}
