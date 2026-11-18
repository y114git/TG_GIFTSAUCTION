using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>
/// Play, Stop, Loop and a speed choice under the picture of a sprite. Nothing plays by itself: the picture shows the first frame until Play is pressed. One timer steps through frame pictures
/// that are made once (each frame drawn from the shared texture pages into the sprite's bounding box), so a step only swaps the source of the image. Hiding the control (seen at the next tick) or leaving the tree stops the playback and shows the first frame again.
/// The state lives in memory only.
/// </summary>
public sealed class SpritePreviewPlayer : WrapPanel
{
    private static readonly double[] Speeds = [0.5, 1, 2];
    private static readonly string[] SpeedNames = ["0.5x", "1x", "2x"];

    private readonly TextureBitmaps _textures;
    private readonly DispatcherTimer _timer = new();
    private readonly Button _play = new() { Name = "ObjectPlay" }, _stop = new() { Name = "ObjectStop" };
    private readonly ToggleButton _loop = new() { Name = "ObjectLoop", IsChecked = true };
    private readonly ComboBox _speed = new() { Name = "ObjectSpeed", ItemsSource = SpeedNames, SelectedIndex = 1, MinWidth = 72 };
    private GameMakerSprite? _sprite;
    private Image? _target;
    private IImage? _rest;
    private RenderTargetBitmap?[]? _frames;
    private RenderTargetBitmap? _first;
    private Task<RenderTargetBitmap?[]>? _loading;

    // Counts the stops: a Play that waited for the pages starts only when no Stop came in between.
    private int _stops;

    public SpritePreviewPlayer(TextureBitmaps textures)
    {
        _textures = textures;
        (Name, ItemSpacing, LineSpacing) = ("ObjectPlayer", 4, 4);
        _timer.Tick += (_, _) => Step();
        _play.Click += (_, _) =>
        {
            if (IsPlaying) Pause();
            else _ = PlayAsync();
        };
        _stop.Click += (_, _) => Stop();
        _speed.SelectionChanged += (_, _) => SetInterval();
        _stop.Content = Loc.Current["editors.object.stop"];
        _loop.Content = Loc.Current["sprites.loop"];
        ToolTip.SetTip(_speed, Loc.Current["editors.object.speedTip"]);
        Children.Add(_play);
        Children.Add(_stop);
        Children.Add(_loop);
        Children.Add(_speed);
        ShowLabels();
        DetachedFromVisualTree += (_, _) => Stop();
    }

    public bool IsPlaying => _timer.IsEnabled;

    /// <summary>The frame shown now (0 while stopped).</summary>
    public int Frame { get; private set; }

    internal TimeSpan Interval => _timer.Interval;

    public bool Loop => _loop.IsChecked == true;

    public double SpeedFactor => Speeds[Math.Clamp(_speed.SelectedIndex, 0, Speeds.Length - 1)];

    /// <summary>Shows this sprite's frames in the image. Playback stops; the loop and speed choices stay.</summary>
    public void Bind(GameMakerSprite? sprite, Image? target)
    {
        Stop();
        // The frames are this player's own drawings and nothing shows them after Stop; a load still running is dropped when it ends.
        foreach (var frame in _frames ?? []) frame?.Dispose();
        _first?.Dispose();
        (_sprite, _target, _frames, _loading, _first, FirstShown) = (sprite, target, null, null, null, Task.CompletedTask);
        var many = sprite is { Textures.Count: > 1 } && target is not null;
        foreach (var control in new Control[] { _play, _stop, _loop, _speed }) control.GateKey(many ? null : "editors.object.oneFrame");
    }

    /// <summary>The picture of the first frame is on screen when this ends (drawn at its place in the bounding box, so a trimmed frame has its offsets).</summary>
    public Task FirstShown { get; private set; } = Task.CompletedTask;

    /// <summary>Shows the first frame exactly as the player draws every frame: the whole bounding box with the frame at its target offset, also for a sprite of one frame that never plays.</summary>
    public void ShowFirst() => FirstShown = ShowFirstAsync();

    private async Task ShowFirstAsync()
    {
        if (_sprite is not { Textures.Count: > 0 } sprite || _target is not { } target) return;
        if (sprite.Textures[0].Texture is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item || await _textures.GetAsync(page) is not { } bitmap) return;
        // The player may have been bound to another sprite, or started, while the page decoded.
        if (!ReferenceEquals(_sprite, sprite) || !ReferenceEquals(_target, target) || IsPlaying || _rest is not null) return;
#pragma warning disable CA2000 // owned by the player, disposed when it binds another sprite
        var picture = Draw(bitmap, item, sprite);
#pragma warning restore CA2000
        _first?.Dispose();
        (_first, target.Source) = (picture, picture);
    }

    public async Task PlayAsync()
    {
        if (IsPlaying || _sprite is not { Textures.Count: > 1 } sprite || _target is null) return;
        var loading = _loading ??= LoadAsync(sprite);
        var stops = _stops;
        RenderTargetBitmap?[] frames;
        try
        {
            frames = await loading;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Play is not awaited by the button: a page that does not decode leaves the first frame shown, and the next Play tries again.
            if (ReferenceEquals(loading, _loading)) _loading = null;
            ShowLabels();
            return;
        }

        // The sprite may have changed while the pages decoded: its frames are not shown.
        if (!ReferenceEquals(loading, _loading))
        {
            if (!ReferenceEquals(frames, _frames)) foreach (var frame in frames) frame?.Dispose();
            return;
        }

        _frames = frames;
        // Playback may have been asked for twice while the pages decoded.
        if (IsPlaying || !IsEffectivelyVisible || stops != _stops) return;
        _rest ??= _target.Source;
        // A playback that ended on the last frame starts over, and shows the first frame at once.
        if (Frame >= frames.Length - 1 && !Loop)
        {
            Frame = 0;
            _target.Source = frames[0] ?? _rest;
        }

        SetInterval();
        _timer.Start();
        ShowLabels();
    }

    public void Pause()
    {
        _timer.Stop();
        ShowLabels();
    }

    /// <summary>Stops and goes back to the first frame, which is the picture the image had before playing.</summary>
    public void Stop()
    {
        _stops++;
        _timer.Stop();
        Frame = 0;
        if (_rest is not null && _target is not null) _target.Source = _rest;
        _rest = null;
        ShowLabels();
    }

    /// <summary>One tick of the timer: the next frame, or the first after the last when looping; without a loop the playback ends on the last frame.</summary>
    public void Step()
    {
        if (!IsPlaying) return;
        // There is no change event for a tab that is hidden, so each tick looks.
        if (!IsEffectivelyVisible)
        {
            Stop();
            return;
        }

        if (_frames is not { Length: > 1 } frames || _target is null) return;
        var next = Frame + 1;
        if (next >= frames.Length)
        {
            if (!Loop)
            {
                Pause();
                return;
            }

            next = 0;
        }

        Frame = next;
        if (frames[next] is { } picture) _target.Source = picture;
    }

    private void SetInterval()
    {
        if (_sprite is null) return;
        _timer.Interval = SpriteGif.FrameInterval(_sprite, SpeedFactor);
    }

    private void ShowLabels() => _play.Content = Loc.Current[IsPlaying ? "sprites.pause" : "sprites.play"];

#pragma warning disable CA2000 // the frames are owned by the player and disposed when it binds another sprite
    private async Task<RenderTargetBitmap?[]> LoadAsync(GameMakerSprite sprite)
    {
        var pictures = new RenderTargetBitmap?[sprite.Textures.Count];
        foreach (var (i, frame) in sprite.Textures.Index())
            if (frame.Texture is { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item && await _textures.GetAsync(page) is { } bitmap)
                pictures[i] = Draw(bitmap, item, sprite);
        return pictures;
    }

    // A frame drawn at its place in the sprite's bounding box: a trimmed frame is smaller than the box and would wobble if shown alone.
    internal static RenderTargetBitmap Draw(Bitmap page, GameMakerTexturePageItem item, GameMakerSprite sprite)
    {
        var (width, height) = (item.BoundingWidth > 0 ? item.BoundingWidth : (int)sprite.Width, item.BoundingHeight > 0 ? item.BoundingHeight : (int)sprite.Height);
        var target = new RenderTargetBitmap(new PixelSize(Math.Max(1, width), Math.Max(1, height)));
        using var context = target.CreateDrawingContext(true);
        using var options = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
        var (drawnWidth, drawnHeight) = (item.TargetWidth > 0 ? item.TargetWidth : item.SourceWidth, item.TargetHeight > 0 ? item.TargetHeight : item.SourceHeight);
        context.DrawImage(page, new Rect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight), new Rect(item.TargetX, item.TargetY, drawnWidth, drawnHeight));
        return target;
    }
#pragma warning restore CA2000
}
