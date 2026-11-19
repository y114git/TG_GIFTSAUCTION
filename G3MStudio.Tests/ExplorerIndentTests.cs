using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ExplorerIndentTests
{
    [Fact]
    public void AnEntryIsIndentedFurtherThanTheHeadingItIsListedUnder()
    {
        var recent = new ExplorerRow(ExplorerRowKind.Type, ExplorerModel.QuickRecent, "Recent", 2);
        var entry = new ExplorerRow(ExplorerRowKind.Item, "sprites", "spr_player", 3);
        Assert.True(entry.IndentDepth > recent.IndentDepth);
        Assert.Equal(recent.IndentDepth + 1, entry.IndentDepth);
    }
}
