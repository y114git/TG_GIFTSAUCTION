using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class UiLayersTests
{
    [Fact]
    public void TheUiLayersOfAGameAreADataTypeAndListTheirProperties()
    {
        using var data = TestData.Create(1);
        var type = ResourceTypes.All.Single(t => t.Key == "uiLayers");
        Assert.Empty(type.Items(data) ?? []);
        data.FORM.Chunks["UILR"] = new GameMakerChunkUILR();
        data.FORM.UILR.List.Add(new GameMakerUIRootNode { Node = new GameMakerUILayer { Name = data.Strings.MakeString("ui_main", out _) } });
        var root = Assert.Single(type.Items(data)!);
        Assert.Equal("ui_main", ResourceTypes.NameOf(root));
        Assert.NotEmpty(PropertyModel.Describe(root));
    }
}
