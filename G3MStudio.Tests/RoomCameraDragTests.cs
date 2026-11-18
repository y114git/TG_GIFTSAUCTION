using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class RoomCameraDragTests
{
    [Fact]
    public async Task DraggingACameraEdgeMovesItAndItsCornerResizesItWithOneReportEach()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new RoomView { Scene = new RoomScene { Width = 1000, Height = 1000, Items = [] }, Views = [new Rect(100, 100, 200, 100)], ViewSlots = [3], ShowViews = true };
            var window = new Window { Content = view, Width = 600, Height = 400 };
            window.Show();
            var seen = new List<(int Slot, Rect Rect)>();
            view.CameraEdited += (slot, rect) => seen.Add((slot, rect));

            window.MouseDown(new Point(200, 100), MouseButton.Left);
            window.MouseMove(new Point(210, 120));
            window.MouseMove(new Point(220, 130));
            window.MouseUp(new Point(220, 130), MouseButton.Left);
            Assert.Equal((3, new Rect(120, 130, 200, 100)), Assert.Single(seen));

            window.MouseDown(new Point(320, 230), MouseButton.Left);
            window.MouseMove(new Point(340, 240));
            window.MouseUp(new Point(340, 240), MouseButton.Left);
            Assert.Equal((3, new Rect(120, 130, 220, 110)), seen[1]);
            Assert.Equal(2, seen.Count);
        });
    }

    [Fact]
    public async Task LosingTheCaptureMidDragPutsTheCameraBackAndRaisesNothing()
    {
        await HeadlessApp.Run(() =>
        {
            var view = new RoomView { Scene = new RoomScene { Width = 1000, Height = 1000, Items = [] }, Views = [new Rect(100, 100, 200, 100)], ViewSlots = [3], ShowViews = true };
            var window = new Window { Content = view, Width = 600, Height = 400 };
            window.Show();
            var seen = new List<(int Slot, Rect Rect)>();
            view.CameraEdited += (slot, rect) => seen.Add((slot, rect));

            window.MouseDown(new Point(200, 100), MouseButton.Left);
            window.MouseMove(new Point(260, 160));
            Assert.NotEqual(new Rect(100, 100, 200, 100), view.Views[0]);
            view.RaiseEvent(new PointerCaptureLostEventArgs(view, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
            Assert.Equal(new Rect(100, 100, 200, 100), view.Views[0]);
            window.MouseUp(new Point(260, 160), MouseButton.Left);
            Assert.Empty(seen);
        });
    }
}

