using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ExplorerNarrowFoldTests
{
    private static ExplorerModel NewModel() => new(key => key);

    [Fact]
    public void ATypeHeadingFoldsAndUnfoldsWhileAFilterIsActive()
    {
        using var data = ExplorerModelTests.Generate(40);
        var model = NewModel();
        model.Load(data);
        model.SetFilter("1");
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "sprites" });
        model.Toggle("sprites");
        Assert.DoesNotContain(model.Rows, r => r is { IsItem: true, Key: "sprites" });
        Assert.False(model.Rows.Single(r => r is { Kind: ExplorerRowKind.Type, Key: "sprites" }).Expanded);
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "objects" });
        model.Toggle("sprites");
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "sprites" });
    }

    [Fact]
    public void TheDataGroupFoldsWhileAFilterIsActive()
    {
        using var data = ExplorerModelTests.Generate(40);
        var model = new ExplorerModel(key => key) { OpenDataOnSearch = true }; // the old behaviour, on by choice
        model.Load(data);
        model.SetFilter("1");
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "strings" });
        model.Toggle(ExplorerModel.DataGroup);
        Assert.DoesNotContain(model.Rows, r => r is { IsItem: true, Key: "strings" });
    }

    [Fact]
    public void FoldsMadeWhileFilteringAreForgottenWhenTheFilterEndsAndLeaveTheFullTreeAlone()
    {
        using var data = ExplorerModelTests.Generate(40);
        var model = NewModel();
        model.Load(data);
        model.SetFilter("1");
        model.Toggle("sprites");
        model.SetFilter("");
        Assert.True(model.Rows.Single(r => r is { Kind: ExplorerRowKind.Type, Key: "sprites" }).Expanded);
        model.SetFilter("1");
        Assert.True(model.Rows.Single(r => r is { Kind: ExplorerRowKind.Type, Key: "sprites" }).Expanded);
    }

    [Fact]
    public void FoldAllOpensAndClosesEveryTypeAndAPrefixIsKnownPerType()
    {
        using var data = ExplorerModelTests.Generate(40);
        var model = NewModel();
        model.Load(data);
        Assert.True(model.IsType("sprites"));
        Assert.Equal("spr", ExplorerModel.PrefixOf("sprites"));
        model.FoldAll(false);
        Assert.DoesNotContain(model.Rows, r => r.IsItem);
        model.FoldAll(true);
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "sprites" });
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "strings" });
    }

    [Fact]
    public void ATypeWithManyEntriesStartsFoldedUntilTheUserOpensItOrAFilterMatches()
    {
        using var data = ExplorerModelTests.Generate(4 * (ExplorerModel.FoldedAbove + 10));
        var model = NewModel();
        model.Load(data);
        Assert.False(model.Rows.Single(r => r is { Kind: ExplorerRowKind.Type, Key: "sprites" }).Expanded);
        Assert.DoesNotContain(model.Rows, r => r is { IsItem: true, Key: "sprites" });
        Assert.True(model.Rows.Count < 100);
        model.SetFilter("spr_77");
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "sprites" });
        model.SetFilter("");
        model.Toggle("sprites");
        model.Load(data);
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "sprites" });
        using var small = ExplorerModelTests.Generate(40);
        var other = NewModel();
        other.Load(small);
        Assert.Contains(other.Rows, r => r is { IsItem: true, Key: "sprites" });
    }
}
