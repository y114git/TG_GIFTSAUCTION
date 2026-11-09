using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The image editor: the tool options bar, a frame strip, the canvas, the Brushes, Colours, Toolbox and Layers panels and a status line, like GameMaker's image editor.
/// One view serves the document tab (a <see cref="WorkspaceImageSession"/>) and the painter window (a <see cref="LocalImageSession"/>); the drawing itself is
/// <see cref="ImageCanvas"/> and <see cref="ImageOps"/>. In a document tab a frame has layers (see <see cref="LayerStore"/>); the data file still holds the flat result of every edit.
/// Every tool and action is a command (<see cref="Run"/>), so keys, menus and the command palette all reach the same code.
/// </summary>
public sealed partial class ImageEditorView : DockPanel, IDisposable
{
    private const int RecentLimit = 16;

    private readonly ImageSession _session;
    private readonly ImageOptions _options;
    private readonly TextBlock _status = new() { Name = "ImageStatus", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _hint = new() { Name = "ImageHint", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly List<Rgba> _recent = [];
    private readonly DispatcherTimer _play = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool _onionOn;
    private bool _standalone = true;

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
        Canvas.Hovered += ShowStatus;
        Canvas.SelectionChanged += ShowStatus;
        Canvas.ViewChanged += ShowStatus;
        session.Changed += Reload;
        _play.Tick += (_, _) => ShowFrame((Frame + 1) % Math.Max(1, session.FrameCount));
        Focusable = true;
        BuildLayout();
        ApplyOptions();
        SetTool(ImageTool.Pencil);
        SetColour(new Rgba(0, 0, 0, 255), false);
        SetColour(new Rgba(255, 255, 255, 255), true);
        KeyDown += OnKey;
        DetachedFromVisualTree += (_, _) => _play.Stop();
        RefreshLayers();
    }

    public ImageCanvas Canvas { get; }

    public int Frame { get; private set; }

    public ImageTool Tool { get; private set; }

    /// <summary>The layer being drawn on, bottom is 0.</summary>
    public int ActiveLayer { get; private set; }

    public event Action<int>? FrameChanged;

    /// <summary>The shortcut of a command; the window sets this to its command registry, so rebinding in Settings reaches the editor. The default is the table of default keys.</summary>
    public Func<string, string?> Bindings
    {
        get;
        set
        {
            field = value;
            _standalone = false;
        }
    } = id => AppCommands.ImageCommands.FirstOrDefault(c => c.Id == id).Shortcut;

    /// <summary>The extra gestures of a command; the window sets this to its registry like <see cref="Bindings"/>.</summary>
    public Func<string, IEnumerable<string>> AliasBindings { get; set; } = id => AppCommands.ImageAliases.GetValueOrDefault(id) ?? [];

    private static bool ChangesCanvas(ImageEdit edit) => edit.Ops.Any(o => o.Kind is ImageOps.Resize or ImageOps.Crop or ImageOps.Trim or ImageOps.Rotate90 or ImageOps.Scale or ImageOps.Skew);

    private void OnEdited(ImageEdit edit)
    {
        _session.Commit(Frame, edit);
        // A canvas size change flattens the layers in the command, so the editor shows the flat result.
        if (Canvas.Layers.Count > 1 && ChangesCanvas(edit)) Reload();
        RefreshButtons();
        ShowStatus();
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
        ShowStatus();
    }

    public void ShowFrame(int frame)
    {
        if (frame == Frame || frame < 0 || frame >= _session.FrameCount) return;
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
        var at = cell is { } c ? $"({c.X}, {c.Y})   " : "";
        _status.Text = $"{at}{Loc.Current.Format("image.size", ("w", p.Width), ("h", p.Height))}   {Loc.Current["rooms.inspector.frame"]} {Frame + 1}/{Math.Max(1, _session.FrameCount)}   {Canvas.Zoom:0}x{selection}";
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox or NumericUpDown) return;
        foreach (var (id, _) in AppCommands.ImageCommands)
        {
            if (!new[] { Bindings(id) }.Concat(AliasBindings(id)).Any(g => Shortcuts.Parse(g) is { } gesture && gesture.Matches(e))) continue;
            e.Handled = Run(id);
            return;
        }

        // The painter window has no command registry, so undo and redo are fixed there.
        if (!_standalone || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (e.Key == Key.Z) Undo();
        else if (e.Key == Key.Y) Redo();
        else return;
        e.Handled = true;
    }

    private string Tip(string text, string id) => Bindings(id) is { } shortcut ? $"{text} ({shortcut})" : text;

    public void Dispose()
    {
        _play.Stop();
        _session.Changed -= Reload;
        Canvas.Dispose();
        (_session as IDisposable)?.Dispose();
    }
}
