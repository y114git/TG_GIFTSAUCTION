using Avalonia.Controls;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the room belongs to the data, which is disposed
public sealed class UiLayerEditTests
{
    [Fact]
    public void UiLayerEditsAddAndDeleteNodesAndUndoPutsTheTreeBack()
    {
        using var data = TestData.Create(1);
        var before = data.FORM.UILR?.List.Count ?? 0;
        var add = new UiLayerCommand(UiLayerCommand.AddLayer, "", "ui_main");
        add.Apply(data);
        Assert.Equal(before + 1, data.FORM.UILR!.List.Count);
        Assert.Throws<InvalidOperationException>(() => new UiLayerCommand(UiLayerCommand.AddLayer, "", "ui_main").Apply(data));
        var panel = new UiLayerCommand(UiLayerCommand.AddPanel, "ui_main", "panel");
        panel.Apply(data);
        var text = new UiLayerCommand(UiLayerCommand.AddText, "ui_main/panel", "label");
        text.Apply(data);
        Assert.Throws<InvalidOperationException>(() => new UiLayerCommand(UiLayerCommand.AddText, "ui_main/panel/label", "x").Apply(data)); // a text item holds no children
        var layer = (GameMakerUILayer)data.FORM.UILR.List[before].Node;
        var inner = (GameMakerUIFlexPanel)layer.Children[0].Node;
        Assert.Equal("label", UiLayerCommand.NameOf(inner.Children[0].Node));

        var delete = new UiLayerCommand(UiLayerCommand.Delete, "ui_main/panel");
        delete.Apply(data);
        Assert.Empty(layer.Children);
        delete.Revert(data);
        Assert.Single(layer.Children);
        text.Revert(data);
        panel.Revert(data);
        add.Revert(data);
        Assert.Equal(before, data.FORM.UILR?.List.Count ?? 0);
        Assert.Equal(panel.Args, UiLayerCommand.FromArgs(panel.Args).Args);
    }

    [Fact]
    public async Task TheAddLayerPopupAddsAUiLayerToTheDataFileAndTheTabEditsItsNodes()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var data = workspace.Data;
        var room = new GameMakerRoom { Name = data.Strings.MakeString("rm_ui", out _), Layers = [] };
        data.Rooms.Add(room);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var preview = new RoomPreview(room, textures, workspace);
            await preview.Ready;
            var layers = Assert.IsType<RoomLayersPanel>(((StackPanel)preview.SidePanel!).Children[0]);
            layers.AddLayer(6); // the UI kind is the last one
            var root = data.FORM.UILR!.List.Single(r => UiLayerCommand.NameOf(r.Node) == "UILayer");
            Assert.Empty(room.Layers); // the layer belongs to the data file, not to the room
            var view = new UiLayerView(root, workspace);
            Assert.Equal("UILayer", view.Picked);
            Assert.True(view.Add(UiLayerCommand.AddPanel));
            view.Select("UILayer/panel");
            Assert.True(view.Add(UiLayerCommand.AddText));
            var panel = (GameMakerUIFlexPanel)((GameMakerUILayer)root.Node).Children[0].Node;
            Assert.Equal("text", UiLayerCommand.NameOf(Assert.Single(panel.Children).Node));
            view.Select("UILayer/panel/text");
            Assert.True(view.DeleteNode());
            Assert.Empty(panel.Children);
            workspace.History.Undo();
            Assert.Single(panel.Children);
            view.Select("UILayer");
            Assert.False(view.DeleteNode());
        });
    }
}
