using G3MLib.Analyzer.Compiler;
using G3MLib.DataFile.Compiler;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Navigation;
using G3MStudio.Editor;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class GmlReferenceTests
{
    private static readonly GmlReference Reference = GmlReference.Default;

    [Fact]
    public void TheBundledReferenceHasTheCommonFunctionsAndConstantGroups()
    {
        Assert.True(Reference.Entries.Count(e => e.Kind == RefKind.Function) >= 600);
        Assert.True(Reference.Entries.Count(e => e.Kind == RefKind.Constant) >= 30);
        Assert.All(Reference.Entries, e => Assert.True(e.Summary.Length > 0 && e.Category.Length > 0));
    }

    [Fact]
    public void EveryFunctionAgreesWithTheCompilerTablesOfTheLibrary()
    {
        var table = new BuiltinList().Functions;
        foreach (var e in Reference.Entries.Where(e => e.Kind == RefKind.Function && table.ContainsKey(e.Name)))
        {
            var info = table[e.Name];
            if (info.MinArguments > 0) Assert.True(e.Required == info.MinArguments, e.Name);
        }
    }

    [Fact]
    public void FindReturnsTheFunctionOrTheLongestMatchingConstantGroup()
    {
        var draw = Reference.Find("draw_text")!;
        Assert.Equal("draw_text(x, y, string)", draw.Signature);
        Assert.Equal("Text", draw.Category.Split(' ')[^1]);
        Assert.Equal("gp_*", Reference.Find("gp_face1")!.Name);
        Assert.Equal("gp_axis*", Reference.Find("gp_axislh")!.Name);
        Assert.Equal("vk_*", Reference.Find("vk_space")!.Name);
        Assert.Null(Reference.Find("not_a_gml_name"));
    }

    [Fact]
    public void OptionalAndRestParametersAreMarkedInTheSignature()
    {
        Assert.Equal("array_push(array, ...value)", Reference.Find("array_push")!.Signature);
        Assert.Contains("[offset]", Reference.Find("array_reverse")!.Signature, StringComparison.Ordinal);
        Assert.True(Reference.Find("game_load")!.Deprecated);
        Assert.True(Reference.Find("image_number")!.ReadOnly);
    }

    [Fact]
    public void ManualLinksFollowTheOfficialPathPattern()
    {
        var link = Reference.ManualLink(Reference.Find("string_length")!);
        Assert.Equal("https://manual.gamemaker.io/lts/en/GameMaker_Language/GML_Reference/Strings/string_length.htm", link);
        Assert.EndsWith("/Drawing/Text/draw_text.htm", Reference.ManualLink(Reference.Find("draw_text")!), StringComparison.Ordinal);
        Assert.EndsWith("/Game_Input/Keyboard_Input/keyboard_check.htm", Reference.ManualLink(Reference.Find("vk_up")!), StringComparison.Ordinal);
        Assert.EndsWith("GML_Reference.htm", Reference.ManualLink(Reference.Find("os_windows")!), StringComparison.Ordinal);
        Assert.Contains("colour", Reference.ManualLink(Reference.Find("draw_set_color")!), StringComparison.Ordinal);
    }

    [Fact]
    public void SearchRanksNamePrefixThenNameThenDescriptionAndFiltersByCategory()
    {
        var result = Reference.Search("array_p");
        Assert.Equal("array_pop", result[0].Name);
        Assert.Contains(result, e => e.Name == "array_push");
        Assert.All(Reference.Search("", "Strings"), e => Assert.Equal("Strings", e.Category));
        Assert.NotEmpty(Reference.Search("upper case"));
        Assert.Empty(Reference.Search("zzzzqq"));
    }

    [Fact]
    public void ADocumentedSignatureShowsRealParameterNamesAndTheActiveParameterDescription()
    {
        var signature = Signature.FromReference(Reference.Find("draw_text")!);
        Assert.Equal(["x", "y", "string"], signature.Parameters);
        Assert.Equal(3, signature.Required);
        var line = GmlHelp.ActiveParameter(signature, 1);
        Assert.StartsWith("y: Real", line, StringComparison.Ordinal);
        Assert.Contains("Draws a string", line, StringComparison.Ordinal);
        Assert.Equal("", GmlHelp.ActiveParameter(Signature.FromCounts("x", 1, 1), 0));
    }

    [Fact]
    public void ACompletedCallHasATabStopOnEachRequiredParameter()
    {
        var call = CallSnippet.For(Signature.FromReference(Reference.Find("draw_sprite_ext")!));
        Assert.StartsWith("draw_sprite_ext(sprite, subimg, x, y,", call.Text, StringComparison.Ordinal);
        Assert.Equal(9, call.Stops.Count);
        var (start, length) = call.Stops[0];
        Assert.Equal("sprite", call.Text.Substring(start, length));
        Assert.Equal("game_restart()", CallSnippet.For(Signature.FromReference(Reference.Find("game_restart")!)).Text);
        var optional = CallSnippet.For(Signature.FromReference(Reference.Find("game_end")!));
        Assert.Equal("game_end()", optional.Text);
        Assert.Equal(("game_end(".Length, 0), optional.Stops[0]);
    }

    [Fact]
    public void KeywordsAndSnippetsAreOfferedBesideNames()
    {
        var names = new[] { new KeyValuePair<string, string>("iffy", "Function") };
        var found = GmlCompletion.Candidates(names, "i").ToLookup(k => k.Key, k => k.Value);
        Assert.Contains("Keyword", found["if"]);
        Assert.Contains("Snippet", found["ifelse"]);
        Assert.Contains("Function", found["iffy"]);
    }

    [Fact]
    public void MembersAfterADotComeFromGlobalsConstructorsAndKnownVariables()
    {
        const string text = "global.score = 1;\nglobal.lives = 3;\nfunction Hero() constructor { hp = 10; mp = 5; }\nvar h = new Hero();\nh.hp = 2;\nglobal.";
        var doc = GmlDoc.Parse(text);
        Assert.Equal(["lives", "score"], GmlMembers.After(doc, text.Length - 1, ["x"]).Select(m => m.Key));
        var h = text.IndexOf("h.hp", StringComparison.Ordinal) + 1;
        Assert.Equal(["hp", "mp"], GmlMembers.After(doc, h, ["x"]).Select(m => m.Key));
        var other = GmlDoc.Parse("y.a = 1;\ny.");
        Assert.Equal(["a", "x"], GmlMembers.After(other, 9, ["x"]).Select(m => m.Key));
    }

    [Fact]
    public void LineEditsDeleteJoinSortTrimAndConvertCase()
    {
        static string Apply(string text, LineEdit? e) => e is null ? text : text.Remove(e.Offset, e.Length).Insert(e.Offset, e.Text);
        static string Trim(string text) => GmlLineEdits.TrimTrailingWhitespace(text, 0, 0).OrderByDescending(e => e.Offset).Aggregate(text, (t, e) => t.Remove(e.Offset, e.Length).Insert(e.Offset, e.Text));

        Assert.Equal("a\nc", Apply("a\nb\nc", GmlLineEdits.DeleteLines("a\nb\nc", 3, 3)));
        Assert.Equal("a\nb", Apply("a\nb\nc", GmlLineEdits.DeleteLines("a\nb\nc", 4, 4)));
        Assert.Equal("a b c", Apply("a\n   b\nc", GmlLineEdits.JoinLines("a\n   b\nc", 0, 8)));
        Assert.Equal("a b\nc", Apply("a\nb\nc", GmlLineEdits.JoinLines("a\nb\nc", 0, 0)));
        Assert.Equal("a\nB\nc", Apply("c\nB\na", GmlLineEdits.SortLines("c\nB\na", 0, 5)));
        Assert.Equal("c\nB\na", Apply("a\nB\nc", GmlLineEdits.SortLines("a\nB\nc", 0, 5)));
        Assert.Equal("a;\r\nb\r\n", Trim("a;  \r\nb\t\r\n"));
        Assert.Empty(GmlLineEdits.TrimTrailingWhitespace("a\nb", 0, 0));
        Assert.Equal("say HELLO now", Apply("say hello now", GmlLineEdits.ConvertCase("say hello now", 6, 6, GmlLineEdits.CaseMode.Upper)));
        Assert.Equal("Hello World", Apply("hELLO wORLD", GmlLineEdits.ConvertCase("hELLO wORLD", 0, 11, GmlLineEdits.CaseMode.Title)));
        Assert.Null(GmlLineEdits.ConvertCase("abc", 0, 3, GmlLineEdits.CaseMode.Lower));
    }

    [Fact]
    public void ExpandingTheSelectionGrowsFromWordToBracketsToLineToText()
    {
        const string text = "foo(bar, baz);\nx = 1;";
        var doc = GmlDoc.Parse(text);
        var at = text.IndexOf("baz", StringComparison.Ordinal) + 1;
        var word = GmlSelection.Expand(doc, at, at)!.Value;
        Assert.Equal("baz", text[word.Start..word.End]);
        var inside = GmlSelection.Expand(doc, word.Start, word.End)!.Value;
        Assert.Equal("bar, baz", text[inside.Start..inside.End]);
        var brackets = GmlSelection.Expand(doc, inside.Start, inside.End)!.Value;
        Assert.Equal("(bar, baz)", text[brackets.Start..brackets.End]);
        var line = GmlSelection.Expand(doc, brackets.Start, brackets.End)!.Value;
        Assert.Equal("foo(bar, baz);", text[line.Start..line.End]);
        Assert.Equal((0, text.Length), GmlSelection.Expand(doc, line.Start, line.End)!.Value);
        Assert.Null(GmlSelection.Expand(doc, 0, text.Length));
    }

    [Fact]
    public void BracketsGetTheirNestingLevelAcrossAllThreeKinds()
    {
        const string text = "a(b[c{d}])";
        var depths = GmlColorizer.BracketDepths(text, GmlTokenizer.Tokenize(text, true));
        Assert.Equal([0, 1, 2, 2, 1, 0], depths.OrderBy(d => d.Key).Select(d => d.Value));
    }

    [Fact]
    public void GoToWithTheFPrefixListsTheReferenceEntries()
    {
        var all = new List<GoToEntry>
        {
            new(GoToKind.Reference, "draw_text", "ref:draw_text", "", "Draws a string at a position."),
            new(GoToKind.Function, "draw_thing", "functions:draw_thing", "functions"),
            new(GoToKind.Code, "draw_code", "code:draw_code", "code"),
        };
        var found = GoToSearch.Run(all, "f:draw", []);
        Assert.Equal(2, found.Count);
        Assert.DoesNotContain(found, e => e.Kind == GoToKind.Code);
    }

    [Fact]
    public async Task TheReferencePanelSearchesInsertsAndOpensTheManual()
    {
        await HeadlessApp.Run(() =>
        {
            var panel = new ReferencePanel(Reference);
            var window = new Avalonia.Controls.Window { Content = panel, Width = 500, Height = 600 };
            window.Show();
            string? inserted = null, manual = null;
            panel.InsertRequested += t => inserted = t;
            panel.ManualRequested += u => manual = u;
            panel.Show("string_length");
            Assert.Equal("string_length", panel.Selected?.Name);
            Assert.Contains("Counts the characters", panel.DetailText, StringComparison.Ordinal);
            Assert.Equal("string_length(string)", ReferencePanel.InsertText(panel.Selected!));
            Assert.Null(ReferencePanel.InsertText(Reference.Find("vk_*")!));
            panel.Show("array_");
            Assert.All(panel.Shown, e => Assert.Contains("array_", e.Name, StringComparison.Ordinal));
            window.Close();
            Assert.Null(inserted);
            Assert.Null(manual);
            return Task.CompletedTask;
        });
    }
}
