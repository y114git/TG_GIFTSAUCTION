using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>
/// The eight handles just outside a picture, shared by the image editor and the sprite editor, so both behave the same. A press on one drags that side in whole pixels (the far side
/// is the anchor), Shift keeps the aspect, Ctrl stretches the picture to the new size instead of adding or cutting canvas, Esc cancels. The release reports one <see cref="ResizeSpec"/>.
/// </summary>
internal sealed partial class CanvasHandles(Control host, Func<Rect> picture, Func<double> zoom, Func<(int Width, int Height)> size)
{
    private (int X, int Y)? _side;
    private (int X, int Y)? _hover;
    private Point _from;
    private Point _at;
    private (int Width, int Height) _to;
    private bool _stretch;
    private bool _modAll;
    private IPointer? _pointer;

    /// <summary>Half the drawn square less one, in screen pixels.</summary>
    public Func<int> HandleSize { get; set; } = () => 3;

    /// <summary>Extra room between the picture's edge and the handle, so handles inside the picture (a collision box) never touch these.</summary>
    public double Gap { get; set; }

    /// <summary>Screen pixels beyond the drawn square where a press still grabs the handle.</summary>
    public double ReachExtra { get; set; } = 3;

    public bool Enabled { get; set; } = true;

    /// <summary>True while something else owns the pointer (a stroke, a shape): the handles are neither drawn nor grabbed.</summary>
    public Func<bool>? Blocked { get; set; }

    public bool Dragging => _side is not null;

    public bool OverHandle => _side is null && _hover is not null;

    /// <summary>The side being dragged, else the one under the pointer.</summary>
    public (int X, int Y)? Active => _side ?? _hover;

    /// <summary>The size being dragged; null when no drag is on.</summary>
    public ResizeSpec? Drag => _side is { } side ? Spec(side, _to, _stretch, AllFrames || _modAll) : null;

    /// <summary>The editor's "All frames" toggle; the drag also applies to every frame while the <see cref="ResizeScope.Modifier"/> key is held.</summary>
    public bool AllFrames { get; set; }

    /// <summary>Raised when the dragged size changes, and with null when the drag ends or is cancelled.</summary>
    public event Action<ResizeSpec?>? DragChanged;

    /// <summary>Raised on release with the new size, when it differs from the picture's; the handler returns false when the size could not be applied.</summary>
    public event Func<ResizeSpec, bool>? Dropped;

    /// <summary>Raised when the pointer comes onto a handle (true) or leaves it (false), so the editor can say what a drag does.</summary>
    public event Action<bool>? Hovered;

    /// <summary>The side under the pointer or being dragged changed, so the cursor follows.</summary>
    public event Action? SideChanged;

    private double Half => HandleSize() + 1;

    private double Reach => Half + ReachExtra;

    private Point Centre(Rect area, int sx, int sy)
    {
        var off = Half + Gap;
        return new(sx < 0 ? area.Left - off : sx > 0 ? area.Right + off : area.Center.X, sy < 0 ? area.Top - off : sy > 0 ? area.Bottom + off : area.Center.Y);
    }

    /// <summary>The side whose handle a press at the host point grabs: -1, 0 or 1 across and down; null away from the eight handles.</summary>
    public (int X, int Y)? HandleAt(Point p)
    {
        if (!Enabled || Blocked?.Invoke() == true) return null;
        var area = picture();
        // A handle's reach crosses the edge a little; a press on a pixel of the picture always belongs to the picture.
        if (p.X > area.Left && p.X < area.Right && p.Y > area.Top && p.Y < area.Bottom) return null;
        for (var sy = -1; sy <= 1; sy++)
            for (var sx = -1; sx <= 1; sx++)
            {
                if (sx == 0 && sy == 0) continue;
                var centre = Centre(area, sx, sy);
                if (Math.Abs(p.X - centre.X) <= Reach && Math.Abs(p.Y - centre.Y) <= Reach) return (sx, sy);
            }

        return null;
    }
}
