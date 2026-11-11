using Avalonia;
using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>
/// The frame strip of a sprite, like GameMaker's: every frame cut out of its texture page, in order, with the current one outlined and the selected ones tinted.
/// A click selects (Ctrl toggles, Shift extends), a drag reorders, and the buttons add, duplicate, delete and import frames; each action is one undoable step.
/// Above the strip are playback (play, loop, the sprite's speed), the onion skin and the origin lock. Every frame starts as an empty box; the pictures come in
/// as the pages are decoded off the UI thread (<see cref="Ready"/>), so opening a sprite never freezes the window.
/// </summary>
public sealed partial class SpriteFramesView : ScrollViewer
{
    // A subclass has no template of its own: without this it never draws a scrollbar or scrolls.
    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    private const double MaxSide = 56;
    private const double DragStart = 6;

    /// <summary>Applies an edit that may be refused with a warning the owner can overrule; set by the window. Without it the edit is made plainly.</summary>
    public static Func<Workspace, Func<bool, EditCommand>, string, Task<bool>>? RiskyEdit { get; set; }

    private readonly StackPanel _strip = new() { Name = "SpriteStrip", Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(8, 0, 8, 4) };
    private readonly Rectangle _dropMark = new() { Width = 3, IsVisible = false, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left };

    private readonly GameMakerSprite _sprite;
    private readonly TextureBitmaps _textures;
    private readonly Workspace? _workspace;
    private GMImage?[] _shown = [];
    private GameMakerTexturePageItem?[] _items = [];
    private readonly List<OriginMarker> _markers = [];
    private readonly HashSet<int> _selected = [0];
    private int _anchor;
    private (int Index, Point From, bool Moving, bool Collapse)? _drag;
    private bool _building;
    private readonly DispatcherTimer _playback = new();
    private readonly ToggleButton _play = new() { Name = "SpritePlay" };
    private readonly ToggleButton _loop = new() { Name = "SpriteLoop", IsChecked = true };
    private readonly ToggleButton _onion = new() { Name = "SpriteOnion" };
    private readonly NumericUpDown _speed = new() { Name = "SpriteSpeed", Minimum = 0.1m, Maximum = 1000, Increment = 1, FormatString = "0.##", MinWidth = 110 };
    private readonly TextBlock _speedUnit = new() { Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _add = new() { Name = "SpriteFrameAdd" };
    private readonly Button _duplicate = new() { Name = "SpriteFrameDuplicate" };
    private readonly Button _delete = new() { Name = "SpriteFrameDelete" };
    private readonly Button _import = new() { Name = "SpriteFrameImport" };
    private readonly ToggleButton _lock = new() { Name = "SpriteLockOrigin" };
    private readonly TextBlock _position = new() { Name = "SpritePosition", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };

    public SpriteFramesView(GameMakerSprite sprite, TextureBitmaps textures, EditHistory? history = null, Workspace? workspace = null)
    {
        (_sprite, _textures, _workspace) = (sprite, textures, workspace);
        Name = "SpriteFrames";
        Focusable = true;
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        MaxHeight = MaxSide + 150;
        _playback.Tick += (_, _) => Step();
        _play.Content = Loc.Current["sprites.play"];
        _play.IsCheckedChanged += (_, _) =>
        {
            _play.Content = Loc.Current[_play.IsChecked == true ? "sprites.pause" : "sprites.play"];
            if (_play.IsChecked == true && _sprite.Textures.Count > 1) _playback.Start();
            else _playback.Stop();
        };
        DetachedFromVisualTree += (_, _) => _playback.Stop();
        _loop.Content = Loc.Current["sprites.loop"];
        _onion.Content = Loc.Current["sprites.onion"];
        _onion.IsCheckedChanged += (_, _) => OnionChanged?.Invoke(_onion.IsChecked == true);
        _lock.Content = Loc.Current["sprites.lockOrigin"];
        _lock.IsCheckedChanged += (_, _) => OriginLockChanged?.Invoke();
        var speedLabel = new TextBlock { Text = Loc.Current["sprites.speed"], Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
        _speed.Labeled("sprites.speed").LabeledBy(speedLabel);
        _speed.ValueChanged += (_, _) => CommitSpeed();
        ShowSpeed();
        foreach (var (button, key, run) in new (Button, string, Action)[]
        {
            (_add, "sprites.addFrame", () => _ = AddAsync()), (_duplicate, "sprites.duplicateFrame", () => _ = DuplicateAsync()),
            (_delete, "sprites.deleteFrame", () => _ = DeleteAsync()), (_import, "sprites.importFrames", () => ImportRequested?.Invoke()),
        })
        {
            button.Content = Loc.Current[key];
            button.Click += (_, _) => run();
        }

        var playbackRow = new WrapPanel { ItemSpacing = 8, LineSpacing = 4, Margin = new Thickness(8, 4, 8, 0), Children = { _play, _loop, speedLabel, _speed, _speedUnit, _onion, _lock, _position } };
        var editRow = new WrapPanel { ItemSpacing = 6, LineSpacing = 4, Margin = new Thickness(8, 4, 8, 4), Children = { _add, _duplicate, _delete, _import } };
        foreach (var row in new[] { playbackRow, editRow })
            foreach (var item in row.Children) item.VerticalAlignment = VerticalAlignment.Center;
        _dropMark.Bind(Shape.FillProperty, this.GetResourceObservable("Text.Accent"));
        var strip = new Grid { Children = { _strip, _dropMark } };
        _strip.PointerPressed += OnStripPressed;
        _strip.PointerMoved += OnStripMoved;
        _strip.PointerReleased += OnStripReleased;
        _strip.PointerCaptureLost += (_, _) => EndDrag();
        KeyDown += OnKey;
        Content = new StackPanel { Children = { playbackRow, editRow, strip } };
        Show(0);
        Ready = RefillAsync();
        if ((history ?? workspace?.History) is not { } changes) return;
        changes.Changed += OnHistoryChanged;
        DetachedFromVisualTree += (_, _) => changes.Changed -= OnHistoryChanged;
    }

    /// <summary>The frame the next replace applies to: the one last clicked.</summary>
    public int SelectedFrame { get; private set; }

    /// <summary>Every selected frame, in order.</summary>
    public IReadOnlyList<int> SelectedFrames => [.. _selected.Order()];

    /// <summary>True while the origin is locked: the property list then shows Origin X and Y read-only.</summary>
    public bool OriginLocked => _lock.IsChecked == true;

    public event Action? OriginLockChanged;

    /// <summary>The onion skin was switched on or off; the canvas then draws the neighbouring frames faintly.</summary>
    public event Action<bool>? OnionChanged;

    /// <summary>The Import button was pressed; the window asks for a sheet, strip or GIF.</summary>
    public event Action? ImportRequested;

    /// <summary>The frame shown by playback, outlined in the strip.</summary>
    public int Current { get; private set; }

    /// <summary>The frame to show on the canvas changed: playback stepped or a thumbnail was clicked.</summary>
    public event Action<int>? FrameChosen;

    /// <summary>One tick of playback: the next frame, then the first again when looping, else a stop on the last.</summary>
    public void Step()
    {
        var count = Math.Max(1, _sprite.Textures.Count);
        if (Current + 1 >= count && _loop.IsChecked != true)
        {
            _play.IsChecked = false;
            return;
        }

        Pick((Current + 1) % count);
    }

    /// <summary>Selects one frame and shows it (a click, an arrow key, a playback step).</summary>
    public void Pick(int frame)
    {
        if (_sprite.Textures.Count == 0) return;
        frame = Math.Clamp(frame, 0, _sprite.Textures.Count - 1);
        _selected.Clear();
        _selected.Add(frame);
        (_anchor, SelectedFrame) = (frame, frame);
        Choose(frame);
    }

    /// <summary>Selects frames the way the mouse does: a plain click one frame, Ctrl toggles it, Shift extends from the first click.</summary>
    public void Select(int frame, KeyModifiers modifiers)
    {
        if (frame < 0 || frame >= _sprite.Textures.Count) return;
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            _selected.Clear();
            for (var i = Math.Min(_anchor, frame); i <= Math.Max(_anchor, frame); i++) _selected.Add(i);
        }
        else if (modifiers.HasFlag(KeyModifiers.Control))
        {
            if (!_selected.Remove(frame) || _selected.Count == 0) _selected.Add(frame);
            _anchor = frame;
        }
        else
        {
            Pick(frame);
            return;
        }

        SelectedFrame = frame;
        Choose(frame);
    }

    private void SetPlaybackRate()
    {
        // GameMaker's playback speed is frames per second, or frames per game frame at an assumed 30 game frames per second.
        _playback.Interval = TimeSpan.FromSeconds(1 / SpriteGif.FramesPerSecond(_sprite));
    }

    private void ShowSpeed()
    {
        _building = true;
        _speed.Value = (decimal)Math.Round(_sprite.GMS2PlaybackSpeed, 2);
        _speedUnit.Text = Loc.Current[_sprite.GMS2PlaybackSpeedType == AnimSpeedType.FramesPerGameFrame ? "sprites.perGameFrame" : "sprites.perSecond"];
        SetPlaybackRate();
        _building = false;
    }

    private void CommitSpeed()
    {
        if (_building || _workspace is null || _speed.Value is not { } value || (float)value == _sprite.GMS2PlaybackSpeed) return;
        var name = SpriteName;
        _ = EditAsync(_ => new SetPropertyCommand("sprites", name, "GMS2PlaybackSpeed", ((float)value).ToString(System.Globalization.CultureInfo.InvariantCulture)), name);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        var handled = true;
        switch (e.Key)
        {
            case Key.Left: Pick(Current - 1); break;
            case Key.Right: Pick(Current + 1); break;
            case Key.Space: _play.IsChecked = _play.IsChecked != true; break;
            case Key.Delete: _ = DeleteAsync(); break;
            case Key.A when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _selected.UnionWith(Enumerable.Range(0, _sprite.Textures.Count));
                Choose(Current);
                break;
            case Key.D when e.KeyModifiers.HasFlag(KeyModifiers.Control): _ = DuplicateAsync(); break;
            default: handled = false; break;
        }

        e.Handled = handled;
    }

    private int IndexAt(Point at)
    {
        for (var i = 0; i < _strip.Children.Count; i++)
            if (_strip.Children[i].Bounds.Inflate(new Thickness(_strip.Spacing / 2, 0)).Contains(at)) return i;
        return -1;
    }

    // The insertion point nearest to the pointer: before the first frame whose middle is right of it.
    private int DropIndex(Point at)
    {
        var index = _strip.Children.Count;
        for (var i = 0; i < _strip.Children.Count; i++)
            if (at.X < _strip.Children[i].Bounds.Center.X)
            {
                index = i;
                break;
            }

        return index;
    }

    private void OnStripPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        var point = e.GetCurrentPoint(_strip);
        var index = IndexAt(point.Position);
        if (!point.Properties.IsLeftButtonPressed || index < 0) return;
        // A press on a selected frame keeps the selection so it can be dragged as a group; it narrows on release when nothing was dragged.
        var inSelection = _selected.Contains(index) && _selected.Count > 1 && e.KeyModifiers == KeyModifiers.None;
        if (inSelection) (SelectedFrame, _anchor) = (index, index);
        else Select(index, e.KeyModifiers);
        _drag = (index, point.Position, false, inSelection);
        e.Pointer.Capture(_strip);
        e.Handled = true;
    }

    private void OnStripMoved(object? sender, PointerEventArgs e)
    {
        if (_drag is not { } drag) return;
        var at = e.GetPosition(_strip);
        if (!drag.Moving && Math.Abs(at.X - drag.From.X) < DragStart) return;
        _drag = drag with { Moving = true };
        var drop = DropIndex(at);
        var x = drop < _strip.Children.Count ? _strip.Children[drop].Bounds.Left - (_strip.Spacing / 2) : _strip.Children.LastOrDefault()?.Bounds.Right ?? 0;
        _dropMark.Margin = new Thickness(_strip.Margin.Left + x - 1, _strip.Margin.Top, 0, 0);
        _dropMark.Height = _strip.Children.FirstOrDefault()?.Bounds.Height ?? MaxSide;
        _dropMark.IsVisible = true;
    }

    private void OnStripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_drag is not { } drag) return;
        var at = e.GetPosition(_strip);
        _drag = null;
        _dropMark.IsVisible = false;
        e.Pointer.Capture(null);
        if (drag.Moving) _ = MoveSelectedAsync(DropIndex(at));
        else if (drag.Collapse) Pick(drag.Index);
    }

    private void EndDrag()
    {
        _drag = null;
        _dropMark.IsVisible = false;
    }

    private void Choose(int frame)
    {
        Show(frame);
        FrameChosen?.Invoke(frame);
    }

    private void Show(int frame)
    {
        Current = frame;
        _position.Text = $"{Loc.Current["rooms.inspector.frame"]} {frame + 1} / {_sprite.Textures.Count}";
        for (var i = 0; i < _strip.Children.Count; i++)
            if (_strip.Children[i] is StackPanel { Children: [Border cell, ..] })
            {
                cell.BorderThickness = new Thickness(i == frame ? 2 : 0);
                cell.BorderBrush = CanvasTheme.Handle;
                cell.Background = _selected.Contains(i) ? CanvasTheme.Selection : null;
            }
    }

    public int FrameCount => _strip.Children.Count;

    /// <summary>Frames that show a picture now.</summary>
    public int Pictures => _strip.Children.Count(c => c is StackPanel { Children: [Border { Child: Grid { Children: [Image, ..] } }, ..] });

    /// <summary>Completes when every frame has its picture, or is known to have none.</summary>
    public Task Ready { get; private set; }

    private static GMImage? PageImage(GameMakerSprite sprite, int frame) => sprite.Textures[frame].Texture?.TexturePage?.TextureData?.Image;

    /// <summary>Redraws after an edit or undo that changed the frame list or swapped a picture under this sprite.</summary>
    private void OnHistoryChanged()
    {
        foreach (var marker in _markers) marker.InvalidateVisual(); // an origin edit moves the crosshair
        ShowSpeed();
        var same = _shown.Length == _sprite.Textures.Count
            && Enumerable.Range(0, _shown.Length).All(i => ReferenceEquals(_shown[i], PageImage(_sprite, i)) && ReferenceEquals(_items[i], _sprite.Textures[i].Texture));
        if (same) return;
        foreach (var entry in _sprite.Textures)
            if (entry.Texture?.TexturePage is { } page) _textures.Forget(page);
        Ready = RefillAsync();
    }

    private async Task RefillAsync()
    {
        _strip.Children.Clear();
        _strip.IsVisible = _sprite.Textures.Count > 1; // a single frame is the canvas itself
        _markers.Clear(); // the crosshairs of the frames that were just removed
        for (var i = 0; i < _sprite.Textures.Count; i++) _strip.Children.Add(EmptyFrame(i));
        _shown = [.. Enumerable.Range(0, _sprite.Textures.Count).Select(i => PageImage(_sprite, i))];
        _items = [.. _sprite.Textures.Select(t => t.Texture)];
        var last = Math.Max(0, _sprite.Textures.Count - 1);
        _selected.RemoveWhere(i => i > last);
        if (_selected.Count == 0) _selected.Add(Math.Min(SelectedFrame, last));
        SelectedFrame = Math.Min(SelectedFrame, last);
        var chosen = Math.Min(Current, last);
        if (chosen != Current || _sprite.Textures.Count > 0) Choose(chosen);
        await FillAsync(_sprite, _textures);
    }

    private async Task FillAsync(GameMakerSprite sprite, TextureBitmaps textures)
    {
        for (var i = 0; i < sprite.Textures.Count; i++)
        {
            if (sprite.Textures[i].Texture is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item || await textures.GetAsync(page) is not { } bitmap) continue;
            var picture = new CroppedBitmap(bitmap, new PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight));
            // A small picture grows by whole steps; one larger than the cell shrinks to fit it instead of spilling out of it.
            var longest = (double)Math.Max(item.SourceWidth, item.SourceHeight);
            var scale = longest > MaxSide ? MaxSide / longest : Math.Max(1, Math.Floor(MaxSide / longest));
            var image = new Image { Source = picture, Width = item.SourceWidth * scale, Height = item.SourceHeight * scale, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(image, scale < 1 ? BitmapInterpolationMode.MediumQuality : BitmapInterpolationMode.None);
            // Every frame sits in a cell of the same size, so the captions line up whatever the picture size.
            image.HorizontalAlignment = HorizontalAlignment.Center;
            image.VerticalAlignment = VerticalAlignment.Center;
            var frame = new StackPanel { Spacing = 2, Children = { new Border { Width = MaxSide, Height = MaxSide, Child = new Grid { Children = { image, Marker(image, scale) } } }, new TextBlock { Text = $"{i}  {item.SourceWidth}x{item.SourceHeight}", Classes = { "muted" } } } };
            if (_strip.Children.Count > i) _strip.Children[i] = frame;
            Show(Current);
        }
    }

    private OriginMarker Marker(Image image, double scale)
    {
        var marker = new OriginMarker(_sprite, scale) { Width = image.Width, Height = image.Height, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        _markers.Add(marker);
        return marker;
    }

    private static StackPanel EmptyFrame(int index) => new()
    {
        Spacing = 2,
        Background = Brushes.Transparent, // an empty box takes the pointer too
        Children =
        {
            new Border { Width = 64, Height = 64, BorderThickness = new Thickness(2), BorderBrush = CanvasTheme.Muted },
            new TextBlock { Text = index.ToString(System.Globalization.CultureInfo.InvariantCulture), Classes = { "muted" } },
        },
    };
}

/// <summary>A crosshair at the origin of a sprite, drawn over a frame that is shown at a whole-number scale.</summary>
public sealed class OriginMarker(GameMakerSprite sprite, double scale) : Control
{
    public override void Render(DrawingContext context)
    {
        var at = new Point((sprite.OriginX + 0.5) * scale, (sprite.OriginY + 0.5) * scale);
        if (!new Rect(Bounds.Size).Inflate(8).Contains(at)) return;
        foreach (var (pen, reach) in new[] { (new Pen(Brushes.Black, 3), 6.5), (new Pen(CanvasTheme.Handle, 1), 5.5) })
        {
            context.DrawLine(pen, at - new Vector(reach, 0), at + new Vector(reach, 0));
            context.DrawLine(pen, at - new Vector(0, reach), at + new Vector(0, reach));
        }
    }
}
