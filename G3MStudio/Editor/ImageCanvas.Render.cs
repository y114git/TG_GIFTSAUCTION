using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MStudio.Core.Workspace;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

public sealed partial class ImageCanvas
{
    private Geometry? _outline;

    private Rect PictureRect => new(_origin.X, _origin.Y, Picture.Width * Zoom, Picture.Height * Zoom);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Backdrop ?? CanvasTheme.Bg, new Rect(Bounds.Size));
        var area = PictureRect;
        if (TilePreview) DrawTiles(context, area);
        Chequer.Draw(context, area, Math.Max(Zoom, 8), ChequerLight, ChequerDark);
        DrawOnion(context, area);
        context.DrawImage(_bitmap, new Rect(0, 0, Picture.Width, Picture.Height), area);
        if (ShowGrid) DrawGrid(context, area);
        DrawGuides(context, area);
        DrawSelection(context);
        DrawPreview(context);
    }

    private void DrawTiles(DrawingContext context, Rect area)
    {
        using (context.PushOpacity(0.45))
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0) context.DrawImage(_bitmap, new Rect(0, 0, Picture.Width, Picture.Height), new Rect(area.X + dx * area.Width, area.Y + dy * area.Height, area.Width, area.Height));
    }

    private void DrawOnion(DrawingContext context, Rect area)
    {
        foreach (var frame in Onion)
        {
            if (!_onion.TryGetValue(frame, out var bitmap)) _onion[frame] = bitmap = Bitmap(frame);
            using (context.PushOpacity(OnionOpacity)) context.DrawImage(bitmap, new Rect(0, 0, frame.Width, frame.Height), new Rect(area.X, area.Y, frame.Width * Zoom, frame.Height * Zoom));
        }
    }

    private static WriteableBitmap Bitmap(PixelBuffer picture)
    {
        var bitmap = NewBitmap(picture);
        using var frame = bitmap.Lock();
        for (var row = 0; row < picture.Height; row++) System.Runtime.InteropServices.Marshal.Copy(picture.Data, row * picture.Width * 4, frame.Address + row * frame.RowBytes, picture.Width * 4);
        return bitmap;
    }

    private void DrawGrid(DrawingContext context, Rect area)
    {
        var visible = area.Intersect(new Rect(Bounds.Size));
        if (visible.Width <= 0 || visible.Height <= 0) return;
        var (x0, x1) = (Math.Max(0, (int)Math.Floor((visible.Left - area.X) / Zoom)), Math.Min(Picture.Width, (int)Math.Ceiling((visible.Right - area.X) / Zoom)));
        var (y0, y1) = (Math.Max(0, (int)Math.Floor((visible.Top - area.Y) / Zoom)), Math.Min(Picture.Height, (int)Math.Ceiling((visible.Bottom - area.Y) / Zoom)));
        var guide = GridBrush ?? CanvasTheme.Guide;
        var fine = new Pen(CanvasTheme.Soft(guide, 0.25), 1);
        var coarse = new Pen(CanvasTheme.Soft(guide, 0.7), 1);
        for (var x = x0; x <= x1; x++)
        {
            var big = GridCell > 0 && x % GridCell == 0;
            if (Zoom >= 6 || big) context.DrawLine(big ? coarse : fine, new Point(area.X + x * Zoom, visible.Top), new Point(area.X + x * Zoom, visible.Bottom));
        }

        for (var y = y0; y <= y1; y++)
        {
            var big = GridCell > 0 && y % GridCell == 0;
            if (Zoom >= 6 || big) context.DrawLine(big ? coarse : fine, new Point(visible.Left, area.Y + y * Zoom), new Point(visible.Right, area.Y + y * Zoom));
        }
    }

    // The mirror axes of the symmetry modes.
    private void DrawGuides(DrawingContext context, Rect area)
    {
        var pen = new Pen(CanvasTheme.Point, 1, new DashStyle([4, 4], 0));
        if (Symmetry.HasFlag(SymmetryMode.Horizontal)) context.DrawLine(pen, new Point(area.Center.X, area.Top), new Point(area.Center.X, area.Bottom));
        if (Symmetry.HasFlag(SymmetryMode.Vertical)) context.DrawLine(pen, new Point(area.Left, area.Center.Y), new Point(area.Right, area.Center.Y));
    }

    private void DrawSelection(DrawingContext context)
    {
        if (State.Selection is not { } selection) return;
        _outline ??= Outline(selection);
        using var _ = context.PushTransform(Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(_origin.X, _origin.Y));
        context.DrawGeometry(null, new Pen(Brushes.Black, 3 / Zoom), _outline);
        context.DrawGeometry(null, new Pen(Brushes.White, 1.5 / Zoom, new DashStyle([3, 3], 0)), _outline);
    }

    /// <summary>The border of the selected pixels as lines in picture coordinates (one rectangle when the selection is a rectangle).</summary>
    private static StreamGeometry Outline(PixelSelection selection)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        if (selection.Bounds() is not { } box) return geometry;
        if (selection.Mask.Count(m => m != 0) == box.Width * box.Height)
        {
            context.BeginFigure(new Point(box.X, box.Y), false);
            foreach (var corner in new[] { new Point(box.X + box.Width, box.Y), new Point(box.X + box.Width, box.Y + box.Height), new Point(box.X, box.Y + box.Height), new Point(box.X, box.Y) }) context.LineTo(corner);
            context.EndFigure(false);
            return geometry;
        }

        for (var y = box.Y; y < box.Y + box.Height; y++)
            for (var x = box.X; x < box.X + box.Width; x++)
            {
                if (!selection.Contains(x, y)) continue;
                if (!selection.Contains(x, y - 1)) Segment(context, x, y, x + 1, y);
                if (!selection.Contains(x, y + 1)) Segment(context, x, y + 1, x + 1, y + 1);
                if (!selection.Contains(x - 1, y)) Segment(context, x, y, x, y + 1);
                if (!selection.Contains(x + 1, y)) Segment(context, x + 1, y, x + 1, y + 1);
            }

        return geometry;
    }

    private static void Segment(StreamGeometryContext context, double x0, double y0, double x1, double y1)
    {
        context.BeginFigure(new Point(x0, y0), false);
        context.LineTo(new Point(x1, y1));
        context.EndFigure(false);
    }

    // What the tool in hand will do: the dragged shape, the points of a polygon, the move, and the brush under the pointer.
    private void DrawPreview(DrawingContext context)
    {
        var pen = new Pen(CanvasTheme.Point, 2);
        var line = new Pen(CanvasTheme.Line, 1);
        if (_drawing && Tool is ImageTool.Line)
            context.DrawLine(pen, Centre(_start), Centre(_hoverCell));
        else if (_drawing && Tool is ImageTool.Rectangle or ImageTool.Ellipse or ImageTool.Select)
            DrawBox(context, pen, Tool == ImageTool.Select ? (_start, _hoverCell) : Corners(_start, _hoverCell), Tool == ImageTool.Ellipse);
        else if (_drawing && Tool == ImageTool.Move) DrawMove(context, pen);
        else if (Tool is ImageTool.Lasso && _drawing) DrawPath(context, pen, _points);
        else if (Collecting) DrawPath(context, pen, [.. _points, _hoverCell]);
        else if (Picture.Contains(_hoverCell.X, _hoverCell.Y) && Tool is ImageTool.Pencil or ImageTool.Eraser)
        {
            var low = (BrushSize - 1) / 2;
            context.DrawRectangle(null, line, new Rect(ToScreen(_hoverCell.X - low, _hoverCell.Y - low), new Size(BrushSize * Zoom, BrushSize * Zoom)));
        }
        else if (Picture.Contains(_hoverCell.X, _hoverCell.Y)) context.DrawRectangle(null, line, new Rect(ToScreen(_hoverCell.X, _hoverCell.Y), new Size(Zoom, Zoom)));
    }

    private Point Centre((int X, int Y) cell) => ToScreen(cell.X + 0.5, cell.Y + 0.5);

    private void DrawBox(DrawingContext context, Pen pen, ((int X, int Y) A, (int X, int Y) B) box, bool ellipse)
    {
        var rect = new Rect(ToScreen(Math.Min(box.A.X, box.B.X), Math.Min(box.A.Y, box.B.Y)), new Size((Math.Abs(box.B.X - box.A.X) + 1) * Zoom, (Math.Abs(box.B.Y - box.A.Y) + 1) * Zoom));
        if (ellipse) context.DrawEllipse(null, pen, rect.Center, rect.Width / 2, rect.Height / 2);
        else context.DrawRectangle(null, pen, rect);
    }

    private void DrawMove(DrawingContext context, Pen pen)
    {
        var bounds = State.Selection?.Bounds() ?? new Box(0, 0, Picture.Width, Picture.Height);
        var (dx, dy) = (_hoverCell.X - _start.X, _hoverCell.Y - _start.Y);
        context.DrawRectangle(null, pen, new Rect(ToScreen(bounds.X + dx, bounds.Y + dy), new Size(bounds.Width * Zoom, bounds.Height * Zoom)));
    }

    private void DrawPath(DrawingContext context, Pen pen, List<(int X, int Y)> points)
    {
        for (var i = 1; i < points.Count; i++) context.DrawLine(pen, Centre(points[i - 1]), Centre(points[i]));
        foreach (var point in points) context.DrawEllipse(CanvasTheme.Point, null, Centre(point), HandleSize, HandleSize);
    }
}
