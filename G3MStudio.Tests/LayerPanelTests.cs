using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>
/// The Layers panel: the current layer has the hover fill (no outline), a layer is renamed from the button, a double click or the rename key, as one undoable edit,
/// and the rows stay (only change) while the number of layers stays.
/// </summary>
public sealed class LayerPanelTests
{
    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static Button NameButton(ImageEditorView view, int layer) => view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "LayerSelect_" + layer);

    [Fact]
    public async Task TheCurrentLayerHasTheHoverFillAndARenameIsOneUndoableEdit()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            using var view = new ImageEditorView(new WorkspaceImageSession(workspace, "spr_a"));
            var window = await LayoutAudit.Host(view, 1200, 900);
            try
            {
                Assert.True(view.Run("img.layerAdd"));
                var rows = view.GetVisualDescendants().OfType<Border>().Where(b => b.Name?.StartsWith("LayerRow_", StringComparison.Ordinal) == true).ToList();
                Assert.All(rows, r => Assert.Equal(default, r.BorderThickness)); // no outline on any row
                Assert.Contains("layerCurrent", NameButton(view, 1).Classes);
                Assert.DoesNotContain("layerCurrent", NameButton(view, 0).Classes);

                // Hiding a layer changes its row in place: the same controls, the eye shows the new state.
                var eye = view.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(b => b.Name == "LayerEye_1");
                Assert.True(view.Run("img.layerVisible"));
                Assert.Same(eye, view.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(b => b.Name == "LayerEye_1"));
                Assert.False(eye.IsChecked);
                Assert.True(view.Run("img.layerVisible"));

                var position = workspace.History.Position;
                var original = view.Canvas.Layers.Layers[1].Name; // worded in the language of the moment, which other tests share
                Assert.True(view.Run("img.layerRename"));
                await LayoutAudit.Settle();
                Assert.Equal(1, view.RenamingLayer);
                Assert.Single(view.GetVisualDescendants().OfType<TextBox>(), b => b.Name == "LayerRenameBox");
                view.CommitRename("Hero; outline\nlayer");
                Assert.Equal(-1, view.RenamingLayer);
                Assert.Equal("Hero  outline layer", view.Canvas.Layers.Layers[1].Name); // the separators of the edit text become spaces
                Assert.Equal("Hero  outline layer", NameButton(view, 1).Content);
                Assert.Equal(position + 1, workspace.History.Position);
                view.Undo();
                Assert.Equal(original, view.Canvas.Layers.Layers[1].Name);
                view.Redo();
                Assert.Equal("Hero  outline layer", view.Canvas.Layers.Layers[1].Name);

                // Escape keeps the name; the rename key (F2) inside the editor renames the current layer, a double click the clicked one.
                view.BeginRename(0);
                var box = view.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "LayerRenameBox");
                var bottom = view.Canvas.Layers.Layers[0].Name;
                box.Text = "Nope";
                box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                Assert.Equal(bottom, view.Canvas.Layers.Layers[0].Name);
                Assert.True(view.HandleKey(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F2, Source = view }));
                Assert.Equal(view.ActiveLayer, view.RenamingLayer);
                view.CommitRename("Top");
                NameButton(view, 0).RaiseEvent(new Avalonia.Input.TappedEventArgs(InputElement.DoubleTappedEvent, null!));
                Assert.Equal(0, view.RenamingLayer);
                view.CommitRename("Bottom");
                Assert.Equal(["Bottom", "Top"], view.Canvas.Layers.Layers.Select(l => l.Name));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ARenameInProgressStaysWithItsFrameWhenAnotherFrameIsShown()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            using var session = new WorkspaceImageSession(workspace, "spr_a");
            Assert.True(session.ChangeFrames(FrameListCommand.Duplicate, 0));
            using var view = new ImageEditorView(session);
            var window = await LayoutAudit.Host(view, 1200, 900);
            try
            {
                // Two layers on each frame: a frame of one layer keeps no name of its own.
                view.ShowFrame(1);
                Assert.True(view.Run("img.layerAdd"));
                view.ShowFrame(0);
                Assert.True(view.Run("img.layerAdd"));
                var original = session.ReadLayers(1).Layers[1].Name;
                view.BeginRename(1);
                view.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "LayerRenameBox").Text = "Renamed";
                view.ShowFrame(1);
                if (view.RenamingLayer >= 0) view.CommitRename("Renamed"); // the box outlived the frame it was made for
                Assert.Equal("Renamed", session.ReadLayers(0).Layers[1].Name);
                Assert.Equal(original, session.ReadLayers(1).Layers[1].Name);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
