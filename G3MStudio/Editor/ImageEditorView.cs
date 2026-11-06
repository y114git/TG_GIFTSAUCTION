using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The image editor: the tool options bar, a frame strip, the canvas, the Brushes, Colours and Toolbox panels and a status line, like GameMaker's image editor.
/// One view serves the document tab (a <see cref="WorkspaceImageSession"/>) and the painter window (a <see cref="LocalImageSession"/>); the drawing itself is
/// <see cref="ImageCanvas"/> and <see cref="ImageOps"/>. Layers are not offered: the data file stores each frame as one flat bitmap.
/// </summary>
public sealed partial class ImageEditorView : DockPanel, IDisposable
{
    private const int RecentLimit = 16;

    private readonly ImageSession _session;
    private readonly TextBlock _status = new() { Name = "ImageStatus", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _hint = new() { Name = "ImageHint", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly List<Rgba> _recent = [];
    private readonly DispatcherTimer _play = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private bool _onionOn;

    public ImageEditorView(ImageSession session, int frame = 0)
    {
        _session = session;
        Frame = frame;
        Canvas = new ImageCanvas(new ImageState(session.Read(frame)));
        Canvas.Edited += OnEdited;
        Canvas.Picked += SetColour;
        Canvas.Hovered += ShowStatus;
        Canvas.SelectionChanged += ShowStatus;
        Canvas.ViewChanged += ShowStatus;
        session.Changed += Reload;
        _play.Tick += (_, _) => ShowFrame((Frame + 1) % Math.Max(1, session.FrameCount));
        Focusable = true;
        BuildLayout();
        SetTool(ImageTool.Pencil);
        SetColour(new Rgba(0, 0, 0, 255), false);
        SetColour(new Rgba(255, 255, 255, 255), true);
        KeyDown += OnKey;
        DetachedFromVisualTree += (_, _) => _play.Stop();
    }

    public ImageCanvas Canvas { get; }

    public int Frame { get; private set; }

    public ImageTool Tool { get; private set; }

    public event Action<int>? FrameChanged;

    private void OnEdited(ImageEdit edit)
    {
        _session.Commit(Frame, edit);
        RefreshButtons();
        ShowStatus();
    }

    /// <summary>The model changed behind the editor (undo, redo, another edit): read the frame again.</summary>
    private void Reload()
    {
        if (_session.FrameCount == 0) return;
        Frame = Math.Min(Frame, _session.FrameCount - 1);
        var picture = _session.Read(Frame);
        if (Canvas.State.Picture.Width != picture.Width || Canvas.State.Picture.Height != picture.Height) Canvas.SetSelection(null);
        Canvas.State.Picture = picture;
        Canvas.Upload(true);
        UpdateOnion();
        Canvas.InvalidateVisual();
        RefreshButtons();
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

    private void UpdateOnion() => Canvas.Onion = _onionOn ? [.. new[] { Frame - 1, Frame + 1 }.Where(f => f >= 0 && f < _session.FrameCount).Select(_session.Read)] : [];

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
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (control && e.Key == Key.Z) Undo();
        else if (control && e.Key == Key.Y) Redo();
        else if (control && e.Key == Key.A) Canvas.SetSelection(PixelSelection.All(Canvas.Picture.Width, Canvas.Picture.Height));
        else if (control && e.Key == Key.C) CopySelection(false);
        else if (control && e.Key == Key.X) CopySelection(true);
        else if (control && e.Key == Key.V) PasteClipboard();
        else if (control && e.Key == Key.I) Canvas.SetSelection(Canvas.State.Selection?.Inverted());
        else if (control && e.Key == Key.D) ChangeFrames(FrameListCommand.Duplicate);
        else if (!control && e.Key == Key.Escape) Canvas.SetSelection(null);
        else if (!control && e.Key == Key.Delete) Canvas.Run(new ImageOp(ImageOps.Clear));
        else if (!control && e.Key == Key.G) Canvas.ShowGrid = !Canvas.ShowGrid;
        else if (!control && e.Key == Key.X) SwapColours();
        else if (!control && e.Key is Key.OemPlus or Key.Add) SetSize(Canvas.BrushSize + 1);
        else if (!control && e.Key is Key.OemMinus or Key.Subtract) SetSize(Canvas.BrushSize - 1);
        else if (!control && e.Key == Key.D1) ShowFrame(Frame - 1);
        else if (!control && e.Key == Key.D2) ShowFrame(Frame + 1);
        else if (!control && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.C) Canvas.ActualSize();
        else if (!control && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.F) Canvas.Fit();
        else if (!control && ImageTools.FromKey(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Shift)) is { } tool) ChooseTool(tool, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        else return;
        e.Handled = true;
    }

    private void ChooseTool(ImageTool tool, bool filled)
    {
        SetTool(tool);
        if (tool is ImageTool.Rectangle or ImageTool.Ellipse or ImageTool.Polygon) SetStyle(filled ? ShapeStyle.Filled : ShapeStyle.Outline);
    }

    public void Dispose()
    {
        _play.Stop();
        _session.Changed -= Reload;
        Canvas.Dispose();
        (_session as IDisposable)?.Dispose();
    }
}
