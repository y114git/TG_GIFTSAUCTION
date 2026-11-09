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

    private static DropDownButton Group(string key, params MenuItem[] items) => new() { Content = Loc.Current[key], Flyout = new MenuFlyout { ItemsSource = items } };

    /// <summary>The four command groups as drop-down buttons, so they match the other buttons of the bar (same height, border and hover).</summary>
    private StackPanel Menus()
    {
        var bar = new StackPanel { Name = "ImageMenu", Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
        foreach (var group in new[]
        {
            Group("image.menu.selection",
                Item("image.selectAll", "img.selectAll"), Item("image.selectNone", "img.selectNone"), Item("image.invertSelection", "img.invertSelection"),
                Item("image.expand", "img.expandSelection"), Item("image.copy", "img.copy"), Item("image.cut", "img.cut"), Item("image.paste", "img.paste"),
                Item("image.clear", "img.clear"), Item("image.fillSelection", "img.fillSelection"), Item("image.strokeSelection", "img.strokeSelection")),
            Group("image.menu.transform",
                Item("image.flipH", "img.flipH"), Item("image.flipV", "img.flipV"), Item("image.rotateCw", "img.rotateCw"), Item("image.rotateCcw", "img.rotateCcw"),
                Item("image.rotate", "img.rotate"), Item("image.scale", "img.scale"), Item("image.skew", "img.skew")),
            Group("image.menu.adjust",
                Item("image.adjust", "img.adjust"), Item("image.invertColours", "img.invertColours"), Item("image.posterize", "img.posterize"), Item("image.greyscale", "img.greyscale"),
                Item("image.dither", "img.dither"), Item("image.outline", "img.outline"), Item("image.blur", "img.blur")),
            Group("image.menu.canvas", Item("image.resize", "img.resize"), Item("image.crop", "img.crop"), Item("image.trim", "img.trim")),
        }) bar.Children.Add(group);
        return bar;
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

    /// <summary>The Resize dialog; a session without a whole sprite (the painter window) or a selection keeps the plain number prompts.</summary>
    private async Task<bool> AskResize(bool scale)
    {
        if (_session.ResizeContext is not { } context || Canvas.State.Selection is not null || TopLevel.GetTopLevel(this) is not Window owner) return false;
        var dialog = new ResizeImageWindow((Canvas.Picture.Width, Canvas.Picture.Height), context.Frames, context.Groups, context.Group, scale);
        ResizeOpened?.Invoke(dialog);
        if (await dialog.AskAsync(owner) is not { } choice) return true;
        if (_session.CommitResize(Frame, choice.Spec, choice.AllFrames, choice.Group)) Reload();
        return true;
    }

    private async Task AskResizeOr(bool scale, Func<Task> fallback)
    {
        if (!await AskResize(scale)) await fallback();
    }

    /// <summary>Raised with each Resize dialog, so a test can drive it.</summary>
    public event Action<ResizeImageWindow>? ResizeOpened;

    private async Task Ask(string titleKey, (string Label, double Value, double Min, double Max)[] fields, Action<double[]> done)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (await ParameterWindow.AskAsync(owner, Loc.Current[titleKey], [.. fields.Select(f => (Loc.Current[f.Label], f.Value, f.Min, f.Max))]) is { } values) done(values);
    }
}
