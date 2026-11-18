using Avalonia;
using Avalonia.Input;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

internal sealed partial class CanvasHandles
{
    /// <summary>The new size as a spec: the far side is the anchor. A stretch scales the pixels (the Resize dialog's remembered method and box choice), else only the canvas changes.</summary>
    internal static ResizeSpec Spec((int X, int Y) side, (int Width, int Height) size, bool stretch, bool allFrames = false)
    {
        var (ax, ay) = (side.X < 0 ? 2 : 0, side.Y < 0 ? 2 : 0);
        if (!stretch) return new(false, size.Width, size.Height, ax, ay) { AllFrames = allFrames };
        var choices = ResizeChoices.Current;
        return new(true, size.Width, size.Height, ax, ay) { Smooth = choices.Smooth, ScaleBox = choices.Box, AllFrames = allFrames };
    }

    /// <summary>
    /// The canvas size after dragging side <paramref name="side"/> by <paramref name="delta"/> picture pixels: whole pixels, 1 to <see cref="ResizeSpec.MaxSide"/>.
    /// With <paramref name="keepAspect"/> a corner follows the side that changed most, and an edge drag takes the other side along.
    /// </summary>
    internal static (int Width, int Height) DragSize((int Width, int Height) size, (int X, int Y) side, Vector delta, bool keepAspect)
    {
        static int Side(double value) => Math.Clamp((int)Math.Round(value), 1, ResizeSpec.MaxSide);
        var w = side.X == 0 ? size.Width : Side(size.Width + side.X * delta.X);
        var h = side.Y == 0 ? size.Height : Side(size.Height + side.Y * delta.Y);
        if (!keepAspect) return (w, h);
        var (fx, fy) = ((double)w / size.Width, (double)h / size.Height);
        var f = side.X == 0 ? fy : side.Y == 0 ? fx : Math.Abs(fx - 1) >= Math.Abs(fy - 1) ? fx : fy;
        f = Math.Min(f, (double)ResizeSpec.MaxSide / Math.Max(size.Width, size.Height)); // the longer side stops at the limit, so the aspect holds there
        return (Side(size.Width * f), Side(size.Height * f));
    }

    internal static StandardCursorType SideCursor((int X, int Y) side) => side switch
    {
        (0, _) => StandardCursorType.SizeNorthSouth,
        (_, 0) => StandardCursorType.SizeWestEast,
        (-1, -1) => StandardCursorType.TopLeftCorner,
        (1, 1) => StandardCursorType.BottomRightCorner,
        (1, -1) => StandardCursorType.TopRightCorner,
        _ => StandardCursorType.BottomLeftCorner,
    };
}
