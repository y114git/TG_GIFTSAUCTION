using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>
/// The canvas size by dragging: a press on one of the handles just outside an edge or corner of the picture drags that side in whole pixels, Shift keeps the aspect,
/// Ctrl stretches the picture along with the canvas, Escape cancels, and the release reports one <see cref="ResizeSpec"/> (the far side is the anchor).
/// The editor turns it into one undoable edit. The handles are the same ones the sprite editor shows (<see cref="CanvasHandles"/>).
/// </summary>
public sealed partial class ImageCanvas
{
    private CanvasHandles Handles => field ??= CreateHandles();

    private CanvasHandles CreateHandles()
    {
        var handles = new CanvasHandles(this, () => PictureRect, () => Zoom, () => (Picture.Width, Picture.Height)) { HandleSize = () => HandleSize, Blocked = () => Locked || _drawing || Collecting };
        handles.DragChanged += spec => ResizeDragging?.Invoke(spec);
        handles.Hovered += over => HandleHovered?.Invoke(over);
        handles.SideChanged += UpdateCursor;
        handles.Dropped += spec =>
        {
            // The pixels stay where they are on screen: a drag on the left or top side moves the picture's corner, not its content. Only once the size is applied.
            var shift = new Vector((spec.Width - Picture.Width) * spec.AnchorX / 2 * Zoom, (spec.Height - Picture.Height) * spec.AnchorY / 2 * Zoom);
            var done = ResizeDropped?.Invoke(spec) == true;
            if (done && !_fitted) Origin -= shift;
            return done;
        };
        return handles;
    }

    /// <summary>True when the edge and corner handles may change the canvas size.</summary>
    public bool CanResize
    {
        get;
        set
        {
            field = value;
            Handles.Enabled = value;
            InvalidateVisual();
        }
    } = true;

    /// <summary>True while the pointer is over a handle (and no drag is on).</summary>
    public bool OverHandle => Handles.OverHandle;

    /// <summary>The editor toggle "All frames": a handle drag then applies to every frame of the sprite.</summary>
    public bool AllFrames
    {
        get => Handles.AllFrames;
        set => Handles.AllFrames = value;
    }

    /// <summary>The canvas size being dragged; null when no drag is on.</summary>
    public ResizeSpec? ResizeDrag => Handles.Drag;

    /// <summary>Raised when the dragged size changes, and with null when the drag ends or is cancelled.</summary>
    public event Action<ResizeSpec?>? ResizeDragging;

    /// <summary>Raised on release with the new canvas size, when it differs from the picture's; the handler returns false when the size could not be applied.</summary>
    public event Func<ResizeSpec, bool>? ResizeDropped;

    /// <summary>Raised when the pointer comes onto a handle (true) or leaves it (false), so the editor can say what a drag does.</summary>
    public event Action<bool>? HandleHovered;

    private bool ResizeOn => Handles.Dragging;

    private (int X, int Y)? HandleSide => Handles.Active;

    internal (int X, int Y)? HandleAt(Point p) => Handles.HandleAt(p);

    internal static (int Width, int Height) DragSize((int Width, int Height) size, (int X, int Y) side, Vector delta, bool keepAspect) => CanvasHandles.DragSize(size, side, delta, keepAspect);

    private bool BeginResize(PointerPressedEventArgs e, Point at) => Handles.Begin(e, at);

    private bool MoveResize(Point at, KeyModifiers modifiers) => Handles.Move(at, modifiers);

    private bool EndResize(KeyModifiers modifiers) => Handles.End(modifiers);

    private void ClearHandleHover() => Handles.ClearHover();

    /// <summary>Escape: the drag ends and the canvas keeps its size.</summary>
    public bool CancelResize() => Handles.Cancel();

    private static StandardCursorType SideCursor((int X, int Y) side) => CanvasHandles.SideCursor(side);

    private void DrawHandles(DrawingContext context) => Handles.Draw(context);
}
