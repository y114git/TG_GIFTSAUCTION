using Avalonia;
using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using G3MStudio.Commands;
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
        VerticalAlignment = VerticalAlignment.Top; // as tall as its rows and strip, which is more than a fixed cap when a narrow tab wraps the toolbars into more lines
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
        var editImage = EditorButtons.EditImage("SpriteEditImage", () => EditImageRequested?.Invoke(SelectedFrame)).Gate(_sprite.Textures.Count > 0 ? null : Loc.Current["why.noFrames"]);
        // The size of every frame, typed with an anchor; the frames find their room on the texture pages by themselves, and the dialog says how before it is applied.
        var resize = new Button { Name = "SpriteResize", Content = Loc.Current["cmd.edit.resizeSprite"] }.Gate(_sprite.Textures.Count > 0 ? null : Loc.Current["why.noFrames"]);
        resize.Click += (_, _) => ResizeRequested?.Invoke();
        var editRow = new WrapPanel { ItemSpacing = 6, LineSpacing = 4, Margin = new Thickness(8, 4, 8, 4), Children = { _add, _duplicate, _delete, _import, editImage, resize } };
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
        // The viewer scrolls the strip sideways and measures its content without a width limit, so the rows would never wrap: they are held to the visible width.
        PropertyChanged += (_, e) =>
        {
            if (e.Property != ViewportProperty) return;
            var limit = Viewport.Width > 16 ? Viewport.Width - 16 : double.PositiveInfinity;
            playbackRow.MaxWidth = editRow.MaxWidth = limit;
        };
        Show(0);
        Ready = RefillAsync();
        if ((history ?? workspace?.History) is { } changes) HistoryFollow.Of(this, changes, OnHistoryChanged);
    }

    /// <summary>Resize was pressed: the sprite editor asks for the new size of the frames.</summary>
    public event Action? ResizeRequested;

    /// <summary>Edit Image was pressed; the frame the image editor should open on.</summary>
    public event Action<int>? EditImageRequested;

    /// <summary>The frame the next replace applies to: the one last clicked.</summary>
    public int SelectedFrame { get; private set; }

    /// <summary>Every selected frame, in order.</summary>
    public IReadOnlyList<int> SelectedFrames => [.. _selected.Order()];

    /// <summary>True while the origin is locked: the property list then shows Origin X and Y read-only.</summary>
    public bool OriginLocked
    {
        get => _lock.IsChecked == true;
        set => _lock.IsChecked = value;
    }

    /// <summary>True while the onion skin is on.</summary>
    public bool Onion
    {
        get => _onion.IsChecked == true;
        set => _onion.IsChecked = value;
    }

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
        _playback.Interval = SpriteGif.FrameInterval(_sprite);
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
            case Key.A when e.KeyModifiers.HasFlag(Shortcuts.Command):
                _selected.UnionWith(Enumerable.Range(0, _sprite.Textures.Count));
                Choose(Current);
                break;
            case Key.D when e.KeyModifiers.HasFlag(Shortcuts.Command): _ = DuplicateAsync(); break;
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

    internal string? FrameCaption(int frame) => (_strip.Children.ElementAtOrDefault(frame) as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text;

    /// <summary>Completes when every frame has its picture, or is known to have none.</summary>
    public Task Ready { get; private set; }

    private static GMImage? PageImage(GameMakerSprite sprite, int frame) => sprite.Textures[frame].Texture?.TexturePage?.TextureData?.Image;
}
