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
    private (IImage Picture, Rect Target)? _picture;
    private readonly List<(IImage Picture, Rect Target)> _ghosts = [];
    private int _run;
    private int _signature;
    private readonly CanvasHandles _handles;
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
        Focusable = true;
        // The outer handles sit beyond the collision box handles (which reach 4 pixels past the border), so the two never share a pixel.
        _handles = new CanvasHandles(this, () => PictureRect, () => Zoom, () => (W, H)) { HandleSize = () => 4, Gap = 6, ReachExtra = 1 };
        _handles.DragChanged += spec => ResizeDragging?.Invoke(spec);
        _handles.Hovered += over => HandleHovered?.Invoke(over);
        _handles.SideChanged += () => Cursor = new Cursor(_handles.Active is { } side ? CanvasHandles.SideCursor(side) : StandardCursorType.Arrow);
        _handles.Dropped += spec => ResizeDropped?.Invoke(spec) == true;
        CanResize = sprite.Textures.Count > 0;
        Resize();
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
    }

    /// <summary>Room around the picture for the canvas handles, in screen pixels: the handle, its gap from the box handles and the room to grab it.</summary>
    public const double Pad = 26;

    private Rect PictureRect => new(Pad, Pad, W * Zoom, H * Zoom);

    private void Resize()
    {
        Width = (_sprite.Width * Zoom) + 1 + (2 * Pad);
        Height = (_sprite.Height * Zoom) + 1 + (2 * Pad);
    }

    /// <summary>The editor toggle "All frames": a handle drag then applies to every frame of the sprite.</summary>
    public bool AllFrames
    {
        get => _handles.AllFrames;
        set => _handles.AllFrames = value;
    }

    /// <summary>The canvas size being dragged (with the modifiers that were held); null when no drag is on.</summary>
    public ResizeSpec? ResizeDrag => _handles.Drag;

    /// <summary>Raised when the dragged size changes, and with null when the drag ends or is cancelled.</summary>
    public event Action<ResizeSpec?>? ResizeDragging;

    /// <summary>Raised on release with the new size; the handler returns false when it could not be applied.</summary>
    public event Func<ResizeSpec, bool>? ResizeDropped;

    /// <summary>Raised when the pointer comes onto a canvas handle (true) or leaves it (false).</summary>
    public event Action<bool>? HandleHovered;

    /// <summary>False hides the canvas handles (a sprite the file cannot resize).</summary>
    public bool CanResize
    {
        get => _handles.Enabled;
        set
        {
            _handles.Enabled = value;
            InvalidateVisual();
        }
    }

    /// <summary>The handle a press at the control point grabs, for tests.</summary>
    internal (int X, int Y)? HandleAt(Point screen) => _handles.HandleAt(screen);

    /// <summary>The size of the picture on show, in pixels, or (0, 0) while it is not decoded.</summary>
    internal (int Width, int Height) PictureSize => _picture is { } p ? ((int)p.Target.Width, (int)p.Target.Height) : (0, 0);

    /// <summary>The bitmap on show; changes whenever the picture is cut again.</summary>
    internal IImage? PictureBitmap => _picture?.Picture;

    /// <summary>The cut of the frame on show is on screen when this ends.</summary>
    public Task Ready { get; private set; } = Task.CompletedTask;

    /// <summary>The picture facts that decide what is cut: sprite size, the frames shown, their page items and the page images behind them.</summary>
    private int PictureSignature()
    {
        var hash = new HashCode();
        hash.Add(_sprite.Width);
        hash.Add(_sprite.Height);
        hash.Add(_sprite.Textures.Count);
        foreach (var frame in new[] { Frame - 1, Frame, Frame + 1 })
        {
            var item = Onion || frame == Frame ? _sprite.Textures.ElementAtOrDefault(frame)?.Texture : null;
            hash.Add(item is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item));
            if (item is null) continue;
            hash.Add((item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight, item.TargetX, item.TargetY));
            hash.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item.TexturePage?.TextureData?.Image));
        }

        return hash.ToHashCode();
    }

    /// <summary>The data changed (an edit, undo, redo, another editor): the size follows at once, the picture is cut again only when what it shows changed, the box and guides are drawn from the data.</summary>
    public void Refresh()
    {
        CanResize = _sprite.Textures.Count > 0;
        Resize();
        if (PictureSignature() == _signature) InvalidateVisual();
        else _ = ShowFrameAsync(Frame);
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

    /// <summary>Draws the frames before and after the shown one faintly under it (onion skin).</summary>
    public bool Onion
    {
        get;
        set
        {
            field = value;
            _ = ShowFrameAsync(Frame);
        }
    }

    private async Task<(IImage Picture, Rect Target)?> CutAsync(int frame)
    {
        if (_sprite.Textures.ElementAtOrDefault(frame)?.Texture is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item || await _textures.GetAsync(page) is not { } bitmap) return null;
        return (new CroppedBitmap(bitmap, new PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight)), new Rect(item.TargetX, item.TargetY, item.SourceWidth, item.SourceHeight));
    }

    public Task ShowFrameAsync(int frame) => Ready = LoadAsync(frame);

    // The picture on show stays until the new cut is ready, so a refresh does not flash an empty canvas; a newer call ends an older one that is still waiting for a page.
    private async Task LoadAsync(int frame)
    {
        var wanted = Frame = Math.Clamp(frame, 0, Math.Max(0, _sprite.Textures.Count - 1));
        var run = ++_run;
        _signature = PictureSignature();
        Resize();
        var cut = await CutAsync(wanted);
        if (run != _run) return;
        _picture = cut;
        var ghosts = new List<(IImage Picture, Rect Target)>();
        if (Onion)
            foreach (var neighbour in new[] { wanted - 1, wanted + 1 })
                if (neighbour >= 0 && neighbour < _sprite.Textures.Count && await CutAsync(neighbour) is { } ghost)
                {
                    if (run != _run) return;
                    ghosts.Add(ghost);
                }

        if (run != _run) return;
        _ghosts.Clear();
        _ghosts.AddRange(ghosts);
        InvalidateVisual();
    }

    private (int Left, int Top, int Right, int Bottom) Slice => _sprite.V3NineSlice is { Enabled: true } n ? (n.Left, n.Top, n.Right, n.Bottom) : (0, 0, 0, 0);

    private MaskBox CurrentBox => _grab == Grab.Box ? _box : StoredBox();

    private MaskBox StoredBox() => SpriteMaskCommand.BoxOf(_sprite);

    private Point ToPixel(Point screen) => new((screen.X - Pad) / Zoom, (screen.Y - Pad) / Zoom);

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
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _handles.Dragging || _grab != Grab.None) return; // a second button during a drag starts nothing
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
        else if (_handles.Begin(e, e.GetPosition(this)))
        {
            Focus(); // Escape reaches the drag
            e.Handled = true;
            return;
        }
        else
        {
            return;
        }

        e.Pointer.Capture(this);
        Focus(); // Escape reaches the drag
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pixel = ToPixel(e.GetPosition(this));
        if (_handles.Dragging)
        {
            _handles.Move(e.GetPosition(this), e.KeyModifiers);
            return;
        }

        if (_grab == Grab.None)
        {
            // The inner handles and the origin win near the border; the outer handles only start beyond them.
            var shape = HoverShape(pixel);
            if (shape == StandardCursorType.Arrow) _handles.Move(e.GetPosition(this), e.KeyModifiers);
            else _handles.ClearHover();
            if (_handles.Active is null) Cursor = new Cursor(shape);
            return;
        }

        Drag(pixel);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _handles.ClearHover();
    }

    // Alt+Tab or another window taking the pointer mid-drag ends the drag without a resize.
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _handles.Cancel();
        CancelGrab();
    }

    // The origin, box and guide drags work on copies of the values, so letting go of the grab keeps what the sprite holds.
    private bool CancelGrab()
    {
        if (_grab == Grab.None) return false;
        _grab = Grab.None;
        InvalidateVisual();
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _handles.Modifiers(e.KeyModifiers);
        if (e.Key != Key.Escape || !(_handles.Cancel() | CancelGrab())) return;
        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _handles.Modifiers(e.KeyModifiers);
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
        if (_handles.Dragging)
        {
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            _handles.End(e.KeyModifiers); // lets go of the pointer before the drop runs, so a throwing drop cannot leave it captured
            e.Pointer.Capture(null);
            return;
        }

        var grab = _grab;
        if (grab == Grab.None || e.InitialPressMouseButton != MouseButton.Left) return; // another button's release does not end the drag
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
        var transform = context.PushTransform(Matrix.CreateTranslation(Pad, Pad));
        var area = new Rect(0, 0, W * Zoom, H * Zoom);
        Chequer.Draw(context, area, 8);
        using (context.PushOpacity(0.3))
            foreach (var (ghost, place) in _ghosts)
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None })) context.DrawImage(ghost, new Rect(ghost.Size), new Rect(place.X * Zoom, place.Y * Zoom, place.Width * Zoom, place.Height * Zoom));
        if (_picture is { } shown)
        {
            var target = new Rect(shown.Target.X * Zoom, shown.Target.Y * Zoom, shown.Target.Width * Zoom, shown.Target.Height * Zoom);
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None })) context.DrawImage(shown.Picture, new Rect(shown.Picture.Size), target);
        }

        if (ShowBox) DrawBox(context, CurrentBox);
        if (_sprite.V3NineSlice is { Enabled: true } && ShowSlice) DrawSlice(context, _grab == Grab.Guide ? _slice : Slice);
        var (ox, oy) = _grab == Grab.Origin ? _origin : (_sprite.OriginX, _sprite.OriginY);
        var at = new Point((ox + 0.5) * Zoom, (oy + 0.5) * Zoom);
        foreach (var (pen, reach) in new[] { (new Pen(Brushes.Black, 3), 7.0), (new Pen(CanvasTheme.Handle, 1), 6.0) })
        {
            context.DrawLine(pen, at - new Vector(reach, 0), at + new Vector(reach, 0));
            context.DrawLine(pen, at - new Vector(0, reach), at + new Vector(0, reach));
        }

        transform.Dispose();
        _handles.Draw(context); // outside the picture, in control coordinates
    }

    private void DrawBox(DrawingContext context, MaskBox box)
    {
        var rect = new Rect(box.Left * Zoom, box.Top * Zoom, box.Width * Zoom, box.Height * Zoom);
        context.DrawRectangle(CanvasTheme.Soft(CanvasTheme.Point, 0.15), new Pen(CanvasTheme.Point, 2), rect);
        foreach (var corner in new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight, new Point(rect.Center.X, rect.Top), new Point(rect.Center.X, rect.Bottom), new Point(rect.Left, rect.Center.Y), new Point(rect.Right, rect.Center.Y) })
            context.DrawRectangle(CanvasTheme.Handle, new Pen(Brushes.Black, 2), new Rect(corner.X - 3, corner.Y - 3, 6, 6));
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
