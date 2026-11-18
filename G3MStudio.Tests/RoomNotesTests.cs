using Avalonia;
using G3MLib.DataFile.Models;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

/// <summary>Effect layers and scrolling layers show as labelled notes that cannot be picked; view outlines carry their labels.</summary>
public sealed class RoomNotesTests
{
#pragma warning disable CA2000 // model objects owned by the room of the test
    [Fact]
    public async Task AnEffectLayerAndAScrollingBackgroundAreNotesThatAreNotPicked()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, background, instance) = RoomRenderTests.Room();
            background.HSpeed = 2;
            room.Layers!.Add(new GameMakerRoom.Layer
            {
                LayerName = new GameMakerString("Fx"),
                LayerType = GameMakerRoom.LayerType.Effect,
                LayerDepth = 50,
                IsVisible = true,
                EffectType = new GameMakerString("_filter_blur"),
            });
            using var scene = RoomScene.Build(room);
            Assert.Contains(scene.Markers, m => m.Element is null && m.Label == "effect: _filter_blur");
            Assert.Contains(scene.Markers, m => m.Element is null && m.Label.StartsWith("scroll 2", StringComparison.Ordinal));
            var view = new RoomView { Scene = scene };
            Assert.Same(instance, view.HitTest(new Point(30, 30)));
            Assert.Null(view.HitTest(new Point(2, 2))); // the notes sit at the top left and are not elements
            Assert.Null(scene.BoundsOf(new object()));
            RoomRenderTests.Draw(view);
            view.SelectAll();
            Assert.Single(view.Selection);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ViewOutlinesDrawWithTheirLabels()
    {
        await HeadlessApp.Run(() =>
        {
            var (room, _, _) = RoomRenderTests.Room();
            using var scene = RoomScene.Build(room);
            var view = new RoomView { Scene = scene, ShowViews = true, Views = [new Rect(8, 8, 40, 40)], ViewLabels = ["view 0: camera 8, 8 40x40, port 0, 0 80x80"] };
            var picture = RoomRenderTests.Draw(view);
            Assert.NotEmpty(picture);
            return Task.CompletedTask;
        });
    }
#pragma warning restore CA2000
}
