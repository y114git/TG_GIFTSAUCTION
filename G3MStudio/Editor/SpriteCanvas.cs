using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Rooms;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// One frame of a sprite at a whole-number zoom with the three things a modder drags on it: the origin cross, the collision box with eight handles,
/// and the four nine-slice guides. Dragging only reports the new value when the button is released; the panel turns it into one undo step.
/// </summary>
public sealed class SpriteCanvas : Control
{
    private enum Grab { None, Origin, Box, Guide }

    private readonly GameMakerSprite _sprite;
    private readonly TextureBitmaps _textures;
    private IImage? _picture;
    private Grab _grab;
    private BoxHandle _handle;
    private string _side = "";
    private Point _from;
    private MaskBox _box;
    private (int X, int Y) _origin;
    private (int Left, int Top, int Right, int Bottom) _slice;

    public SpriteCanvas(GameMakerSprite sprite, TextureBitmaps textures)
    {
        (_sprite, _textures) = (sprite, textures);
        Name = "SpriteCanvas";
        Zoom = Math.Clamp(256 / (int)Math.Max(1, Math.Max(sprite.Width, sprite.Height)), 1, 16);
        Resize();
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
    }

    private void Resize()
    {
        Width = (_sprite.Width * Zoom) + 1;
        Height = (_sprite.Height * Zoom) + 1;
    }

    /// <summary>Whole-number magnification, 1 to 32.</summary>
    public int Zoom
    {
        get;
        set
        {
            field = Math.Clamp(value, 1, 32);
            Resize();
            InvalidateVisual();
        }
    }

    public int Frame { get; private set; }

    /// <summary>Show the collision box and its handles.</summary>
    public bool ShowBox { get; set; } = true;

    /// <summary>Show the nine-slice guides (drawn and draggable only when the sprite's nine-slice is on).</summary>
    public bool ShowSlice { get; set; } = true;

    public event Action<int, int>? OriginChanged;

    public event Action<MaskBox>? BoxChanged;

    public event Action<int, int, int, int>? SliceChanged;

    public async Task ShowFrameAsync(int frame)
    {
        Frame = Math.Clamp(frame, 0, Math.Max(0, _sprite.Textures.Count - 1));
        _picture = null;
        if (_sprite.Textures.ElementAtOrDefault(Frame)?.Texture is { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item && await _textures.GetAsync(page) is { } bitmap)
            _picture = new CroppedBitmap(bitmap, new PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight));
        InvalidateVisual();
    }

    private (int Left, int Top, int Right, int Bottom) Slice => _sprite.V3NineSlice is { Enabled: true } n ? (n.Left, n.Top, n.Right, n.Bottom) : (0, 0, 0, 0);

    private MaskBox CurrentBox => _grab == Grab.Box ? _box : StoredBox();

    private MaskBox StoredBox() => SpriteMaskCommand.BoxOf(_sprite);

    private Point ToPixel(Point screen) => new(screen.X / Zoom, screen.Y / Zoom);

    private int W => (int)_sprite.Width;

    private int H => (int)_sprite.Height;

    private string? GuideAt(Point pixel)
    {
        if (_sprite.V3NineSlice is not { Enabled: true }) return null;
        var (s, reach) = (Slice, 4.0 / Zoom);
        if (Math.Abs(pixel.X - s.Left) <= reach) return "left";
        if (Math.Abs(pixel.X - (W - s.Right)) <= reach) return "right";
        if (Math.Abs(pixel.Y - s.Top) <= reach) return "top";
        return Math.Abs(pixel.Y - (H - s.Bottom)) <= reach ? "bottom" : null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var pixel = ToPixel(e.GetPosition(this));
        _from = pixel;
        if (Math.Abs(pixel.X - (_sprite.OriginX + 0.5)) <= 6.0 / Zoom && Math.Abs(pixel.Y - (_sprite.OriginY + 0.5)) <= 6.0 / Zoom)
        {
            (_grab, _origin) = (Grab.Origin, (_sprite.OriginX, _sprite.OriginY));
        }
        else if (GuideAt(pixel) is { } side)
        {
            (_grab, _side, _slice) = (Grab.Guide, side, Slice);
        }
        else if (ShowBox && SpriteMask.HandleAt(StoredBox(), pixel.X, pixel.Y, 4.0 / Zoom) is { } handle)
        {
            (_grab, _handle, _box) = (Grab.Box, handle, StoredBox());
        }
        else
        {
            return;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pixel = ToPixel(e.GetPosition(this));
        if (_grab == Grab.None)
        {
            Cursor = new Cursor(HoverShape(pixel));
            return;
        }

        Drag(pixel);
        InvalidateVisual();
    }

    private StandardCursorType HoverShape(Point pixel)
    {
        if (Math.Abs(pixel.X - (_sprite.OriginX + 0.5)) <= 6.0 / Zoom && Math.Abs(pixel.Y - (_sprite.OriginY + 0.5)) <= 6.0 / Zoom) return StandardCursorType.SizeAll;
        if (GuideAt(pixel) is { } side) return side is "left" or "right" ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth;
        return ShowBox && SpriteMask.HandleAt(StoredBox(), pixel.X, pixel.Y, 4.0 / Zoom) is not null ? StandardCursorType.Hand : StandardCursorType.Arrow;
    }

    private void Drag(Point pixel)
    {
        int dx = (int)Math.Round(pixel.X - _from.X), dy = (int)Math.Round(pixel.Y - _from.Y);
        switch (_grab)
        {
            case Grab.Origin:
                _origin = ((int)Math.Floor(pixel.X), (int)Math.Floor(pixel.Y));
                break;
            case Grab.Box:
                _box = SpriteMask.Drag(StoredBox(), _handle, dx, dy, W, H);
                break;
            case Grab.Guide:
                var at = (int)Math.Round(_side is "left" or "right" ? pixel.X : pixel.Y);
                _slice = SpriteMask.MoveGuide(Slice, _side, at, W, H);
                break;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var grab = _grab;
        if (grab == Grab.None) return;
        Drag(ToPixel(e.GetPosition(this)));
        _grab = Grab.None;
        e.Pointer.Capture(null);
        switch (grab)
        {
            case Grab.Origin when _origin != (_sprite.OriginX, _sprite.OriginY):
                OriginChanged?.Invoke(_origin.X, _origin.Y);
                break;
            case Grab.Box when _box != StoredBox():
                BoxChanged?.Invoke(_box);
                break;
            case Grab.Guide when _slice != Slice:
                SliceChanged?.Invoke(_slice.Left, _slice.Top, _slice.Right, _slice.Bottom);
                break;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var area = new Rect(0, 0, W * Zoom, H * Zoom);
        Chequer.Draw(context, area, 8);
        if (_picture is { } picture && _sprite.Textures.ElementAtOrDefault(Frame)?.Texture is { } item)
        {
            var target = new Rect(item.TargetX * Zoom, item.TargetY * Zoom, item.SourceWidth * Zoom, item.SourceHeight * Zoom);
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None })) context.DrawImage(picture, new Rect(picture.Size), target);
        }

        if (ShowBox) DrawBox(context, CurrentBox);
        if (_sprite.V3NineSlice is { Enabled: true } && ShowSlice) DrawSlice(context, _grab == Grab.Guide ? _slice : Slice);
        var (ox, oy) = _grab == Grab.Origin ? _origin : (_sprite.OriginX, _sprite.OriginY);
        var at = new Point((ox + 0.5) * Zoom, (oy + 0.5) * Zoom);
        foreach (var (pen, reach) in new[] { (new Pen(Brushes.Black, 3), 7.0), (new Pen(Brushes.White, 1), 6.0) })
        {
            context.DrawLine(pen, at - new Vector(reach, 0), at + new Vector(reach, 0));
            context.DrawLine(pen, at - new Vector(0, reach), at + new Vector(0, reach));
        }
    }

    private void DrawBox(DrawingContext context, MaskBox box)
    {
        var rect = new Rect(box.Left * Zoom, box.Top * Zoom, box.Width * Zoom, box.Height * Zoom);
        context.DrawRectangle(CanvasTheme.Soft(CanvasTheme.Point, 0.15), new Pen(CanvasTheme.Point, 2), rect);
        foreach (var corner in new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight, new Point(rect.Center.X, rect.Top), new Point(rect.Center.X, rect.Bottom), new Point(rect.Left, rect.Center.Y), new Point(rect.Right, rect.Center.Y) })
            context.DrawRectangle(Brushes.White, new Pen(Brushes.Black, 2), new Rect(corner.X - 3, corner.Y - 3, 6, 6));
    }

    private void DrawSlice(DrawingContext context, (int Left, int Top, int Right, int Bottom) s)
    {
        var pen = new Pen(CanvasTheme.Guide, 2, DashStyle.Dash);
        double l = s.Left * Zoom, r = (W - s.Right) * Zoom, t = s.Top * Zoom, b = (H - s.Bottom) * Zoom;
        context.DrawLine(pen, new Point(l, 0), new Point(l, H * Zoom));
        context.DrawLine(pen, new Point(r, 0), new Point(r, H * Zoom));
        context.DrawLine(pen, new Point(0, t), new Point(W * Zoom, t));
        context.DrawLine(pen, new Point(0, b), new Point(W * Zoom, b));
    }
}
