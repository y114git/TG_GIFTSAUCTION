using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

public sealed partial class ImageEditorView
{
    private static readonly Dictionary<string, ImageTool> ToolCommands = ImageTools.All.ToDictionary(ImageTools.CommandOf);

    /// <summary>Runs one command of <see cref="Core.Commands.AppCommands.ImageCommands"/>; false for an id the editor does not know.</summary>
    public bool Run(string id)
    {
        if (ToolCommands.TryGetValue(id, out var tool)) ChooseTool(tool, false);
        else if (id.EndsWith("Filled", StringComparison.Ordinal) && ToolCommands.TryGetValue(id[..^"Filled".Length], out var shape)) ChooseTool(shape, true);
        else if (RunAction(id) || RunSelection(id) || RunTransform(id) || RunLayer(id))
        {
            if (_modeMenus.Any(m => ((MenuItem[])m.Tag!).Any(i => i.Tag as string == id))) _lastMenuCommand = id;
            MarkMenus();
        }
        else return false;
        return true;
    }

    private void ChooseTool(ImageTool tool, bool filled)
    {
        SetTool(tool);
        if (tool is ImageTool.Rectangle or ImageTool.Ellipse or ImageTool.Polygon) SetStyle(filled ? ShapeStyle.Filled : ShapeStyle.Outline);
    }

    private bool RunAction(string id)
    {
        switch (id)
        {
            case "img.brushBigger": SetSize(Canvas.BrushSize + 1); break;
            case "img.brushSmaller": SetSize(Canvas.BrushSize - 1); break;
            case "img.swapColours": SwapColours(); break;
            case "img.smooth": Flip(_smoothToggle); break;
            case "img.grid": Flip(_gridToggle); break;
            case "img.symmetryH": Flip(_mirrorX); break;
            case "img.symmetryV": Flip(_mirrorY); break;
            case "img.tiles": Flip(_tilesToggle); break;
            case "img.onion": Flip(_onionToggle); break;
            case "img.play": Flip(_playToggle); break;
            case "img.zoomIn": Canvas.ZoomBy(1.25); break;
            case "img.zoomOut": Canvas.ZoomBy(0.8); break;
            case "img.zoomFit": Canvas.Fit(); break;
            case "img.zoomActual": Canvas.ActualSize(); break;
            case "img.previousFrame": ShowFrame(Frame - 1); break;
            case "img.nextFrame": ShowFrame(Frame + 1); break;
            case "img.duplicateFrame": ChangeFrames(FrameListCommand.Duplicate); break;
            case "img.removeFrame": ChangeFrames(FrameListCommand.Remove); break;
            default: return false;
        }

        return true;
    }

    private static void Flip(Avalonia.Controls.Primitives.ToggleButton button) => button.IsChecked = button.IsChecked != true;

    private bool RunSelection(string id)
    {
        switch (id)
        {
            case "img.selectAll": Canvas.SetSelection(PixelSelection.All(Canvas.Picture.Width, Canvas.Picture.Height)); break;
            case "img.selectNone": Canvas.SetSelection(null); break;
            case "img.invertSelection": Canvas.SetSelection(Canvas.State.Selection?.Inverted()); break;
            case "img.expandSelection": _ = Ask("image.expand", [("image.p.pixels", 1, -64, 64)], v => Canvas.SetSelection(Canvas.State.Selection?.Expanded((int)v[0]))); break;
            case "img.copy": CopySelection(false); break;
            case "img.cut": CopySelection(true); break;
            case "img.paste": PasteClipboard(); break;
            case "img.clear": Canvas.Run(new ImageOp(ImageOps.Clear)); break;
            case "img.fillSelection": Canvas.Run(new ImageOp(ImageOps.FillSelection, ImageOp.Format(Canvas.Primary))); break;
            case "img.strokeSelection": _ = Ask("image.strokeSelection", [("image.p.pixels", 1, 1, 64)], v => Canvas.Run(new ImageOp(ImageOps.StrokeSelection, ImageOp.Format(Canvas.Primary), ImageOp.Format((int)v[0])))); break;
            default: return false;
        }

        return true;
    }

    private bool RunTransform(string id)
    {
        switch (id)
        {
            case "img.flipH": Canvas.Run(new ImageOp(ImageOps.Flip, "h")); break;
            case "img.flipV": Canvas.Run(new ImageOp(ImageOps.Flip, "v")); break;
            case "img.rotateCw": Canvas.Run(new ImageOp(ImageOps.Rotate90, "cw")); break;
            case "img.rotateCcw": Canvas.Run(new ImageOp(ImageOps.Rotate90, "ccw")); break;
            case "img.rotate": _ = Ask("image.rotate", [("image.p.angle", 45, -360, 360)], v => Canvas.Run(new ImageOp(ImageOps.Rotate, ImageOp.Format(v[0])))); break;
            case "img.scale": _ = AskResizeOr(true, () => Ask("image.scale", [("image.p.width", Canvas.Picture.Width, 1, 4096), ("image.p.height", Canvas.Picture.Height, 1, 4096)], v => Canvas.Run(new ImageOp(ImageOps.Scale, ImageOp.Format((int)v[0]), ImageOp.Format((int)v[1]))))); break;
            case "img.skew": _ = Ask("image.skew", [("image.p.angle", 20, -80, 80)], v => Canvas.Run(new ImageOp(ImageOps.Skew, ImageOp.Format(v[0])))); break;
            case "img.adjust": _ = Ask("image.adjust", [("image.p.hue", 0, -180, 180), ("image.p.saturation", 0, -100, 100), ("image.p.brightness", 0, -100, 100), ("image.p.contrast", 0, -100, 100)], v => Canvas.Run(new ImageOp(ImageOps.Adjust, ImageOp.Format(v[0]), ImageOp.Format(v[1]), ImageOp.Format(v[2]), ImageOp.Format(v[3])))); break;
            case "img.invertColours": Canvas.Run(new ImageOp(ImageOps.Invert)); break;
            case "img.posterize": _ = Ask("image.posterize", [("image.p.levels", 4, 2, 256)], v => Canvas.Run(new ImageOp(ImageOps.Posterize, ImageOp.Format((int)v[0])))); break;
            case "img.greyscale": _ = Ask("image.greyscale", [("image.p.amount", 100, 0, 100)], v => Canvas.Run(new ImageOp(ImageOps.Greyscale, ImageOp.Format((int)v[0])))); break;
            case "img.dither": _ = Ask("image.dither", [("image.p.levels", 4, 2, 16)], v => Canvas.Run(new ImageOp(ImageOps.Dither, ImageOp.Format((int)v[0])))); break;
            case "img.outline": Canvas.Run(new ImageOp(ImageOps.Outline, ImageOp.Format(Canvas.Primary))); break;
            case "img.blur": _ = Ask("image.blur", [("image.p.radius", 1, 1, 8)], v => Canvas.Run(new ImageOp(ImageOps.Blur, ImageOp.Format((int)v[0])))); break;
            case "img.resize": _ = AskResizeOr(false, () => Ask("image.resize", [("image.p.width", Canvas.Picture.Width, 1, 4096), ("image.p.height", Canvas.Picture.Height, 1, 4096), ("image.p.anchorX", 0, 0, 2), ("image.p.anchorY", 0, 0, 2)], v => Canvas.Run(new ImageOp(ImageOps.Resize, ImageOp.Format((int)v[0]), ImageOp.Format((int)v[1]), ImageOp.Format((int)v[2]), ImageOp.Format((int)v[3]))))); break;
            case "img.crop": Canvas.Run(new ImageOp(ImageOps.Crop)); break;
            case "img.trim": Canvas.Run(new ImageOp(ImageOps.Trim)); break;
            default: return false;
        }

        return true;
    }

    /// <summary>Puts the options of Settings into the controls and the canvas; the controls then carry them into the canvas through their own handlers.</summary>
    private void ApplyOptions()
    {
        var o = _options;
        _size.Value = o.BrushSize;
        _round.IsChecked = o.BrushRound;
        _smoothToggle.IsChecked = o.Smooth;
        _tolerance.Value = o.Tolerance;
        _contiguous.IsChecked = o.Contiguous;
        _textSize.Value = o.TextSize;
        _gridToggle.IsChecked = o.ShowGrid;
        Canvas.GridCell = o.GridCell;
        Canvas.OnionOpacity = o.OnionOpacity / 100.0;
        Canvas.HandleSize = o.HandleSize;
        Canvas.TextFont = o.TextFont;
        Canvas.GridBrush = Paint(o.GridColour);
        Canvas.ChequerLight = Paint(o.CheckerLight);
        Canvas.ChequerDark = Paint(o.CheckerDark);
        Canvas.Backdrop = Paint(o.CanvasBackground);
    }

    private static SolidColorBrush? Paint(string text) => Rgba.Parse(text) is { } c ? new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B)) : null;

    /// <summary>The Text tool: draws the text of the options bar at the clicked pixel as pixels in a stamp op.</summary>
    public void PlaceText((int X, int Y) cell, Rgba colour)
    {
        if (TextRaster.Render(_text.Text ?? "", Canvas.TextFont, _textSize.Value is { } size ? (double)size : _options.TextSize, colour, Canvas.Smooth) is not { } pixels) return;
        Canvas.Run(new ImageOp(ImageOps.Stamp, ImageOp.Format([cell]), ImageOp.Format([(pixels.Width, pixels.Height)]), Convert.ToBase64String(pixels.Data)));
    }
}
