using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

public sealed partial class ImageEditorView
{
    private static readonly string[] Palette =
    [
        "#000000", "#202020", "#404040", "#606060", "#808080", "#A0A0A0", "#C0C0C0", "#FFFFFF",
        "#800000", "#FF0000", "#FF8000", "#FFC000", "#FFFF00", "#80FF00", "#00FF00", "#00FF80",
        "#00FFFF", "#0080FF", "#0000FF", "#8000FF", "#FF00FF", "#FF0080", "#804000", "#408000",
        "#004080", "#400080", "#FFC0C0", "#C0FFC0", "#C0C0FF", "#FFE0A0", "#A0E0FF", "#E0A0FF",
    ];

    private readonly List<(ImageTool Tool, ToggleButton Button)> _toolButtons = [];
    private readonly Button _undoButton = new() { Name = "ImageUndo" };
    private readonly Button _redoButton = new() { Name = "ImageRedo" };
    private readonly Button _applyButton = new() { Name = "ImageApply" };
    private readonly Button _discardButton = new() { Name = "ImageDiscard", Padding = new Thickness(6, 2) };
    private readonly Button _applyMore = new() { Name = "ImageApplyMore", Padding = new Thickness(4, 2), CornerRadius = new CornerRadius(0, 4, 4, 0) };
    private readonly Button _originalButton = new() { Name = "ImageOriginal", Padding = new Thickness(6, 2) };
    private WrapPanel _originalGroup = new();
    private MenuItem _saveFrameItem = new(), _saveAllItem = new();
    private readonly TextBlock _pendingMark = new() { Name = "ImagePending", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _primary = new() { Name = "PrimarySwatch", Width = 40, Height = 40, BorderThickness = new Thickness(2) };
    private readonly Border _secondary = new() { Name = "SecondarySwatch", Width = 40, Height = 40, BorderThickness = new Thickness(2) };
    private readonly Border _outlineSwatch = new() { Name = "OutlineSwatch", Width = 16, Height = 16, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _fillSwatch = new() { Name = "FillSwatch", Width = 16, Height = 16, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
    private StackPanel _outlineKey = new(), _fillKey = new();
    private readonly TextBox _hex = new() { Name = "ImageColour", Width = 100 };
    private readonly WrapPanel _recentPanel = new() { MaxWidth = 200, MinHeight = 26 }; // recent colours sit above the palette so the tall palette never pushes them out of view
    private readonly NumericUpDown _size = new() { Name = "ImageSize", Minimum = 1, Maximum = 256, Value = 1, Increment = 1, FormatString = "0", MinWidth = 110 };
    private readonly ComboBox _style = new() { Name = "ImageStyle", Width = 110 };
    private readonly NumericUpDown _tolerance = new() { Name = "ImageTolerance", Minimum = 0, Maximum = 255, Value = 0, Increment = 5, FormatString = "0", MinWidth = 110 };
    private readonly CheckBox _contiguous = new() { Name = "ImageContiguous", IsChecked = true };
    private readonly Slider _sizeSlider = new() { Name = "ImageSizeSlider", Minimum = 1, Maximum = 64, Value = 1, Width = 110, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _text = new() { Name = "ImageText", Width = 140, MaxLength = 200 };
    private readonly NumericUpDown _textSize = new() { Name = "ImageTextSize", Minimum = 4, Maximum = 256, Value = 16, Increment = 1, FormatString = "0", MinWidth = 110 };
    private ToggleButton _smoothToggle = new(), _gridToggle = new(), _mirrorX = new(), _mirrorY = new(), _tilesToggle = new(), _onionToggle = new(), _playToggle = new();
    private readonly StackPanel _frames = new() { Name = "ImageFrames", Spacing = 4, Margin = new Thickness(6) };
    private readonly StackPanel _thumbs = new() { Name = "ImageFrameStrip", Spacing = 4 };
    private readonly ScrollViewer _framesHost = new() { Name = "ImageFramesScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; // captions wrap to the column
    private bool _thumbsStale = true;
    private readonly OverflowBar _bar = new() { Name = "ImageBar", Margin = new Thickness(6, 4) };
    private WrapPanel _brushGroup = new(), _styleGroup = new(), _toleranceGroup = new(), _textGroup = new(), _changesGroup = new();
    private readonly Grid _hintSlot = new();
    private readonly Decorator _framesSlot = new() { Name = "ImageFramesSlot" }, _sideSlot = new() { Name = "ImageSideSlot" };
    private readonly ScrollViewer _sideHost = new() { Name = "ImageSideScroll" };

    private void BuildLayout()
    {
        var top = Options();
        // The pointer position, the drag hint and the tool hint take their widest text in a hidden copy, so the line under the canvas keeps its height while the pointer moves.
        var ghost = new TextBlock { Text = "(-99999, -99999)", Classes = { "muted" }, Opacity = 0, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAccessibilityView(ghost, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_dragHint, AccessibilityView.Raw);
        _hintSlot.Children.Add(_hint);
        _hintSlot.Children.Add(_dragHint);
        // The hint has its own row, so how it wraps never depends on how wide the status text is.
        var bottom = new StackPanel { Margin = new Thickness(8, 2), Children = { new WrapPanel { ItemSpacing = 16, Children = { new Grid { Children = { ghost, _cursor } }, _status, _pendingMark, _allFrames } }, _hintSlot } };
        SetDock(top, Avalonia.Controls.Dock.Top);
        SetDock(bottom, Avalonia.Controls.Dock.Bottom);
        _sideHost.Content = new StackPanel { Margin = new Thickness(6), Spacing = 6, Children = { Brushes(), Colours(), Toolbox(), LayersPanel() } };
        _framesHost.Content = _frames; // the frame column scrolls when its buttons and thumbnails are taller than the editor
        BuildFrames();
        _framesSlot.Child = _framesHost;
        _sideSlot.Child = _sideHost;
        Children.Add(top);
        Children.Add(bottom);
        // Both side columns are dragged wider or narrower behind a splitter; the size is kept for the session. The canvas keeps over half of the width.
        Children.Add(G3MStudio.Theme.SideSplit.Around(Canvas, 0.55, new G3MStudio.Theme.SideColumn(_framesSlot, "image.frames", 150, 110, true), new G3MStudio.Theme.SideColumn(_sideSlot, "imageSide", 232, 150)));
        SizeChanged += (_, _) => FitToArea();
    }

    // The canvas always keeps most of the editor: a short editor gets a one row bar and no hint line, and a narrow one gives its side columns to the "..." drop-down of the bar.
    private void FitToArea()
    {
        if (Bounds.Width <= 0) return;
        var frames = Bounds.Width >= 560;
        var side = Bounds.Width >= 320;
        var compact = Bounds.Height < 360;
        _bar.MaxRows = compact ? 1 : 2;
        _hintSlot.IsVisible = !compact;
        _bar.SetExtras([.. frames ? [] : new Control[] { _framesHost }, .. side ? [] : new Control[] { _sideHost }]);
        Fill(_framesSlot, _framesHost, frames);
        Fill(_sideSlot, _sideHost, side);
    }

    private static void Fill(Decorator slot, Control content, bool show)
    {
        if (!show) slot.Child = null;
        else if (slot.Child != content) slot.Child = content;
        slot.IsVisible = show;
    }

    private static Button Small(string name, string text, Action click)
    {
        var button = new Button { Name = name, Content = text, Padding = new Thickness(6, 2) };
        if (name == "ImageZoomOut") button.Labeled("rooms.zoomOut");
        if (name == "ImageZoomIn") button.Labeled("rooms.zoomIn");
        if (name == "LayerAdd") button.Labeled("cmd.img.layerAdd");
        if (name == "LayerUp") button.Labeled("cmd.img.layerUp");
        if (name == "LayerDown") button.Labeled("cmd.img.layerDown");
        button.Click += (_, _) => click();
        return button;
    }

    private OverflowBar Options()
    {
        _size.ValueChanged += (_, e) =>
        {
            Canvas.BrushSize = (int)(e.NewValue ?? 1);
            _sizeSlider.Value = Math.Min(_sizeSlider.Maximum, Canvas.BrushSize);
        };
        _sizeSlider.ValueChanged += (_, e) =>
        {
            if ((int)Math.Round(e.NewValue) != Canvas.BrushSize) _size.Value = (int)Math.Round(e.NewValue);
        };
        foreach (var style in Enum.GetValues<ShapeStyle>()) _style.Items.Add(Loc.Current["image.style." + style]);
        _style.SelectedIndex = 0;
        _style.SelectionChanged += (_, _) =>
        {
            Canvas.Style = (ShapeStyle)Math.Max(0, _style.SelectedIndex);
            ShowShapeColours();
        };
        _tolerance.ValueChanged += (_, e) => Canvas.Tolerance = (int)(e.NewValue ?? 0);
        _contiguous.Content = Loc.Current["image.contiguous"];
        _contiguous.IsCheckedChanged += (_, _) => Canvas.Contiguous = _contiguous.IsChecked == true;
        _smoothToggle = Toggle("ImageSmooth", "image.smooth", on => Canvas.Smooth = on);
        _mirrorX = Toggle("ImageMirrorX", "image.symmetryH", on => Canvas.Symmetry = on ? Canvas.Symmetry | SymmetryMode.Horizontal : Canvas.Symmetry & ~SymmetryMode.Horizontal);
        _mirrorY = Toggle("ImageMirrorY", "image.symmetryV", on => Canvas.Symmetry = on ? Canvas.Symmetry | SymmetryMode.Vertical : Canvas.Symmetry & ~SymmetryMode.Vertical);
        _gridToggle = Toggle("ImageGrid", "image.grid", on => Canvas.ShowGrid = on, true);
        _tilesToggle = Toggle("ImageTiles", "image.tiles", on => Canvas.TilePreview = on);
        _onionToggle = Toggle("ImageOnion", "image.onion", on =>
        {
            _onionOn = on;
            UpdateOnion();
        });
        _undoButton.Content = new PathIcon { Data = Geometry.Parse(ImageTools.UndoIcon) };
        _redoButton.Content = new PathIcon { Data = Geometry.Parse(ImageTools.RedoIcon) };
        // Set before the first gate: a disabled button shows why, and shows this tip again once it is enabled. The icon has no text, so the name is set too.
        _undoButton.Labeled("image.undo", Tip(Loc.Current["image.undo"], "edit.undo", "Ctrl+Z"));
        _redoButton.Labeled("image.redo", Tip(Loc.Current["image.redo"], "edit.redo", "Ctrl+Y"));
        ToolTip.SetTip(_applyButton, _session is TargetImageSession ? Tip(Loc.Current["image.apply"], "file.save", "Ctrl+S") : Loc.Current["image.applyTip"]);
        _undoButton.Click += (_, _) => Undo();
        _redoButton.Click += (_, _) => Redo();
        _applyButton.Content = Loc.Current["image.apply"];
        _applyButton.Click += (_, _) => ApplyChanges();
        _applyButton.CornerRadius = new CornerRadius(4, 0, 0, 4);
        BuildApplyMenu();
        _originalButton.Content = new PathIcon { Data = Geometry.Parse("M10 4H4v16h16v-6h-2v4H6V6h4zM13 4v2h3.6L9 13.6 10.4 15 18 7.4V11h2V4z"), Width = 14, Height = 14 };
        _originalButton.Labeled("image.goOriginal", Tip(Loc.Current["image.goOriginal"], "img.goToOriginal"));
        _originalButton.Click += (_, _) => OriginalRequested?.Invoke();
        _pendingMark.Text = Loc.Current["image.unsaved"];
        var zoom = new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Children = { Small("ImageZoomOut", "-", () => Canvas.Step(-1)), Small("ImageZoomIn", "+", () => Canvas.Step(1)), Small("ImageFit", Loc.Current["image.fit"], Canvas.Fit).Labeled("image.fit", Tip(Loc.Current["image.fit"], "img.zoomFit")), Small("ImageActual", "1:1", Canvas.ActualSize).Labeled("cmd.img.zoomActual", Tip(Loc.Current["cmd.img.zoomActual"], "img.zoomActual")) } };
        // One group per task. The bar has at most two rows and the groups that do not fit go into its "..." drop-down; the tool options show for the tool in use only.
        static WrapPanel Group(params Control[] items)
        {
            var group = new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Margin = new Thickness(0, 0, 8, 0) };
            foreach (var item in items)
            {
                item.VerticalAlignment = VerticalAlignment.Center;
                group.Children.Add(item);
            }

            return group;
        }

        _originalGroup = Group(_originalButton);
        _originalGroup.IsVisible = false; // an empty group would sit on the bar with no size
        _bar.Add(_originalGroup);
        foreach (var menu in Menus()) _bar.Add(Group(menu));
        _brushGroup = Group(Label("image.sizeLabel"), _size, _sizeSlider, _smoothToggle);
        _outlineKey = ColourKey(_outlineSwatch, "image.outlineColour");
        _fillKey = ColourKey(_fillSwatch, "image.fillColour");
        _styleGroup = Group(_style, _outlineKey, _fillKey);
        _toleranceGroup = Group(Label("image.tolerance"), _tolerance, _contiguous);
        _textGroup = Group(Label("image.textLabel"), _text, _textSize);
        _discardButton.Content = Loc.Current["image.discard"];
        _discardButton.Labeled("image.discard", Tip(Loc.Current["image.discardTip"], "img.discard"));
        _discardButton.Click += (_, _) => DiscardChanges();
        _changesGroup = Group(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _applyButton, _applyMore } }, _discardButton);
        _changesGroup.IsVisible = _session is TargetImageSession or WorkspaceImageSession or DeferredImageSession;
        foreach (var group in new Control[]
        {
            Group(_undoButton, _redoButton),
            _changesGroup,
            Group(zoom),
            _brushGroup, _styleGroup, _toleranceGroup, _textGroup,
            Group(_mirrorX, _mirrorY, _gridToggle, _tilesToggle, _onionToggle),
        }) _bar.Add(group);

        RefreshButtons();
        return _bar;
    }

    private static TextBlock Label(string key) => new() { Text = Loc.Current[key], Classes = { "muted" } };

    private static ToggleButton Toggle(string name, string key, Action<bool> changed, bool on = false)
    {
        var button = new ToggleButton { Name = name, Content = Loc.Current[key], IsChecked = on };
        button.IsCheckedChanged += (_, _) => changed(button.IsChecked == true);
        return button;
    }

    public void SetSize(int size) => _size.Value = Math.Clamp(size, 1, 256);

    public void SetStyle(ShapeStyle style) => _style.SelectedIndex = (int)style;

    private void RefreshButtons()
    {
        _undoButton.GateKey(_session.CanUndo ? null : "why.nothingToUndo");
        _redoButton.GateKey(_session.CanRedo ? null : "why.nothingToRedo");
        var pending = _session.Pending;
        _applyButton.GateKey(pending ? null : "why.noChanges");
        _discardButton.IsEnabled = pending;
        _pendingMark.IsVisible = pending;
        _allFrames.IsVisible = _session.FrameCount > 1 && Canvas.CanResize && _session.ResizeContext is not null;
    }

    // The arrow beside Apply: a new sprite from this frame or from every frame (a picture with one frame offers only the first).
    private void BuildApplyMenu()
    {
        _saveFrameItem = new MenuItem { Header = Loc.Current["image.saveAsNew"] };
        _saveFrameItem.Click += (_, _) => SaveAsNewRequested?.Invoke([_session.Read(Frame)]);
        ToolTip.SetTip(_saveFrameItem, Loc.Current["image.saveAsNewTip"]);
        _saveAllItem = new MenuItem { Header = Loc.Current["image.saveAllAsNew"] };
        _saveAllItem.Click += (_, _) => SaveAsNewRequested?.Invoke(_session.Pictures());
        ToolTip.SetTip(_saveAllItem, Loc.Current["image.saveAllAsNewTip"]);
        var flyout = new MenuFlyout { ItemsSource = new Control[] { _saveFrameItem, _saveAllItem }, Placement = PlacementMode.BottomEdgeAlignedRight };
        flyout.Opening += (_, _) => _saveAllItem.IsVisible = _session.FrameCount > 1;
        _applyMore.Content = new PathIcon { Data = Geometry.Parse("M4 8l8 8 8-8z"), Width = 8, Height = 8 };
        _applyMore.Flyout = flyout;
        _applyMore.Labeled("image.applyMore", Loc.Current["image.applyMore"]);
    }

    private StackPanel Toolbox()
    {
        var panel = new WrapPanel { Name = "ImageToolbox" };
        foreach (var tool in ImageTools.All)
        {
            var button = new ToggleButton { Name = "Tool_" + tool, Width = 44, Height = 40, Margin = new Thickness(1), Padding = new Thickness(0), Content = new PathIcon { Data = Geometry.Parse(ImageTools.Icon(tool)) } };
            button.Labeled("image.tool." + tool, Tip(Loc.Current["image.tool." + tool], ImageTools.CommandOf(tool)));
            button.Click += (_, _) =>
            {
                ChooseTool(tool, false); // the plain tool, like its key: a shape tool starts as an outline
                Canvas.Focus();
            };
            _toolButtons.Add((tool, button));
            panel.Children.Add(button);
        }

        return Section("image.toolbox", panel);
    }

    /// <summary>A right column panel that folds under its header, so a folded panel gives its room to the others.</summary>
    private static StackPanel Section(string key, Control body, bool open = true) => new() { Spacing = 2, Children = { new Expander { Name = "Section_" + key, IsExpanded = open, HorizontalAlignment = HorizontalAlignment.Stretch, Header = new TextBlock { Text = Loc.Current[key], FontWeight = FontWeight.SemiBold }, Content = body } } };

    private void BuildFrames()
    {
        var play = _playToggle = new ToggleButton { Name = "ImagePlay", Content = Loc.Current["image.play"], Padding = new Thickness(6, 2) };
        play.IsCheckedChanged += (_, _) =>
        {
            if (play.IsChecked == true && _session.FrameCount > 1) _play.Start();
            else _play.Stop();
        };
        _frames.Children.Add(new TextBlock { Text = Loc.Current["image.frames"], FontWeight = FontWeight.SemiBold });
        _frames.Children.Add(Small("ImagePrev", Loc.Current["image.prev"], () => ShowFrame(Frame - 1)));
        _frames.Children.Add(Small("ImageNext", Loc.Current["image.next"], () => ShowFrame(Frame + 1)));
        _frames.Children.Add(Small("ImageAddFrame", Loc.Current["image.addFrame"], () => ChangeFrames(ImageSession.AddBlankFrame)));
        _frames.Children.Add(Small("ImageDuplicateFrame", Loc.Current["image.duplicateFrame"], () => ChangeFrames(FrameListCommand.Duplicate)));
        _frames.Children.Add(Small("ImageRemoveFrame", Loc.Current["image.removeFrame"], () => ChangeFrames(FrameListCommand.Remove)));
        _frames.Children.Add(play);
        _frames.Children.Add(_thumbs);
        SyncFrames();
    }

    /// <summary>The strip of thumbnails: one button per frame, the current one marked; a click moves to that frame without leaving the editor.</summary>
    private void SyncFrames()
    {
        if (_thumbsStale || _thumbs.Children.Count != _session.FrameCount)
        {
            _thumbsStale = false;
            _thumbs.Children.Clear();
            for (var i = 0; i < _session.FrameCount; i++) _thumbs.Children.Add(Thumb(i, _session.Read(i)));
        }

        for (var i = 0; i < _thumbs.Children.Count; i++) ((Button)_thumbs.Children[i]).Classes.Set("active", i == Frame);
    }

    // The picture of the frame being edited changed: redraw only its thumbnail.
    private void UpdateThumb(int frame)
    {
        if (frame >= 0 && frame < _thumbs.Children.Count && !_thumbsStale) _thumbs.Children[frame] = Thumb(frame, Canvas.Picture.Copy());
    }

    private Button Thumb(int frame, PixelBuffer picture)
    {
        // A small picture grows by whole steps; a larger one shrinks to fit the cell.
        var longest = (double)Math.Max(1, Math.Max(picture.Width, picture.Height));
        var scale = longest > 56 ? 56 / longest : Math.Max(1, Math.Floor(56 / longest));
        var image = new Image { Source = ImageCanvas.Bitmap(picture), Width = picture.Width * scale, Height = picture.Height * scale, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapInterpolationMode(image, scale < 1 ? Avalonia.Media.Imaging.BitmapInterpolationMode.MediumQuality : Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        var cell = new Grid { Width = 58, Height = 58, Children = { new Chequer(), image } };
        image.HorizontalAlignment = HorizontalAlignment.Center;
        image.VerticalAlignment = VerticalAlignment.Center;
        var button = new Button
        {
            Name = "ImageFrame" + frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new StackPanel { Spacing = 2, Children = { cell, new TextBlock { Text = frame.ToString(System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Center, Classes = { "muted" } } } },
        };
        button.NamedFor("rooms.inspector.frame", frame.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ToolTip.SetTip(button, AutomationProperties.GetName(button));
        button.Click += (_, _) =>
        {
            ShowFrame(frame);
            Canvas.Focus();
        };
        return button;
    }

    private void ChangeFrames(string operation)
    {
        if (operation == FrameListCommand.Remove && _session.FrameCount < 2) return; // the last frame stays; the command refuses it
        try
        {
            if (RefuseStale() || !_session.ChangeFrames(operation, Frame)) return;
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            _status.Text = e.Message;
            return;
        }

        _thumbsStale = true;
        if (operation == ImageSession.AddBlankFrame) Frame++; // the new empty frame is the one to draw on
        Reload();
        ShowNotice();
    }
}
