using Avalonia;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

public sealed partial class RoomView
{
    public const int MaxPngSide = 16384;

    /// <summary>The refusal for a room over 16384 pixels on a side (the owner may export it reduced instead); null when it fits.</summary>
    public RiskyRefusalException? TooLargeForPng => Scene is { } scene && (scene.Width > MaxPngSide || scene.Height > MaxPngSide)
        ? new RiskyRefusalException("risky.roomPngTitle", "risky.roomPng", $"The room is {scene.Width:0}x{scene.Height:0} pixels; a side may be at most {MaxPngSide}.")
        : null;

    /// <summary>
    /// Draws the whole room at 100% (visible layers only, no selection or grid) and writes it as a PNG. A room over 16384 pixels on a side is refused,
    /// unless <paramref name="reduce"/> is set: then it is drawn smaller so that its longer side is 16384.
    /// </summary>
    public void SavePng(Stream stream, bool reduce = false)
    {
        if (Scene is not { } scene) throw new InvalidOperationException("The room has nothing to draw.");
        if (TooLargeForPng is { } refusal && !reduce) throw refusal;
        var scale = Math.Min(1, MaxPngSide / Math.Max(scene.Width, scene.Height));
        using var target = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(Math.Max(1, (int)(scene.Width * scale)), Math.Max(1, (int)(scene.Height * scale))));
        using (var context = target.CreateDrawingContext(true))
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
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
            using (selected && _transform is { } matrix && item.Element is GameObject ? context.PushTransform(matrix) : default)
            {
                DrawItem(context, item, dest);
                if (selected) context.DrawRectangle(null, outline, dest);
            }

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

        DrawTileOverlays(context);
        DrawHandles(context);
        if (_marquee is { } marquee) context.DrawRectangle(null, outline, marquee);
    }

    private void DrawTileOverlays(DrawingContext context)
    {
        if (TileLayer is not { TilesData.Background: { } set } tileLayer) return;
        Rect Cell(int x, int y) => new(tileLayer.XOffset + x * set.GMS2TileWidth, tileLayer.YOffset + y * set.GMS2TileHeight, set.GMS2TileWidth, set.GMS2TileHeight);
        var soft = G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Line, 0.35);
        if (_stroke is not null)
            foreach (var (x, y) in Brushed(_stroke)) context.DrawRectangle(soft, null, Cell(x, y));
        if (TileSelection is { } selection)
            context.DrawRectangle(null, new Pen(G3MStudio.Theme.CanvasTheme.Line, 2 / _zoom, DashStyle.Dash), Cell(selection.X, selection.Y).Union(Cell(selection.X + selection.Width - 1, selection.Y + selection.Height - 1)));
        if (Floating is not { } block) return;
        for (var y = 0; y < block.Height; y++)
            for (var x = 0; x < block.Width; x++)
                if (block[x, y] != 0) context.DrawRectangle(soft, null, Cell(_floatAt.X + x, _floatAt.Y + y));
        context.DrawRectangle(null, new Pen(G3MStudio.Theme.CanvasTheme.Line, 2 / _zoom), Cell(_floatAt.X, _floatAt.Y).Union(Cell(_floatAt.X + block.Width - 1, _floatAt.Y + block.Height - 1)));
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
