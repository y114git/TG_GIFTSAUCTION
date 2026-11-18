using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

public sealed partial class ImageCanvas
{
    // A drag of a rectangle or ellipse shows the real pixels: the same ops the release runs, drawn on a clear copy of the picture's size that is laid over the picture.
    // A smooth shape copies the whole picture for its brush, so a very large picture keeps the plain box instead of copying it at every move.
    private const int SmoothPreviewLimit = 4_000_000;

    private PixelBuffer? _shapeBuffer;
    private WriteableBitmap? _shapeBitmap;
    private Box? _shapeDirty;
    private (((int X, int Y) A, (int X, int Y) B) Box, ShapeStyle Style, int Size, bool Smooth, SymmetryMode Mirror, Rgba Outline, Rgba Fill, ImageTool Tool, PixelBuffer Picture, PixelSelection? Selection)? _shapeKey;

    /// <summary>The ops a rectangle or ellipse drag runs on release; the preview runs the same ones.</summary>
    internal ImageOp[] ShapeOps(((int X, int Y) A, (int X, int Y) B) box)
    {
        var (outline, fill) = StyleColours();
        var kind = Tool == ImageTool.Rectangle ? ImageOps.Rect : ImageOps.Ellipse;
        return [.. Core.Workspace.Symmetry.Copies([box.A, box.B], Picture.Width, Picture.Height, Symmetry).Select(c => Smooth
            ? new ImageOp(ImageOps.Smooth, kind, ImageOp.Format(outline), ImageOp.Format(fill), ImageOp.Format(BrushSize), ImageOp.Format(c))
            : new ImageOp(kind, ImageOp.Format(outline), ImageOp.Format(fill), ImageOp.Format(BrushSize), ImageOp.Format(c)))];
    }

    /// <summary>The pixels the dragged shape would leave on a clear picture of the same size (null when the preview is not made). For tests.</summary>
    internal PixelBuffer? ShapePreview(((int X, int Y) A, (int X, int Y) B) box)
    {
        if (Smooth && (long)Picture.Width * Picture.Height > SmoothPreviewLimit) return null;
        var key = (box, Style, BrushSize, Smooth, Symmetry, Primary, Secondary, Tool, Picture, State.Selection);
        var (w, h) = (Picture.Width, Picture.Height);
        if (_shapeBuffer is null || _shapeBuffer.Width != w || _shapeBuffer.Height != h)
        {
            _shapeBuffer = new PixelBuffer(w, h);
            _shapeBitmap?.Dispose();
            _shapeBitmap = NewBitmap(_shapeBuffer);
            (_shapeDirty, _shapeKey) = (null, null);
        }

        if (_shapeKey is { } last && last.Equals(key)) return _shapeBuffer;
        _shapeKey = key;
        var before = _shapeDirty;
        if (before is { } old)
            for (var row = old.Y; row < old.Y + old.Height; row++) Array.Clear(_shapeBuffer.Data, (row * w + old.X) * 4, old.Width * 4);
        var state = new ImageState(_shapeBuffer, State.Selection);
        foreach (var op in ShapeOps(box)) ImageOps.Run(state, op);
        var made = _shapeBuffer.TakeDirty() is var (x, y, dw, dh) ? new Box(x, y, dw, dh) : (Box?)null;
        _shapeDirty = made;
        // Only the rows that changed, the old shape's and the new one's, go to the bitmap.
        var area = (before, made) switch
        {
            ({ } a, { } b) => new Box(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X + a.Width, b.X + b.Width) - Math.Min(a.X, b.X), Math.Max(a.Y + a.Height, b.Y + b.Height) - Math.Min(a.Y, b.Y)),
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => (Box?)null,
        };
        if (area is { } u && _shapeBitmap is { } bitmap)
        {
            using var frame = bitmap.Lock();
            for (var row = u.Y; row < u.Y + u.Height; row++) Marshal.Copy(_shapeBuffer.Data, (row * w + u.X) * 4, frame.Address + row * frame.RowBytes + u.X * 4, u.Width * 4);
        }

        return _shapeBuffer;
    }

    private bool DrawShape(DrawingContext context, ((int X, int Y) A, (int X, int Y) B) box)
    {
        if (ShapePreview(box) is null || _shapeBitmap is null) return false;
        DrawBitmap(context, _shapeBitmap, Picture.Width, Picture.Height, PictureRect);
        return true;
    }

    private void ReleaseShapePreview()
    {
        _shapeBitmap?.Dispose();
        (_shapeBitmap, _shapeBuffer, _shapeDirty, _shapeKey) = (null, null, null, null);
    }
}
