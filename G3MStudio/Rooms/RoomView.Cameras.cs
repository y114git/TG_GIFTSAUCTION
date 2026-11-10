using Avalonia;

namespace G3MStudio.Rooms;

public sealed partial class RoomView
{
    private (int Slot, Rect Start, Point From, bool Resize)? _cameraDrag;

    /// <summary>The index in the room's view list of each rectangle of <see cref="Views"/> (disabled views are not drawn).</summary>
    public IReadOnlyList<int> ViewSlots { get; set; } = [];

    /// <summary>A camera rectangle was dragged or resized: the view's index in the room and its new rectangle, in whole pixels. Raised once, when the drag ends.</summary>
    public event Action<int, Rect>? CameraEdited;

    // The outline of a camera is its handle: the bottom right corner resizes it, the other edges move it. The inside stays free for picking instances.
    private bool BeginCameraDrag(Point room)
    {
        if (!ShowViews || Tool != RoomTool.None || ViewSlots.Count != Views.Count) return false;
        var reach = 6 / _zoom;
        for (var slot = 0; slot < Views.Count; slot++)
        {
            var camera = Views[slot];
            var corner = Math.Abs(room.X - camera.Right) <= reach && Math.Abs(room.Y - camera.Bottom) <= reach;
            if (!corner && !(camera.Inflate(reach).Contains(room) && !camera.Deflate(reach).Contains(room))) continue;
            _cameraDrag = (slot, camera, room, corner);
            return true;
        }

        return false;
    }

    private void DragCamera(Point room)
    {
        if (_cameraDrag is not { } drag) return;
        var (dx, dy) = (Math.Round(room.X - drag.From.X), Math.Round(room.Y - drag.From.Y));
        var next = drag.Resize
            ? new Rect(drag.Start.X, drag.Start.Y, Math.Max(1, drag.Start.Width + dx), Math.Max(1, drag.Start.Height + dy))
            : new Rect(drag.Start.X + dx, drag.Start.Y + dy, drag.Start.Width, drag.Start.Height);
        var views = Views.ToList();
        views[drag.Slot] = next;
        Views = views;
    }

    private void EndCameraDrag()
    {
        if (_cameraDrag is not { } drag) return;
        _cameraDrag = null;
        if (Views[drag.Slot] != drag.Start) CameraEdited?.Invoke(ViewSlots[drag.Slot], Views[drag.Slot]);
    }
}
