using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Rename inside one script is scope-aware, a rename across all code lists the uses it cannot resolve, and a new name must be one the compiler reads as an identifier.</summary>
public sealed class CodeRenameScopeTests
{
    private static string Local(string text, string word, string newName, int occurrence = 0)
    {
        var at = -1;
        for (var i = 0; i <= occurrence; i++) at = text.IndexOf(word, at + 1, StringComparison.Ordinal);
        var plan = GmlSymbols.PlanRename(GmlDoc.Parse(text), at, newName, _ => false)!;
        return GmlSymbols.Apply(text, plan.Edits);
    }

    [Fact]
    public void ALocalRenameStaysInsideItsFunction()
    {
        const string text = "function a() { var score = 0; return score; }\nfunction b() { score = 5; return score; }";
        Assert.Equal("function a() { var points = 0; return points; }\nfunction b() { score = 5; return score; }", Local(text, "score", "points"));
    }

    [Fact]
    public void AnInstanceVariableRenameSkipsFunctionsWithALocalOfThatName()
    {
        const string text = "function a() { var score = 0; return score; }\nfunction b() { score = 5; return score; }";
        Assert.Equal("function a() { var score = 0; return score; }\nfunction b() { points = 5; return points; }", Local(text, "score", "points", 2));
    }

    [Fact]
    public void ANestedFunctionDoesNotSeeALocalOfItsParent()
    {
        const string text = "function a() { var s = 1; var g = function() { return s; }; return s; }";
        Assert.Equal("function a() { var t = 1; var g = function() { return s; }; return t; }", Local(text, "s", "t"));
    }

    [Fact]
    public void AParameterIsRenamedInItsOwnFunctionOnly()
    {
        const string text = "function a(v) { return v; }\nfunction b() { return v; }";
        Assert.Equal("function a(w) { return w; }\nfunction b() { return v; }", Local(text, "v", "w"));
    }

    [Fact]
    public void ATopLevelLocalLeavesFunctionsAlone()
    {
        const string text = "var n = 1;\nfunction f() { return n; }\nx = n;";
        Assert.Equal("var m = 1;\nfunction f() { return n; }\nx = m;", Local(text, "n", "m"));
    }

    [Fact]
    public void AStructKeyAndAGlobalMemberAreNotTheLocal()
    {
        const string text = "function a() { var hp = 1; global.hp = hp; return { hp: hp }; }";
        Assert.Equal("function a() { var life = 1; global.hp = life; return { hp: life }; }", Local(text, "hp", "life"));
    }

    [Fact]
    public void ARenameAcrossCodeRenamesSelfAndGlobalAndHoldsBackTheOtherMembers()
    {
        var doc = GmlDoc.Parse("hp = 1;\nself.hp = 2;\nglobal.hp = 3;\nother.hp = 4;\ninst.hp += 1;\nvar s = { hp: 5 };\nenum E { hp }\nx = E.hp;");
        var (rename, review) = GmlSymbols.AcrossReferences(doc, "hp");
        Assert.Equal(3, rename.Count);
        Assert.Equal(["other.hp = 4;", "inst.hp += 1;", "var s = { hp: 5 };", "enum E { hp }", "x = E.hp;"], review.Select(r => Line(doc.Text, r.Start)));
    }

    [Fact]
    public void ASelfMemberWithALocalOfTheSameNameNeedsReview()
    {
        var (rename, review) = GmlSymbols.AcrossReferences(GmlDoc.Parse("var hp = 1;\nself.hp = hp;"), "hp");
        Assert.Equal(2, rename.Count);
        Assert.Single(review);
    }

    private static string Line(string text, int offset) => text.Split('\n')[text.AsSpan(0, offset).Count('\n')];

    [Theory]
    [InlineData("hp", true)]
    [InlineData("_x1", true)]
    [InlineData("score_2", true)]
    [InlineData("héros", false)]
    [InlineData("begin", false)]
    [InlineData("end", false)]
    [InlineData("then", false)]
    [InlineData("repeat", false)]
    [InlineData("true", false)]
    [InlineData("self", false)]
    [InlineData("1x", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    public void ANewNameIsValidOnlyWhenTheCompilerReadsOneIdentifier(string name, bool valid) => Assert.Equal(valid, GmlSymbols.IsValidName(name));

    [Fact]
    public async Task AnAcrossRenameFindsEntriesWithOnlyMemberUsesAndListsThem()
    {
        using var dir = new TempDir();
        using var workspace = await CodeIntelUiTests.Open(dir);
        workspace.Edit(new EditCodeCommand("gml_Script_probe", "other.hp = 1;\nhp = 2;\n"));
        var plan = await CodeRename.PlanAsync(workspace, "hp", "life", false, TestContext.Current.CancellationToken);
        Assert.Equal(1, plan.Uses);
        var hit = Assert.Single(plan.Review);
        Assert.Equal(("gml_Script_probe", 1, "other.hp = 1;"), (hit.Entry, hit.Line, hit.Text));
        Assert.Contains("other.hp = 1;", CodeRenameDialog.Describe(plan), StringComparison.Ordinal);
        workspace.Edit(new EditCodeCommand("gml_Script_probe", "other.hp = 1;\n"));
        var only = await CodeRename.PlanAsync(workspace, "hp", "life", false, TestContext.Current.CancellationToken);
        Assert.Empty(only.Changes);
        Assert.Single(only.Review); // an entry with only member uses is still looked at
    }
}
