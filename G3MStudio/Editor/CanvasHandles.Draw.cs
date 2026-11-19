using System.Globalization;
using Avalonia;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

internal sealed partial class CanvasHandles
{
    /// <summary>The eight handles outside the picture's edge, and while dragging the new canvas outline with its size.</summary>
    public void Draw(DrawingContext context)
    {
        var area = picture();
        var zoomNow = zoom();
        if (Drag is { } spec)
        {
            var (w, h) = size();
            var (dx, dy) = ((spec.Width - w) * spec.AnchorX / 2, (spec.Height - h) * spec.AnchorY / 2);
            var outline = new Rect(area.X - dx * zoomNow, area.Y - dy * zoomNow, spec.Width * zoomNow, spec.Height * zoomNow);
            // Dashed: only the canvas changes. Solid: the picture is stretched with it.
            DrawOutline(context, outline, new Pen(Brushes.Black, 3));
            DrawOutline(context, outline, new Pen(CanvasTheme.Point, 1.5, spec.Scale ? null : new DashStyle([4, 3], 0)));
            DrawLabel(context, outline, spec);
            return;
        }

        if (!Enabled || Blocked?.Invoke() == true) return;
        foreach (var (sx, sy) in Sides)
        {
            var centre = Centre(area, sx, sy);
            context.DrawRectangle(CanvasTheme.Handle, new Pen(Brushes.Black, 1), new Rect(centre.X - Half, centre.Y - Half, Half * 2, Half * 2));
        }
    }

    // Only the part of each edge on the control is drawn: a size dragged to the limit at a high zoom is far larger than any graphics card can draw as one shape.
    private void DrawOutline(DrawingContext context, Rect outline, Pen pen)
    {
        var view = new Rect(host.Bounds.Size).Inflate(8);
        foreach (var y in new[] { outline.Top, outline.Bottom })
            if (y >= view.Top && y <= view.Bottom && Math.Max(outline.Left, view.Left) <= Math.Min(outline.Right, view.Right))
                context.DrawLine(pen, new Point(Math.Max(outline.Left, view.Left), y), new Point(Math.Min(outline.Right, view.Right), y));
        foreach (var x in new[] { outline.Left, outline.Right })
            if (x >= view.Left && x <= view.Right && Math.Max(outline.Top, view.Top) <= Math.Min(outline.Bottom, view.Bottom))
                context.DrawLine(pen, new Point(x, Math.Max(outline.Top, view.Top)), new Point(x, Math.Min(outline.Bottom, view.Bottom)));
    }

    private static readonly (int, int)[] Sides = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];

    // The live size, inside the outline's top left corner and kept on the control so it is readable whichever way the drag grows.
    private static void DrawLabel(DrawingContext context, Rect outline, ResizeSpec spec)
    {
        var text = Loc.Current.Format(spec.Scale ? "image.dragStretch" : "image.dragSize", ("w", spec.Width), ("h", spec.Height));
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.White);
        var box = new Rect(Math.Max(0, outline.X) + 4, Math.Max(0, outline.Y) + 4, formatted.Width + 8, formatted.Height + 4);
        context.DrawRectangle(new SolidColorBrush(Colors.Black, 0.75), null, box, 3, 3);
        context.DrawText(formatted, box.TopLeft + new Vector(4, 2));
    }
}
