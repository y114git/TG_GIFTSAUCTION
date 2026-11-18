using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The image editor: the tool options bar, a frame strip, the canvas, the Brushes, Colours, Toolbox and Layers panels and a status line, like GameMaker's image editor.
/// One view serves the tab (a <see cref="WorkspaceImageSession"/>) and the painter window (a <see cref="LocalImageSession"/>); the drawing itself is
/// <see cref="ImageCanvas"/> and <see cref="ImageOps"/>. In a tab a frame has layers (see <see cref="LayerStore"/>); the data file still holds the flat result of every edit.
/// Every tool and action is a command (<see cref="Run"/>), so keys, menus and the command palette all reach the same code.
/// </summary>
public sealed partial class ImageEditorView : DockPanel, IDisposable
{
    private const int RecentLimit = 16;

    private readonly ImageSession _session;
    private readonly ImageOptions _options;
    private readonly TextBlock _status = new() { Name = "ImageStatus", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ToggleButton _allFrames = new() { Name = "ImageAllFrames", IsVisible = false };
    private readonly TextBlock _hint = new() { Name = "ImageHint", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    // The pointer position and the handle hint change while the pointer moves, and they sit in the line under the canvas. Each shares its cell with an invisible
    // widest text, so the line keeps its size and the canvas above it never moves while the pointer is over the picture or its handles.
    private readonly TextBlock _cursor = new() { Name = "ImageCursor", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _dragHint = new() { Name = "ImageDragHint", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0, IsHitTestVisible = false };
    private readonly List<Rgba> _recent = [];
    private readonly DispatcherTimer _play = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool _onionOn;

    public ImageEditorView(ImageSession session, int frame = 0, ImageOptions? options = null)
    {
        _session = session;
        _options = options ?? new ImageOptions();
        Frame = frame;
        var layers = session.ReadLayers(frame);
        ActiveLayer = layers.Count - 1;
        Canvas = new ImageCanvas(new ImageState(layers.Layers[ActiveLayer].Pixels.Copy()));
        if (!layers.IsPlain) Canvas.SetLayers(layers, ActiveLayer);
        Canvas.Edited += OnEdited;
        Canvas.TextRequested += PlaceText;
        Canvas.Picked += SetColour;
        Canvas.Refused += key => _status.Text = Loc.Current[key];
        Canvas.Hovered += ShowStatus;
        Canvas.SelectionChanged += ShowStatus;
        Canvas.ViewChanged += ShowStatus;
        Canvas.ResizeDragging += ShowResizeDrag;
        Canvas.ResizeDropped += CommitCanvasSize;
        // The drag hint fades in over the tool hint in the same cell; only opacity changes, never the layout.
        Canvas.HandleHovered += over => (_hint.Opacity, _dragHint.Opacity) = over ? (0, 1) : (1, 0);
        _allFrames.Content = Loc.Current["image.allFramesToggle"];
        ToolTip.SetTip(_allFrames, Loc.Current["image.allFramesToggleTip"]);
        _allFrames.IsCheckedChanged += (_, _) => Canvas.AllFrames = _allFrames.IsChecked == true;
        Canvas.CanResize = session is not TargetImageSession { FixedSize: true };
        session.Changed += OnSessionChanged;
        session.PendingChanged += RefreshButtons;
        _play.Tick += (_, _) => ShowFrame((Frame + 1) % Math.Max(1, session.FrameCount));
        Focusable = true;
        BuildLayout();
        RefreshButtons(); // the All frames toggle is there from the start, so the picture never moves under the pointer
        ApplyOptions();
        SetTool(ImageTool.Pencil);
        SetColour(new Rgba(0, 0, 0, 255), false);
        SetColour(new Rgba(255, 255, 255, 255), true);
        KeyDown += OnKey;
        AddHandler(KeyDownEvent, OnSpaceDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnSpaceUp, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => _play.Stop();
        RefreshLayers();
    }

    /// <summary>The deferred session of a sprite tab (the window applies its edits before a save or an exit); null for the other sessions.</summary>
    internal DeferredImageSession? Deferred => _session as DeferredImageSession;

    public ImageCanvas Canvas { get; }

    public int Frame { get; private set; }

    public ImageTool Tool { get; private set; }

    /// <summary>The layer being drawn on, bottom is 0.</summary>
    public int ActiveLayer { get; private set; }

    public event Action<int>? FrameChanged;

    /// <summary>The Apply button was pressed: the window writes the picture back (only a session that applies at the end shows the button).</summary>
    public event Action? ApplyRequested;

    /// <summary>The button (or command) to go back to the resource this editor edits was used: the window opens that resource's tab.</summary>
    public event Action? OriginalRequested;

    /// <summary>Shows the go-to-original button; the window sets it for a picture whose resource has a tab of its own.</summary>
    public bool HasOriginal
    {
        get => _originalGroup.IsVisible;
        set => _originalGroup.IsVisible = _originalButton.IsVisible = value;
    }

    /// <summary>A new sprite was asked for: the picture of one frame or of every frame; the window asks for a name and makes the sprite.</summary>
    public event Action<IReadOnlyList<PixelBuffer>>? SaveAsNewRequested;

    /// <summary>True while edits made here are not applied yet.</summary>
    public bool HasPending => _session.Pending;

    /// <summary>Raised when <see cref="HasPending"/> may have changed.</summary>
    public event Action PendingChanged
    {
        add => _session.PendingChanged += value;
        remove => _session.PendingChanged -= value;
    }

    /// <summary>Asked when Apply finds that a frame with local edits changed elsewhere: Save applies anyway (the local picture wins), DoItLater drops the local edits, Cancel keeps them. Null counts as Cancel.</summary>
    public Func<Task<CloseChoice>>? ConflictAsker { get; set; }

    /// <summary>Keeps the edits: a target (page item, tile set) writes its picture through the window, a deferred sprite writes its local edits to the workspace; an immediate sprite already holds them, so they only stop counting as pending.</summary>
    public void ApplyChanges()
    {
        if (_session is DeferredImageSession) _ = ApplyChangesAsync();
        else if (_session is TargetImageSession) ApplyRequested?.Invoke();
        else _session.Accept();
    }

    /// <summary>Like <see cref="ApplyChanges"/> and tells whether the edits are applied (or dropped by the user's choice) when it returns; false keeps them pending.</summary>
    public async Task<bool> ApplyChangesAsync()
    {
        if (_session is not DeferredImageSession deferred)
        {
            ApplyChanges();
            return true;
        }

        try
        {
            if (deferred.Apply()) return true;
            // A frame changed elsewhere since it was first edited here: writing would overwrite that change, so the user decides.
            switch (await (ConflictAsker?.Invoke() ?? Task.FromResult(CloseChoice.Cancel)))
            {
                case CloseChoice.Save: return deferred.Apply(true);
                case CloseChoice.DoItLater: return deferred.Discard();
                default:
                    _status.Text = Loc.Current["image.conflictKept"];
                    return false;
            }
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
        {
            _status.Text = e.Message; // the step put everything back before it threw
            return false;
        }
    }

    // The status line says what the session did before the operation (it applied the pending edits first).
    private void ShowNotice()
    {
        if (_session.TakeNotice() is { } key) _status.Text = Loc.Current[key];
    }

    /// <summary>Drops the edits made since the editor opened or since the last Apply; false (with a message) when other edits came in between.</summary>
    public bool DiscardChanges()
    {
        if (_session.Discard()) return true;
        _status.Text = Loc.Current["image.discardBlocked"];
        return false;
    }

    /// <summary>The shortcut of a command; the window sets this to its command registry, so rebinding in Settings reaches the editor. The default is the table of default keys.</summary>
    public Func<string, string?> Bindings { get; set; } = id => AppCommands.ImageCommands.FirstOrDefault(c => c.Id == id).Shortcut;

    /// <summary>The extra gestures of a command; the window sets this to its registry like <see cref="Bindings"/>.</summary>
    public Func<string, IEnumerable<string>> AliasBindings { get; set; } = id => AppCommands.ImageAliases.GetValueOrDefault(id) ?? [];

    private static bool ChangesCanvas(ImageEdit edit) => edit.Ops.Any(o => o.Kind is ImageOps.Resize or ImageOps.Crop or ImageOps.Trim or ImageOps.Rotate or ImageOps.Rotate90 or ImageOps.Scale or ImageOps.Skew);

    // The sprite changed behind this editor and the reload is still on its way: an edit made now was drawn on an old picture, so it is not written over the newer data.
    private bool RefuseStale()
    {
        if (!_session.Behind) return false;
        Reload();
        _status.Text = Loc.Current["image.stale"];
        return true;
    }

    private void OnEdited(ImageEdit edit)
    {
        if (RefuseStale()) return;
        _session.Commit(Frame, edit);
        UpdateThumb(Frame);
        // A canvas size change flattens the layers in the command, so the editor shows the flat result.
        if (Canvas.Layers.Count > 1 && ChangesCanvas(edit)) Reload();
        RefreshButtons();
        ShowStatus();
    }

    private void OnSessionChanged()
    {
        _thumbsStale = true;
        Reload();
    }

    /// <summary>The model changed behind the editor (undo, redo, another edit): read the frame again.</summary>
    private void Reload()
    {
        if (_session.FrameCount == 0) return;
        Frame = Math.Min(Frame, _session.FrameCount - 1);
        var layers = _session.ReadLayers(Frame);
        ActiveLayer = Math.Clamp(ActiveLayer, 0, layers.Count - 1);
        if (Canvas.State.Picture.Width != layers.Width || Canvas.State.Picture.Height != layers.Height) Canvas.SetSelection(null);
        Canvas.SetLayers(layers, ActiveLayer);
        UpdateOnion();
        Canvas.InvalidateVisual();
        RefreshButtons();
        RefreshLayers();
        SyncFrames();
        ShowStatus();
    }

    public void ShowFrame(int frame)
    {
        if (frame == Frame || frame < 0 || frame >= _session.FrameCount) return;
        _endRename?.Invoke(); // a layer name being typed goes to the frame it was typed on
        Frame = frame;
        Canvas.SetSelection(null);
        Reload();
        FrameChanged?.Invoke(frame);
    }

    private void UpdateOnion()
    {
        var around = Enumerable.Range(1, _options.OnionFrames).SelectMany(n => new[] { Frame - n, Frame + n });
        Canvas.Onion = _onionOn ? [.. around.Where(f => f >= 0 && f < _session.FrameCount).Select(_session.Read)] : [];
    }

    public void Undo()
    {
        _session.Undo();
        Reload();
    }

    public void Redo()
    {
        _session.Redo();
        Reload();
    }

    public void SetTool(ImageTool tool)
    {
        Tool = tool;
        Canvas.CancelPoints();
        Canvas.Tool = tool;
        foreach (var (other, button) in _toolButtons) button.IsChecked = other == tool;
        _hint.Text = Loc.Current["image.hint." + tool];
        _dragHint.Text = ResizeScope.DragHint(_session.FrameCount > 1 && Canvas.CanResize);
        ToolTip.SetTip(_status, _hint.Text); // the hint line is hidden in a short editor
        ShowOptions();
        _lastMenuCommand = null;
        MarkMenus();
    }

    // Only the options the tool in use reads are on the bar, so a long language does not fill it with controls that do nothing.
    private void ShowOptions()
    {
        _brushGroup.IsVisible = Tool is ImageTool.Pencil or ImageTool.Eraser or ImageTool.Line or ImageTool.Rectangle or ImageTool.Ellipse or ImageTool.Polygon or ImageTool.Curve;
        _styleGroup.IsVisible = Tool is ImageTool.Rectangle or ImageTool.Ellipse or ImageTool.Polygon;
        _toleranceGroup.IsVisible = Tool is ImageTool.Fill or ImageTool.Replace or ImageTool.Remove or ImageTool.Wand;
        _contiguous.IsVisible = Tool is ImageTool.Fill or ImageTool.Wand;
        _textGroup.IsVisible = Tool is ImageTool.Text;
        ShowShapeColours();
    }

    public void SetColour(Rgba colour, bool secondary)
    {
        if (secondary) Canvas.Secondary = colour;
        else Canvas.Primary = colour;
        if (!_recent.Contains(colour)) _recent.Insert(0, colour);
        if (_recent.Count > RecentLimit) _recent.RemoveAt(_recent.Count - 1);
        ShowColours();
    }

    private void ShowStatus() => ShowStatus(null);

    private void ShowStatus((int X, int Y)? cell)
    {
        var p = Canvas.Picture;
        var selection = Canvas.State.Selection?.Bounds() is { } box ? $"   {Loc.Current["image.selection"]} {box.Width}x{box.Height}" : "";
        _cursor.Text = cell is { } c ? $"({c.X}, {c.Y})" : "";
        _status.Text = $"{Loc.Current.Format("image.size", ("w", p.Width), ("h", p.Height))}   {Loc.Current["rooms.inspector.frame"]} {Frame + 1}/{Math.Max(1, _session.FrameCount)}   {Canvas.ZoomText}{selection}{(_session is TargetImageSession { Note: { } note } ? "   " + note : "")}";
    }

    private void OnKey(object? sender, KeyEventArgs e) => e.Handled |= HandleKey(e);

    private bool _spaceHeld;

    // Space over the canvas is the hand grip whichever control has the focus: a focused toolbar button must not take it as a click, and no stroke starts while it is down.
    private void OnSpaceDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.Source is TextBox or NumericUpDown || !Canvas.IsPointerOver) return;
        _spaceHeld = true;
        Canvas.HoldSpace(true);
        e.Handled = true;
    }

    private void OnSpaceUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !_spaceHeld) return;
        _spaceHeld = false;
        Canvas.HoldSpace(false);
        e.Handled = true;
    }

    /// <summary>
    /// Runs the key as an editor command when it is one, and returns true when it did. The window calls this first for a key pressed inside the editor,
    /// so its own undo, redo, save and the like act on the picture here and not on the data file.
    /// </summary>
    public bool HandleKey(KeyEventArgs e)
    {
        if (e.Source is TextBox or NumericUpDown) return false;
        foreach (var (id, _) in AppCommands.ImageCommands)
        {
            if (!new[] { Bindings(id) }.Concat(AliasBindings(id)).Any(g => Shortcuts.Parse(g) is { } gesture && gesture.Matches(e))) continue;
            return Run(id);
        }

        // The edit chords are the window's own commands (the user's keys for them count); the painter window has no registry, so the defaults hold there.
        bool Is(string id, string fallback, params string[] more) => new[] { Bindings(id) ?? fallback }.Concat(AliasBindings(id)).Concat(more).Any(g => Shortcuts.Parse(g) is { } gesture && gesture.Matches(e));
        if (Is("edit.undo", "Ctrl+Z")) Undo();
        else if (Is("edit.redo", "Ctrl+Y", "Ctrl+Shift+Z")) Redo();
        else if (Is("edit.selectAll", "Ctrl+A")) Run("img.selectAll");
        else if (Is("file.save", "Ctrl+S") && _session is TargetImageSession { Dirty: true }) ApplyRequested?.Invoke();
        else if (Is("file.save", "Ctrl+S") && _session is DeferredImageSession { Pending: true }) ApplyChanges();
        else if (Is("edit.rename", "F2") && _session.SupportsLayers) BeginRename(ActiveLayer); // the rename key renames the active layer here, not the sprite
        else return false;
        return true;
    }

    private string Tip(string text, string id, string? fallback = null) => (Bindings(id) ?? fallback) is { } shortcut ? $"{text} ({Shortcuts.Display(shortcut)})" : text;

    public void Dispose()
    {
        _play.Stop();
        _session.Changed -= OnSessionChanged;
        _session.PendingChanged -= RefreshButtons;
        Canvas.Dispose();
        (_session as IDisposable)?.Dispose();
    }
}
