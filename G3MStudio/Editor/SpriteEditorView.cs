using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The whole sprite tab header: playback and frame strip on top, then one zoomable canvas with the tools panel beside it when the tab is wide
/// and under it when narrow. The canvas never gets less than <see cref="MinCanvasHeight"/>; what does not fit scrolls.
/// </summary>
public sealed class SpriteEditorView : ScrollViewer
{
    // A subclass has no template of its own: without this it never draws a scrollbar or scrolls.
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    public const double MinCanvasHeight = 260;
    public const double WideWidth = 900;
    private const double PanelWidth = 420;
    private double ZoomBarHeight => this.Size("Control.Height", 30) + 6 + HintHeight; // the canvas between the bar and the hint keeps the full minimum
    private double BarHeight => this.Size("Control.Height", 30) + 6;
    private const double HintHeight = 40; // two lines of the canvas hint under the picture

    private static double _panelWidth = PanelWidth; // dragged width of the tools panel, kept for the session
    private readonly Grid _body = new();
    private readonly GridSplitter _splitter = new() { Width = 5, VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns };
    // The side panel scrolls on its own when its content is taller than the room beside the canvas.
    private readonly ScrollViewer _toolsHost = new() { Name = "SpriteToolsScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _zoomText = new() { VerticalAlignment = VerticalAlignment.Center, Classes = { "muted" } };
    private readonly Grid _canvasColumn = new() { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
    // Always shown, so the modifiers are known before the first drag and the picture never moves under the pointer when a hint appears.
    private readonly ToggleButton _allFrames = new() { Name = "SpriteAllFrames" };
    private readonly TextBlock _hint = new() { Name = "SpriteCanvasHint", Classes = { "muted" }, Margin = new Thickness(8, 4), TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public SpriteEditorView(GameMakerSprite sprite, TextureBitmaps textures, Workspace workspace)
    {
        Name = "SpriteEditor";
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Frames = new SpriteFramesView(sprite, textures, workspace.History, workspace);
        Tools = new SpriteToolsView(sprite, textures, workspace);
        Frames.EditRefused += message => Notify?.Invoke(NotificationLevel.Error, message);
        var zoomOut = new Button { Name = "SpriteZoomOut", Content = G3MStudio.Theme.GlyphIcons.Remove(), MinWidth = 32 }.Labeled("rooms.zoomOut");
        var zoomIn = new Button { Name = "SpriteZoomIn", Content = G3MStudio.Theme.GlyphIcons.Add(), MinWidth = 32 }.Labeled("rooms.zoomIn");
        var zoomText = _zoomText;
        void Zoom(int by) => SetZoom(Tools.Canvas.Zoom + by);

        zoomOut.Click += (_, _) => Zoom(-1);
        zoomIn.Click += (_, _) => Zoom(1);
        zoomText.Text = $"{Tools.Canvas.Zoom}x";
        var viewer = new ScrollViewer { Name = "SpriteCanvasHost", HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = Tools.Canvas };
        viewer.PointerWheelChanged += (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            Zoom(e.Delta.Y > 0 ? 1 : -1);
            e.Handled = true;
        };
        _allFrames.Content = Loc.Current["image.allFramesToggle"];
        _allFrames.IsVisible = sprite.Textures.Count > 1;
        ToolTip.SetTip(_allFrames, Loc.Current["image.allFramesToggleTip"]);
        _allFrames.IsCheckedChanged += (_, _) => Tools.Canvas.AllFrames = _allFrames.IsChecked == true;
        var zoomBar = new StackPanel { Height = BarHeight, Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0), Children = { zoomOut, zoomText, zoomIn, _allFrames } };
        Frames.ResizeRequested += () => _ = ResizeAsync(workspace, sprite);
        Tools.Canvas.ResizeDropped += spec => CommitCanvasSize(workspace, sprite, spec);
        _hint.Text = ResizeScope.DragHint(sprite.Textures.Count > 1);
        Tools.Canvas.ResizeDragging += spec => ShowDrag(sprite, spec);
        Tools.Canvas.ZoomChanged += () => _zoomText.Text = $"{Tools.Canvas.Zoom}x";
        Grid.SetRow(_hint, 2);
        _canvasColumn.Children.Add(_hint);
        Grid.SetRow(viewer, 1);
        _canvasColumn.Children.Add(zoomBar);
        _canvasColumn.Children.Add(viewer);
        _body.Children.Add(_canvasColumn);
        _toolsHost.Content = Tools;
        _body.Children.Add(_toolsHost);
        _body.Children.Add(_splitter);
        _splitter.DragCompleted += (_, _) =>
        {
            if (IsWide && _body.ColumnDefinitions.Count == 3) _panelWidth = _body.ColumnDefinitions[2].ActualWidth;
        };
        Content = new StackPanel { Children = { Frames, _body } };
        Frames.FrameChosen += Tools.ShowFrame;
        Frames.OnionChanged += on => Tools.Canvas.Onion = on;
        ArrangeParts(default);
        SizeChanged += (_, e) => ArrangeParts(e.NewSize);
        Frames.SizeChanged += (_, _) => ArrangeParts(Bounds.Size);
    }

    // While a handle is dragged the hint line shows the new size and its scope (the frame, or every frame with the sizes they have now); it comes back when the drag ends.
    private void ShowDrag(GameMakerSprite sprite, ResizeSpec? spec)
    {
        if (spec is null)
        {
            _hint.Text = ResizeScope.DragHint(sprite.Textures.Count > 1);
            return;
        }

        _hint.Text = Loc.Current.Format(spec.Scale ? "image.dragStretch" : "image.dragSize", ("w", spec.Width), ("h", spec.Height))
            + ResizeScope.DragSuffix(spec, Frames.SelectedFrame, FrameSizes(sprite));
    }

    private static IReadOnlyList<(int Width, int Height)> FrameSizes(GameMakerSprite sprite) =>
        [.. sprite.Textures.Select(t => (t.Texture?.TargetWidth ?? 0, t.Texture?.TargetHeight ?? 0))];

    /// <summary>Raised with each Resize dialog, so a test can drive it.</summary>
    public event Action<ResizeImageWindow>? ResizeOpened;

    /// <summary>The Resize dialog of the image editor for the frame in view, every frame only when chosen in the dialog, as one undoable edit.</summary>
    public async Task ResizeAsync(Workspace workspace, GameMakerSprite sprite)
    {
        if (sprite.Textures.Count == 0 || sprite.Name.Content is not { } name || TopLevel.GetTopLevel(this) is not Window owner) return;
        using var session = new WorkspaceImageSession(workspace, name);
        var frame = Math.Clamp(Frames.SelectedFrame, 0, sprite.Textures.Count - 1);
        if (session.ResizeContext is not { } context || sprite.Textures[frame].Texture is not { } item) return;
        var dialog = new ResizeImageWindow((item.TargetWidth, item.TargetHeight), context.Frames, context.Groups, context.Group, null,
            choice => session.PlanResize(frame, choice.Spec, choice.AllFrames, choice.Fit) is { } steps ? FitText.Describe(steps) : null, context.Pages, context.AutomaticMax, context.Box, frame, FrameSizes(sprite));
        ResizeOpened?.Invoke(dialog);
        if (await dialog.AskAsync(owner) is not { } chosen) return;
        try
        {
            session.CommitResize(frame, chosen.Spec, chosen.AllFrames, chosen.Group, chosen.Fit);
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            Notify?.Invoke(NotificationLevel.Error, e.Message); // the edit put everything back before it threw
        }
    }

    /// <summary>
    /// A canvas handle was released: the new size as one undoable edit for the selected frame, or for every frame when the all-frames toggle or key was on, the same code path as the image editor handles. With Ctrl the spec stretches the picture
    /// (the Resize dialog's remembered method and box choice). False when the size was not applied.
    /// </summary>
    private bool CommitCanvasSize(Workspace workspace, GameMakerSprite sprite, ResizeSpec spec)
    {
        if (sprite.Textures.Count == 0 || sprite.Name.Content is not { } name) return false;
        try
        {
            using var session = new WorkspaceImageSession(workspace, name);
            return session.CommitResize(Math.Clamp(Frames.SelectedFrame, 0, sprite.Textures.Count - 1), spec, spec.AllFrames || sprite.Textures.Count < 2, null, FitOptionsPanel.Current);
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            Notify?.Invoke(NotificationLevel.Error, e.Message); // the edit put everything back before it threw
            return false;
        }
        catch (Exception e)
        {
            // A drop runs inside the pointer event: nothing may escape to the dispatcher.
            FailureReport.Report(e, "sprite-resize-drop");
            Notify?.Invoke(NotificationLevel.Error, e.Message);
            return false;
        }
    }

    /// <summary>Shows why an edit of the sprite was refused.</summary>
    public Action<NotificationLevel, string>? Notify { get; init; }

    private void SetZoom(int zoom)
    {
        Tools.Canvas.Zoom = zoom;
        _zoomText.Text = $"{Tools.Canvas.Zoom}x";
    }

    /// <summary>What a rebuilt tab (a language change) must keep: zoom, the frame, onion skin and origin lock.</summary>
    public int[] ViewState => [Tools.Canvas.Zoom, Frames.SelectedFrame, Frames.Onion ? 1 : 0, Frames.OriginLocked ? 1 : 0];

    public async Task RestoreViewAsync(int[] state)
    {
        if (state.Length < 4) return;
        SetZoom(state[0]);
        await Frames.Ready;
        Frames.Pick(state[1]);
        (Frames.Onion, Frames.OriginLocked) = (state[2] == 1, state[3] == 1);
    }

    public SpriteFramesView Frames { get; }

    public SpriteToolsView Tools { get; }

    /// <summary>True when the tools panel sits right of the canvas.</summary>
    public bool IsWide { get; private set; }

    private void ArrangeParts(Size size)
    {
        IsWide = size.Width > WideWidth;
        _splitter.IsVisible = IsWide;
        _body.ColumnDefinitions.Clear();
        _body.RowDefinitions.Clear();
        var room = size.Height - Frames.Bounds.Height;
        if (IsWide)
        {
            _body.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star) { MinWidth = 240 });
            _body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            _body.ColumnDefinitions.Add(new ColumnDefinition(_panelWidth, GridUnitType.Pixel) { MinWidth = 240 });
            Grid.SetColumn(_splitter, 1);
            Grid.SetColumn(_toolsHost, 2);
            Grid.SetRow(_toolsHost, 0);
            _canvasColumn.Height = Math.Max(MinCanvasHeight + ZoomBarHeight, room);
            _toolsHost.Height = _canvasColumn.Height;
        }
        else
        {
            _body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(_toolsHost, 0);
            Grid.SetRow(_toolsHost, 1);
            _toolsHost.Height = double.NaN; // under the canvas the whole tab scrolls
            _canvasColumn.Height = Math.Max(MinCanvasHeight + ZoomBarHeight, room * 0.6);
        }
    }
}
