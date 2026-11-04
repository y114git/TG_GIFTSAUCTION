using Avalonia;
using Avalonia.Media;

namespace G3MStudio.Rooms;

public sealed partial class RoomView
{
    /// <summary>Draws the whole room at 100% (visible layers only, no selection or grid) and writes it as a PNG. Rooms over 16384 pixels on a side are refused.</summary>
    public void SavePng(Stream stream)
    {
        if (Scene is not { } scene) throw new InvalidOperationException("The room has nothing to draw.");
        if (scene.Width > 16384 || scene.Height > 16384) throw new InvalidOperationException("The room is larger than 16384 pixels on a side.");
        using var target = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(Math.Max(1, (int)scene.Width), Math.Max(1, (int)scene.Height)));
        using (var context = target.CreateDrawingContext(true))
        {
            context.FillRectangle(scene.FillColor is { } c ? new SolidColorBrush(Color.FromRgb((byte)c, (byte)(c >> 8), (byte)(c >> 16))) : Brushes.Black, new Rect(0, 0, scene.Width, scene.Height));
            foreach (var item in scene.Items) DrawItem(context, item, item.Dest);
        }

        target.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static void DrawItem(DrawingContext context, RoomItem item, Rect dest)
    {
        if (item.Flags == 0)
        {
            context.DrawImage(item.Page, item.Source, dest);
            return;
        }

        var centre = dest.Center;
        var matrix = Matrix.CreateTranslation(-centre.X, -centre.Y);
        if ((item.Flags & 1) != 0) matrix *= Matrix.CreateScale(-1, 1);
        if ((item.Flags & 2) != 0) matrix *= Matrix.CreateScale(1, -1);
        if ((item.Flags & 4) != 0) matrix *= Matrix.CreateRotation(Math.PI / 2);
        using var transform = context.PushTransform(matrix * Matrix.CreateTranslation(centre.X, centre.Y));
        context.DrawImage(item.Page, item.Source, dest);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        DrawnItems = 0;
        if (Scene is null) return;
        var view = new Rect(_offset, new Size(Bounds.Width / _zoom, Bounds.Height / _zoom));
        using var clip = context.PushClip(new Rect(Bounds.Size));
        using var move = context.PushTransform(Matrix.CreateTranslation(-_offset.X, -_offset.Y) * Matrix.CreateScale(_zoom, _zoom));
        if (Scene.FillColor is { } fill) context.FillRectangle(new SolidColorBrush(Color.FromRgb((byte)fill, (byte)(fill >> 8), (byte)(fill >> 16))), new Rect(0, 0, Scene.Width, Scene.Height));
        var outline = new Pen(G3MStudio.Theme.CanvasTheme.Line, 1 / _zoom);
        var delta = _moving && _selection.Count > 0 ? MoveDelta() : default;
        foreach (var item in Scene.Items)
        {
            var selected = item.Element is not null && _selection.Contains(item.Element);
            var dest = selected && _moving ? item.Dest.Translate(delta) : item.Dest;
            if (!dest.Intersects(view)) continue;
            DrawItem(context, item, dest);
            if (selected) context.DrawRectangle(null, outline, dest);
            DrawnItems++;
        }

        var markerPen = new Pen(G3MStudio.Theme.CanvasTheme.Point, 1 / _zoom) { DashStyle = DashStyle.Dash };
        foreach (var marker in Scene.Markers)
        {
            var selected = _selection.Contains(marker.Element);
            var bounds = selected && _moving ? marker.Bounds.Translate(delta) : marker.Bounds;
            if (!bounds.Intersects(view)) continue;
            context.DrawRectangle(null, selected ? outline : markerPen, bounds);
            var text = new FormattedText(marker.Label, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 11 / _zoom, Brushes.White);
            context.DrawText(text, bounds.TopLeft);
        }

        if (_paintSource is not null)
            foreach (var place in _paintPoints)
                context.DrawEllipse(G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Line, 0.5), null, place, 3 / _zoom, 3 / _zoom);

        var guidePen = new Pen(G3MStudio.Theme.CanvasTheme.Guide, 1 / _zoom);
        foreach (var x in VerticalGuides) context.DrawLine(guidePen, new Point(x, view.Top), new Point(x, view.Bottom));
        foreach (var y in HorizontalGuides) context.DrawLine(guidePen, new Point(view.Left, y), new Point(view.Right, y));
        if (_guidePreview is { } preview)
            context.DrawLine(guidePen, preview.Horizontal ? new Point(view.Left, preview.Position) : new Point(preview.Position, view.Top), preview.Horizontal ? new Point(view.Right, preview.Position) : new Point(preview.Position, view.Bottom));

        if (ShowGrid) DrawGrid(context, view);
        if (ShowViews)
        {
            var viewPen = new Pen(G3MStudio.Theme.CanvasTheme.Point, 2 / _zoom, DashStyle.Dash);
            foreach (var camera in Views) context.DrawRectangle(null, viewPen, camera);
        }

        if (_stroke is not null && TileLayer is { TilesData.Background: { } set } tileLayer)
            foreach (var (x, y) in _stroke)
                context.DrawRectangle(G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Line, 0.35), null, new Rect(tileLayer.XOffset + x * set.GMS2TileWidth, tileLayer.YOffset + y * set.GMS2TileHeight, set.GMS2TileWidth, set.GMS2TileHeight));
        if (_marquee is { } marquee) context.DrawRectangle(null, outline, marquee);
    }

    private void DrawGrid(DrawingContext context, Rect view)
    {
        // Lines closer than 3 screen pixels are only noise, and each one costs a draw call.
        if (GridWidth * _zoom < 3 || GridHeight * _zoom < 3) return;
        var pen = new Pen(new SolidColorBrush(Colors.White, 0.25), 1 / _zoom);
        var right = Math.Min(view.Right, Scene?.Width ?? view.Right);
        var bottom = Math.Min(view.Bottom, Scene?.Height ?? view.Bottom);
        for (var x = Math.Max(0, Math.Floor(view.Left / GridWidth) * GridWidth); x <= right; x += GridWidth)
            context.DrawLine(pen, new Point(x, Math.Max(0, view.Top)), new Point(x, bottom));
        for (var y = Math.Max(0, Math.Floor(view.Top / GridHeight) * GridHeight); y <= bottom; y += GridHeight)
            context.DrawLine(pen, new Point(Math.Max(0, view.Left), y), new Point(right, y));
    }
}
