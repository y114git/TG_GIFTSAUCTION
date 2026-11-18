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
        var item = new MenuItem { Header = Loc.Current[key], Tag = id, ToggleType = MenuItemToggleType.Radio };
        item.Click += (_, _) => Run(id);
        return item;
    }

    /// <summary>The four mode menus (Selection, Transform, Adjust, Canvas), so the one in use can be marked.</summary>
    private readonly List<DropDownButton> _modeMenus = [];

    private DropDownButton Group(string key, params MenuItem[] items)
    {
        var button = new DropDownButton { Content = Loc.Current[key], Flyout = new MenuFlyout { ItemsSource = items }, Tag = items };
        _modeMenus.Add(button);
        return button;
    }

    /// <summary>The menu command run last; cleared when a tool is chosen. A menu holds one-shot commands, so "in use" means this, not a state of the picture.</summary>
    private string? _lastMenuCommand;

    /// <summary>Marks the menu in use: the one whose command ran last (that item checked), and Selection while a selection tool is the tool.</summary>
    private void MarkMenus()
    {
        for (var i = 0; i < _modeMenus.Count; i++)
        {
            var items = _modeMenus[i].Tag as MenuItem[] ?? [];
            foreach (var item in items) item.IsChecked = _lastMenuCommand is not null && item.Tag as string == _lastMenuCommand;
            var selecting = i == 0 && Tool is ImageTool.Select or ImageTool.Lasso or ImageTool.Wand or ImageTool.Move;
            _modeMenus[i].Classes.Set("active", selecting || items.Any(item => item.IsChecked));
        }
    }

    /// <summary>The four command groups as drop-down buttons, so they match the other buttons of the bar (same height, border and hover).</summary>
    private DropDownButton[] Menus() =>
    [
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
    ];

    /// <summary>Copies the selected pixels (the whole picture without a selection); a cut also clears them.</summary>
    public void CopySelection(bool cut)
    {
        if (cut && Canvas.Refuse()) return; // a cut that cannot clear the pixels must not take them to the clipboard either
        if (Canvas.Picture.Extract(Canvas.State.Selection) is not var (pixels, _)) return;
        _clipboard = pixels;
        _ = ShareClipboardAsync(pixels);
        if (cut) Canvas.Run(new ImageOp(ImageOps.Clear));
    }

    // The copy also goes to the system clipboard as a bitmap, so other programs can paste it and a later paste here is not shadowed by an older system image.
    private async Task ShareClipboardAsync(PixelBuffer pixels)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
            using var stream = new MemoryStream(FramePicture.ToPng(pixels));
            using var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            await Avalonia.Input.Platform.ClipboardExtensions.SetBitmapAsync(clipboard, bitmap);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or NotSupportedException or System.Runtime.InteropServices.ExternalException)
        {
            // A platform without clipboard bitmaps keeps the internal copy, which Paste still uses.
        }
    }

    /// <summary>Pastes a bitmap from the system clipboard as a movable selection; without one, the copy made in this editor.</summary>
    public async Task PasteAsync()
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && await Avalonia.Input.Platform.ClipboardExtensions.TryGetBitmapAsync(clipboard) is { } bitmap)
            {
                using (bitmap)
                {
                    using var stream = new MemoryStream();
                    bitmap.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    var pixels = FramePicture.FromBytes(stream.ToArray());
                    _clipboard = pixels;
                }
            }
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or NotSupportedException or ImageMagick.MagickException or System.Runtime.InteropServices.ExternalException)
        {
            // An unreadable system image falls back to the editor's own copy.
        }

        PasteClipboard();
    }

    /// <summary>Pastes the copied pixels at the top left of what is in view, as a selection that can be moved.</summary>
    public void PasteClipboard()
    {
        if (_clipboard is not { } pixels || Canvas.Refuse()) return;
        Canvas.Run(new ImageOp(ImageOps.Paste, ImageOp.Format([(0, 0)]), ImageOp.Format([(pixels.Width, pixels.Height)]), ImageOps.Pack(pixels.Data)));
        SetTool(ImageTool.Move);
    }

    /// <summary>The Resize dialog; a session without a whole sprite (the painter window) or a selection keeps the plain number prompts.</summary>
    private async Task<bool> AskResize(bool scale)
    {
        if (_session.ResizeContext is not { } context || Canvas.State.Selection is not null || TopLevel.GetTopLevel(this) is not Window owner) return false;
        var dialog = new ResizeImageWindow((Canvas.Picture.Width, Canvas.Picture.Height), context.Frames, context.Groups, context.Group, scale,
            choice => _session.PlanResize(Frame, choice.Spec, choice.AllFrames, choice.Fit) is { } steps ? FitText.Describe(steps) : null, context.Pages, context.AutomaticMax, context.Box, Frame, context.FrameSizes);
        ResizeOpened?.Invoke(dialog);
        if (await dialog.AskAsync(owner) is not { } choice) return true;
        if (RefuseStale()) return true;
        try
        {
            if (_session.CommitResize(Frame, choice.Spec, choice.AllFrames, choice.Group, choice.Fit))
            {
                Reload();
                ShowNotice();
            }
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            _status.Text = e.Message; // the edit put everything back before it threw
        }

        return true;
    }

    private ResizeSpec? _dragPending;
    /// <summary>What the canvas drag will do to the texture pages, as the status line shows it while the drag is on (empty when the session has no pages).</summary>
    public string DragFitText { get; private set; } = "";

    // While a canvas handle is dragged the status line shows the new size at once and the texture page line once the plan is worked out; planning waits for
    // the pointer to rest (one plan per dispatcher pass), since a page with many pictures takes a moment.
    private void ShowResizeDrag(ResizeSpec? spec)
    {
        if (spec is null)
        {
            (_dragPending, DragFitText, _dragSizes) = (null, "", null);
            ShowStatus();
            return;
        }

        _dragSizes ??= _session.ResizeContext?.FrameSizes ?? [];
        _status.Text = DragLabel(spec) + DragFitText;
        var queued = _dragPending is not null;
        _dragPending = spec;
        if (!queued) Avalonia.Threading.Dispatcher.UIThread.Post(PlanDrag, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void PlanDrag()
    {
        _dragPending = null;
        if (Canvas.ResizeDrag is not { } spec) return;
        try
        {
            DragFitText = _session.PlanResize(Frame, spec, spec.AllFrames || _session.FrameCount < 2, FitOptionsPanel.Current) is { } steps ? $"   {Loc.Current["pages.fitLabel"]}: {FitText.Describe(steps).Replace(Environment.NewLine, " ", StringComparison.Ordinal)}" : "";
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            DragFitText = "   " + e.Message;
        }

        _status.Text = DragLabel(spec) + DragFitText;
    }

    // "Canvas 120 x 90, frame 3" or "..., all 12 frames (now 1000 x 1000, 64 x 64)": the scope is in the label before the pointer is let go.
    private IReadOnlyList<(int Width, int Height)>? _dragSizes;

    private string DragLabel(ResizeSpec spec) =>
        Loc.Current.Format(spec.Scale ? "image.dragStretch" : "image.dragSize", ("w", spec.Width), ("h", spec.Height)) + ResizeScope.DragSuffix(spec, Frame, _dragSizes ?? []);

    /// <summary>
    /// A canvas handle was released: the new size as one undoable edit, for the selected frame, or for every frame of the sprite (its box follows) when the all-frames toggle or key was on; else the picture in the editor.
    /// False when the size was not applied (a locked picture, or the pages refused it), so the canvas does not move the picture.
    /// </summary>
    private bool CommitCanvasSize(ResizeSpec spec)
    {
        if (RefuseStale()) return false;
        if (_session.ResizeContext is null)
        {
            Canvas.SetSelection(null);
            Canvas.Run(spec.Op);
            return (Canvas.Picture.Width, Canvas.Picture.Height) == (spec.Width, spec.Height);
        }

        try
        {
            if (!_session.CommitResize(Frame, spec, spec.AllFrames || _session.FrameCount < 2, null, FitOptionsPanel.Current)) return false;
            Reload();
            ShowNotice();
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            _status.Text = e.Message;
            return false;
        }
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
