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

    [Fact]
    public async Task AUiLayerWithAFlexPanelAndATextItemSurvivesAWriteAndRead()
    {
        using var data = TestData.Create(1);
        data.FORM.Chunks["UILR"] = new GameMakerChunkUILR();
        var text = new GameMakerUITextItemInstance { Name = data.Strings.MakeString("label", out _), Visible = true, X = 4, Y = 8, ScaleX = 1, ScaleY = 1 };
        var panel = new GameMakerUIFlexPanel { Name = data.Strings.MakeString("panel", out _) };
        panel.Children.Add(new GameMakerUINode { Node = text });
        var layer = new GameMakerUILayer { Name = data.Strings.MakeString("ui_main", out _), Visible = true };
        layer.Children.Add(new GameMakerUINode { Node = panel });
        data.FORM.UILR.List.Add(new GameMakerUIRootNode { Node = layer });
        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var root = Assert.Single(back.FORM.UILR.List);
        var readLayer = Assert.IsType<GameMakerUILayer>(root.Node);
        var readPanel = Assert.IsType<GameMakerUIFlexPanel>(Assert.Single(readLayer.Children).Node);
        var readText = Assert.IsType<GameMakerUITextItemInstance>(Assert.Single(readPanel.Children).Node);
        Assert.Equal(("ui_main", "panel", "label", 4, 8), (readLayer.Name.Content, readPanel.Name.Content, readText.Name.Content, readText.X, readText.Y));
    }
}
