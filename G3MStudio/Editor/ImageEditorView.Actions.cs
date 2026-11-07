using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class ImageEditorView
{
    /// <summary>Pixels copied from the picture, shared by every editor in this program so a copy in one sprite pastes into another.</summary>
    private static PixelBuffer? _clipboard;

    private MenuItem Item(string key, string id)
    {
        var item = new MenuItem { Header = Loc.Current[key] };
        item.Click += (_, _) => Run(id);
        return item;
    }

    private static MenuItem Group(string key, params MenuItem[] items) => new() { Header = Loc.Current[key], ItemsSource = items };

    private Menu Menus()
    {
        var menu = new Menu { Name = "ImageMenu" };
        menu.Items.Add(Group("image.menu.selection",
            Item("image.selectAll", "img.selectAll"), Item("image.selectNone", "img.selectNone"), Item("image.invertSelection", "img.invertSelection"),
            Item("image.expand", "img.expandSelection"), Item("image.copy", "img.copy"), Item("image.cut", "img.cut"), Item("image.paste", "img.paste"),
            Item("image.clear", "img.clear"), Item("image.fillSelection", "img.fillSelection"), Item("image.strokeSelection", "img.strokeSelection")));
        menu.Items.Add(Group("image.menu.transform",
            Item("image.flipH", "img.flipH"), Item("image.flipV", "img.flipV"), Item("image.rotateCw", "img.rotateCw"), Item("image.rotateCcw", "img.rotateCcw"),
            Item("image.rotate", "img.rotate"), Item("image.scale", "img.scale"), Item("image.skew", "img.skew")));
        menu.Items.Add(Group("image.menu.adjust",
            Item("image.adjust", "img.adjust"), Item("image.invertColours", "img.invertColours"), Item("image.posterize", "img.posterize"), Item("image.greyscale", "img.greyscale"),
            Item("image.dither", "img.dither"), Item("image.outline", "img.outline"), Item("image.blur", "img.blur")));
        menu.Items.Add(Group("image.menu.canvas", Item("image.resize", "img.resize"), Item("image.crop", "img.crop"), Item("image.trim", "img.trim")));
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
