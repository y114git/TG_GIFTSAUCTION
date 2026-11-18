using Avalonia.Media;
using G3MStudio.Explorer;

namespace G3MStudio.Tests;

public sealed class ExplorerQuickIconTests
{
    [Theory]
    [InlineData("quick")]
    [InlineData("quick-recent")]
    [InlineData("quick-favourites")]
    [InlineData("quick-searches")]
    [InlineData("quick-tags")]
    [InlineData("quick-collections")]
    [InlineData("quick-folders")]
    [InlineData("folder:3")]
    [InlineData("collection:Boss")]
    [InlineData("tag:wip")]
    [InlineData("search")]
    public async Task EveryQuickAccessRowHasAnIconOfItsOwn(string key)
    {
        var data = ResourceIcons.DataOf(key);
        Assert.NotEqual(ResourceIcons.DataOf("no-such-type"), data);
        await HeadlessApp.Run(() =>
        {
            Assert.NotNull(Geometry.Parse(data));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void TheQuickAccessIconsDifferFromEachOther()
    {
        string[] keys = ["quick", "quick-recent", "quick-favourites", "quick-searches", "quick-tags", "quick-collections", "quick-folders"];
        Assert.Equal(keys.Length, keys.Select(ResourceIcons.DataOf).Distinct().Count());
    }
}
