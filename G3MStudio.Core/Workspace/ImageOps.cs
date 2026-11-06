using System.Globalization;

namespace G3MStudio.Core.Workspace;

/// <summary>The picture being edited and the selection on it (null: nothing selected, so tools reach every pixel).</summary>
public sealed class ImageState(PixelBuffer picture, PixelSelection? selection = null)
{
    public PixelBuffer Picture { get; set; } = picture;

    public PixelSelection? Selection { get; set; } = selection;
}

/// <summary>One edit of the image editor as text, so the journal can replay it: a kind and its arguments, joined with ';'.</summary>
public sealed record ImageOp(string Kind, params string[] Parts)
{
    public override string ToString() => string.Join(';', [Kind, .. Parts]);

    public static ImageOp Parse(string line) => line.Split(';') is [var kind, .. var parts] ? new ImageOp(kind, parts) : throw new InvalidDataException("An image edit is empty.");

    public static string Format(IEnumerable<(int X, int Y)> points) => string.Join(' ', points.Select(p => $"{p.X.ToString(CultureInfo.InvariantCulture)},{p.Y.ToString(CultureInfo.InvariantCulture)}"));

    public static string Format(Rgba? colour) => colour?.ToString() ?? "-";

    public static string Format(int number) => number.ToString(CultureInfo.InvariantCulture);

    public static string Format(double number) => number.ToString("R", CultureInfo.InvariantCulture);

    internal int Int(int index) => int.Parse(Parts[index], CultureInfo.InvariantCulture);

    internal double Number(int index) => double.Parse(Parts[index], CultureInfo.InvariantCulture);

    internal Rgba? Colour(int index) => Parts[index] == "-" ? null : Rgba.Parse(Parts[index]) ?? throw new InvalidDataException($"'{Parts[index]}' is not a colour.");

    internal Rgba RequiredColour(int index) => Colour(index) ?? throw new InvalidDataException("A colour is needed.");

    internal List<(int X, int Y)> Points(int index) => [.. Parts[index].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split(',')).Select(p => (int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture)))];

    internal BrushShape Shape(int index) => Parts[index] == "1" ? BrushShape.Round : BrushShape.Square;
}

/// <summary>Runs <see cref="ImageOp"/>s on an <see cref="ImageState"/>. The live editor and the journal replay both go through here, so they give the same pixels.</summary>
public static class ImageOps
{
    public const string Stroke = "stroke", Rect = "rect", Ellipse = "ellipse", Polygon = "polygon", Curve = "curve", Fill = "fill", Replace = "replace", Remove = "remove",
        FillSelection = "fillsel", StrokeSelection = "strokesel", Clear = "clear", Paste = "paste", Move = "move", Flip = "flip", Rotate90 = "rot90", Rotate = "rotate", Scale = "scale",
        Skew = "skew", Resize = "resize", Crop = "crop", Trim = "trim", Invert = "invert", Posterize = "posterize", Adjust = "adjust", Greyscale = "greyscale", Dither = "dither",
        Outline = "outline", Blur = "blur";

    public static void Run(ImageState state, ImageOp op)
    {
        var picture = state.Picture;
        picture.Clip = state.Selection;
        try
        {
            if (!RunDraw(picture, op) && !RunColour(picture, op) && !RunRegion(state, op)) throw new InvalidDataException($"Unknown image edit '{op.Kind}'.");
        }
        finally
        {
            state.Picture.Clip = null;
        }
    }

    private static bool RunDraw(PixelBuffer picture, ImageOp op)
    {
        switch (op.Kind)
        {
            case Stroke: picture.Stroke(op.Points(3), op.Int(0), op.RequiredColour(2), op.Shape(1)); break;
            case Rect: picture.Rectangle(op.Points(3)[0], op.Points(3)[1], op.Colour(0), op.Colour(1), op.Int(2)); break;
            case Ellipse: picture.Ellipse(op.Points(3)[0], op.Points(3)[1], op.Colour(0), op.Colour(1), op.Int(2)); break;
            case Polygon: picture.Polygon(op.Points(4), op.Colour(0), op.Colour(1), op.Int(2), op.Shape(3)); break;
            case Curve: picture.Curve(op.Points(3), op.Int(0), op.RequiredColour(2), op.Shape(1)); break;
            case Fill: picture.Fill(op.Points(1)[0].X, op.Points(1)[0].Y, op.RequiredColour(0), op.Int(2), op.Parts[3] == "1"); break;
            case Replace or Remove: ReplaceColour(picture, op); break;
            default: return false;
        }

        return true;
    }

    private static void ReplaceColour(PixelBuffer picture, ImageOp op)
    {
        var (x, y) = op.Points(0)[0];
        if (!picture.Contains(x, y)) return;
        picture.Replace(picture.Get(x, y), op.Kind == Remove ? Rgba.Clear : op.RequiredColour(1), op.Int(op.Kind == Remove ? 1 : 2));
    }

    private static bool RunColour(PixelBuffer picture, ImageOp op)
    {
        switch (op.Kind)
        {
            case Invert: picture.InvertColours(); break;
            case Posterize: picture.Posterize(op.Int(0)); break;
            case Adjust: picture.Adjust(op.Number(0), op.Number(1), op.Number(2), op.Number(3)); break;
            case Greyscale: picture.Greyscale(op.Int(0)); break;
            case Dither: picture.Dither(op.Int(0)); break;
            case Outline: picture.Outline(op.RequiredColour(0)); break;
            case Blur: picture.Blur(op.Int(0)); break;
            default: return false;
        }

        return true;
    }

    private static bool RunRegion(ImageState state, ImageOp op)
    {
        var picture = state.Picture;
        switch (op.Kind)
        {
            case FillSelection when state.Selection is { } selection: picture.Clip = null; picture.FillSelection(selection, op.RequiredColour(0)); break;
            case StrokeSelection when state.Selection is { } selection: picture.Clip = null; picture.FillMask(selection.Border().Expanded(op.Int(1) - 1).Mask, op.RequiredColour(0)); break;
            case FillSelection or StrokeSelection: break;
            case Clear: picture.ClearSelected(state.Selection); break;
            case Paste: PasteStamp(state, op); break;
            case Move: MoveSelected(state, op.Points(0)[0].X, op.Points(0)[0].Y); break;
            case Flip: Transform(state, p => op.Parts[0] == "h" ? p.FlippedHorizontally() : p.FlippedVertically()); break;
            case Rotate: Transform(state, p => p.Rotated(op.Number(0))); break;
            case Rotate90 or Scale or Skew or Resize or Crop or Trim: WholeOrSelected(state, op); break;
            default: return false;
        }

        return true;
    }

    private static void PasteStamp(ImageState state, ImageOp op)
    {
        var (x, y) = op.Points(0)[0];
        var (w, h) = op.Points(1)[0];
        var pixels = new PixelBuffer(w, h, Convert.FromBase64String(op.Parts[2]));
        state.Picture.Clip = null;
        state.Picture.Place(pixels, x, y);
        state.Selection = PixelSelection.Rectangle(state.Picture.Width, state.Picture.Height, x, y, x + w - 1, y + h - 1);
    }

    // Size-changing edits: without a selection they change the canvas; with one they change only the selected pixels.
    private static void WholeOrSelected(ImageState state, ImageOp op)
    {
        var picture = state.Picture;
        if (state.Selection is not null && op.Kind is Rotate90 or Scale or Skew)
        {
            Transform(state, op.Kind switch
            {
                Rotate90 => p => p.Rotated90(op.Parts[0] == "cw"),
                Scale => p => p.Scaled(op.Int(0), op.Int(1)),
                _ => p => p.SkewedHorizontally(op.Number(0)),
            });
            return;
        }

        state.Picture = op.Kind switch
        {
            Rotate90 => picture.Rotated90(op.Parts[0] == "cw"),
            Scale => picture.Scaled(op.Int(0), op.Int(1)),
            Skew => picture.SkewedHorizontally(op.Number(0)),
            Resize => picture.Resized(op.Int(0), op.Int(1), op.Int(2), op.Int(3)),
            Crop => state.Selection?.Bounds() is var (x, y, w, h) ? picture.Cropped(x, y, w, h) : picture,
            _ => picture.Trimmed(),
        };
        state.Selection = null;
    }

    // A move keeps the shape, so the mask only shifts: copied row by row instead of being rebuilt from pixels.
    private static void MoveSelected(ImageState state, int dx, int dy)
    {
        var picture = state.Picture;
        if (picture.Extract(state.Selection) is not var (pixels, box)) return;
        picture.ClearSelected(state.Selection);
        picture.Place(pixels, box.X + dx, box.Y + dy);
        if (state.Selection is not { } old) return;
        var shifted = new PixelSelection(old.Width, old.Height);
        var (from, width) = (Math.Max(0, -dx), old.Width - Math.Abs(dx));
        for (var y = Math.Max(0, -dy); y < Math.Min(old.Height, old.Height - dy) && width > 0; y++)
            old.Mask.AsSpan(y * old.Width + from, width).CopyTo(shifted.Mask.AsSpan((y + dy) * old.Width + from + dx, width));
        state.Selection = shifted;
    }

    /// <summary>
    /// Lifts the selected pixels (all pixels without a selection), changes them with <paramref name="change"/>, and puts the result back around the same centre
    /// moved by the offset. The selection follows. The mask goes through the same change, so it matches the pixels.
    /// </summary>
    private static void Transform(ImageState state, Func<PixelBuffer, PixelBuffer> change, int dx = 0, int dy = 0)
    {
        var picture = state.Picture;
        if (picture.Extract(state.Selection) is not var (pixels, box)) return;
        var mask = PixelBuffer.OpaqueWhere(state.Selection, box);
        picture.ClearSelected(state.Selection);
        var (moved, movedMask) = (change(pixels), change(mask));
        var (ox, oy) = (box.X + (box.Width - moved.Width) / 2 + dx, box.Y + (box.Height - moved.Height) / 2 + dy);
        picture.Place(moved, ox, oy);
        var selection = movedMask.ToSelection(picture.Width, picture.Height, ox, oy);
        state.Selection = state.Selection is null ? null : selection;
    }
}
