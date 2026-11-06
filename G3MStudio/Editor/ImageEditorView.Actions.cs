using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class ImageEditorView
{
    /// <summary>Pixels copied from the picture, shared by every editor in this program so a copy in one sprite pastes into another.</summary>
    private static PixelBuffer? _clipboard;

    private static MenuItem Item(string key, Action run)
    {
        var item = new MenuItem { Header = Loc.Current[key] };
        item.Click += (_, _) => run();
        return item;
    }

    private static MenuItem Group(string key, params MenuItem[] items) => new() { Header = Loc.Current[key], ItemsSource = items };

    private Menu Menus()
    {
        var menu = new Menu { Name = "ImageMenu" };
        menu.Items.Add(Group("image.menu.selection",
            Item("image.selectAll", () => Canvas.SetSelection(PixelSelection.All(Canvas.Picture.Width, Canvas.Picture.Height))),
            Item("image.selectNone", () => Canvas.SetSelection(null)),
            Item("image.invertSelection", () => Canvas.SetSelection(Canvas.State.Selection?.Inverted())),
            Item("image.expand", () => _ = Ask("image.expand", [("image.p.pixels", 1, -64, 64)], v => Canvas.SetSelection(Canvas.State.Selection?.Expanded((int)v[0])))),
            Item("image.copy", () => CopySelection(false)),
            Item("image.cut", () => CopySelection(true)),
            Item("image.paste", PasteClipboard),
            Item("image.clear", () => Canvas.Run(new ImageOp(ImageOps.Clear))),
            Item("image.fillSelection", () => Canvas.Run(new ImageOp(ImageOps.FillSelection, ImageOp.Format(Canvas.Primary)))),
            Item("image.strokeSelection", () => _ = Ask("image.strokeSelection", [("image.p.pixels", 1, 1, 64)], v => Canvas.Run(new ImageOp(ImageOps.StrokeSelection, ImageOp.Format(Canvas.Primary), ImageOp.Format((int)v[0])))))));
        menu.Items.Add(Group("image.menu.transform",
            Item("image.flipH", () => Canvas.Run(new ImageOp(ImageOps.Flip, "h"))),
            Item("image.flipV", () => Canvas.Run(new ImageOp(ImageOps.Flip, "v"))),
            Item("image.rotateCw", () => Canvas.Run(new ImageOp(ImageOps.Rotate90, "cw"))),
            Item("image.rotateCcw", () => Canvas.Run(new ImageOp(ImageOps.Rotate90, "ccw"))),
            Item("image.rotate", () => _ = Ask("image.rotate", [("image.p.angle", 45, -360, 360)], v => Canvas.Run(new ImageOp(ImageOps.Rotate, ImageOp.Format(v[0]))))),
            Item("image.scale", () => _ = Ask("image.scale", [("image.p.width", Canvas.Picture.Width, 1, 4096), ("image.p.height", Canvas.Picture.Height, 1, 4096)], v => Canvas.Run(new ImageOp(ImageOps.Scale, ImageOp.Format((int)v[0]), ImageOp.Format((int)v[1]))))),
            Item("image.skew", () => _ = Ask("image.skew", [("image.p.angle", 20, -80, 80)], v => Canvas.Run(new ImageOp(ImageOps.Skew, ImageOp.Format(v[0])))))));
        menu.Items.Add(Group("image.menu.adjust",
            Item("image.adjust", () => _ = Ask("image.adjust", [("image.p.hue", 0, -180, 180), ("image.p.saturation", 0, -100, 100), ("image.p.brightness", 0, -100, 100), ("image.p.contrast", 0, -100, 100)], v => Canvas.Run(new ImageOp(ImageOps.Adjust, ImageOp.Format(v[0]), ImageOp.Format(v[1]), ImageOp.Format(v[2]), ImageOp.Format(v[3]))))),
            Item("image.invertColours", () => Canvas.Run(new ImageOp(ImageOps.Invert))),
            Item("image.posterize", () => _ = Ask("image.posterize", [("image.p.levels", 4, 2, 256)], v => Canvas.Run(new ImageOp(ImageOps.Posterize, ImageOp.Format((int)v[0]))))),
            Item("image.greyscale", () => _ = Ask("image.greyscale", [("image.p.amount", 100, 0, 100)], v => Canvas.Run(new ImageOp(ImageOps.Greyscale, ImageOp.Format((int)v[0]))))),
            Item("image.dither", () => _ = Ask("image.dither", [("image.p.levels", 4, 2, 16)], v => Canvas.Run(new ImageOp(ImageOps.Dither, ImageOp.Format((int)v[0]))))),
            Item("image.outline", () => Canvas.Run(new ImageOp(ImageOps.Outline, ImageOp.Format(Canvas.Primary)))),
            Item("image.blur", () => _ = Ask("image.blur", [("image.p.radius", 1, 1, 8)], v => Canvas.Run(new ImageOp(ImageOps.Blur, ImageOp.Format((int)v[0])))))));
        menu.Items.Add(Group("image.menu.canvas",
            Item("image.resize", () => _ = Ask("image.resize", [("image.p.width", Canvas.Picture.Width, 1, 4096), ("image.p.height", Canvas.Picture.Height, 1, 4096), ("image.p.anchorX", 0, 0, 2), ("image.p.anchorY", 0, 0, 2)], v => Canvas.Run(new ImageOp(ImageOps.Resize, ImageOp.Format((int)v[0]), ImageOp.Format((int)v[1]), ImageOp.Format((int)v[2]), ImageOp.Format((int)v[3]))))),
            Item("image.crop", () => Canvas.Run(new ImageOp(ImageOps.Crop))),
            Item("image.trim", () => Canvas.Run(new ImageOp(ImageOps.Trim)))));
        return menu;
    }

    /// <summary>Copies the selected pixels (the whole picture without a selection); a cut also clears them.</summary>
    public void CopySelection(bool cut)
    {
        if (Canvas.Picture.Extract(Canvas.State.Selection) is not var (pixels, _)) return;
        _clipboard = pixels;
        if (cut) Canvas.Run(new ImageOp(ImageOps.Clear));
    }

    /// <summary>Pastes the copied pixels at the top left of what is in view, as a selection that can be moved.</summary>
    public void PasteClipboard()
    {
        if (_clipboard is not { } pixels) return;
        Canvas.Run(new ImageOp(ImageOps.Paste, ImageOp.Format([(0, 0)]), ImageOp.Format([(pixels.Width, pixels.Height)]), Convert.ToBase64String(pixels.Data)));
        SetTool(ImageTool.Move);
    }

    private async Task Ask(string titleKey, (string Label, double Value, double Min, double Max)[] fields, Action<double[]> done)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (await ParameterWindow.AskAsync(owner, Loc.Current[titleKey], [.. fields.Select(f => (Loc.Current[f.Label], f.Value, f.Min, f.Max))]) is { } values) done(values);
    }
}
