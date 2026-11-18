using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class GmlDocNamesTests
{
    private const string Text = """
        #macro GRAVITY 0.5
        enum State { idle, walk, jump = 5 }
        globalvar lives;
        state = State.idle;
        function Vector(vx, vy) constructor { self.vx = vx; }
        var total = 0;
        #region Setup
        #endregion
        """;

    [Fact]
    public void CompletionListsWhatTheTextDeclares()
    {
        var names = GmlCompletion.DocNames(GmlDoc.Parse(Text)).ToDictionary(n => n.Key, n => n.Value);
        Assert.Equal("Variable", names["total"]);
        Assert.Equal("Variable", names["vx"]);
        Assert.Equal("Variable", names["lives"]);
        Assert.Equal("Function", names["Vector"]);
        Assert.Equal("Constant", names["GRAVITY"]);
        Assert.Equal("Constant", names["State"]);
        Assert.False(names.ContainsKey("Setup"));
    }

    [Fact]
    public void AfterTheNameOfAnEnumCompletionListsItsMembers()
    {
        var doc = GmlDoc.Parse(Text);
        Assert.Equal(["idle", "walk", "jump"], GmlMembers.EnumMembers(doc, "State"));
        var dot = Text.IndexOf("State.idle", StringComparison.Ordinal) + "State".Length;
        Assert.Equal(["idle", "walk", "jump"], GmlMembers.After(doc, dot, []).Select(m => m.Key));
        Assert.Empty(GmlMembers.EnumMembers(doc, "lives"));
    }

    [Fact]
    public void TheReferencesOfAnEnumMemberTakeTheDeclarationInThatEnum()
    {
        const string text = "enum A { idle }\nenum B { idle }\nx = B.idle;";
        var doc = GmlDoc.Parse(text);
        var declared = text.IndexOf("idle", text.IndexOf("enum B", StringComparison.Ordinal), StringComparison.Ordinal);
        var used = text.LastIndexOf("idle", StringComparison.Ordinal);
        Assert.Equal([(declared, declared + 4), (used, used + 4)], GmlSymbols.EnumMemberReferences(doc, "B", "idle"));
    }

    [Fact]
    public void ACommaInsideAnEnumValueStartsNoMember()
    {
        var doc = GmlDoc.Parse("enum State { idle = max(1, Base.value), walk }");
        Assert.Equal(["idle", "walk"], GmlMembers.EnumMembers(doc, "State"));
    }
}
