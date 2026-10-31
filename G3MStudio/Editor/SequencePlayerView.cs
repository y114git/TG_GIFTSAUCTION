using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Sequences;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>
/// The canvas of a sequence with a playhead: the sprites the sequence shows at the frame (see <see cref="SequenceEvaluator"/>) over a slider and a Play
/// button that steps at the playback speed of the sequence. Dragging the slider or clicking the ruler of the timeline moves the playhead.
/// </summary>
public sealed class SequencePlayerView : StackPanel
{
    private readonly Slider _slider = new() { Name = "SequenceFrame", Minimum = 0, SmallChange = 1, LargeChange = 5, TickFrequency = 1, IsSnapToTickEnabled = true, MinWidth = 240 };
    private readonly ToggleButton _play = new() { Name = "SequencePlay" };
    private readonly TextBlock _label = new() { Name = "SequenceFrameLabel", Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly DispatcherTimer _timer = new();

    public SequencePlayerView(GameMakerSequence sequence, TextureBitmaps textures)
    {
        Name = "SequencePlayer";
        Spacing = 4;
        Canvas = new SequenceCanvas(sequence, textures);
        var length = double.IsFinite(sequence.Length) ? Math.Clamp(sequence.Length, 1, 100_000) : 1;
        _slider.Maximum = length;
        // GameMaker's speed is frames per second, or frames per game frame at an assumed 30 game frames per second.
        var fps = double.IsFinite(sequence.PlaybackSpeed) ? Math.Clamp(sequence.PlaybackSpeed * (sequence.PlaybackSpeedType == AnimSpeedType.FramesPerGameFrame ? 30 : 1), 1, 240) : 30;
        _timer.Interval = TimeSpan.FromSeconds(1 / fps);
        _timer.Tick += (_, _) => Frame = Frame >= length ? 0 : Frame + 1;
        _play.Content = Loc.Current["sprites.play"];
        _play.IsCheckedChanged += (_, _) =>
        {
            _play.Content = Loc.Current[_play.IsChecked == true ? "sprites.pause" : "sprites.play"];
            if (_play.IsChecked == true) _timer.Start();
            else _timer.Stop();
        };
        _slider.ValueChanged += (_, e) => Frame = e.NewValue;
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        Children.Add(Canvas);
        Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _play, _slider, _label } });
        Show();
    }

    /// <summary>The whole sequence tab: the canvas with its playhead, the timeline (which draws the playhead and moves it on a click) and the tracks.</summary>
    public static StackPanel Tab(GameMakerSequence sequence, TextureBitmaps textures, Workspace? workspace)
    {
        var player = new SequencePlayerView(sequence, textures);
        var timeline = new SequenceTimelineView(sequence, workspace);
        player.FrameChanged += frame => timeline.Playhead = frame;
        timeline.PlayheadRequested += frame => player.Frame = frame;
        return new StackPanel { Spacing = 8, Children = { player, timeline, new SequenceTracksView(sequence, workspace) } };
    }

    /// <summary>Raised when the playhead moves, so the timeline can draw it.</summary>
    public event Action<double>? FrameChanged;

    /// <summary>The frame shown, 0 to the length of the sequence.</summary>
    public double Frame
    {
        get;
        set
        {
            var next = Math.Clamp(Math.Round(value), 0, _slider.Maximum);
            if (next == field) return;
            field = next;
            Show();
            FrameChanged?.Invoke(next);
        }
    }

    public SequenceCanvas Canvas { get; }

    private void Show()
    {
        if (_slider.Value != Frame) _slider.Value = Frame;
        _label.Text = $"{Loc.Current["rooms.inspector.frame"]} {Frame:0} / {_slider.Maximum:0}";
        Canvas.Frame = (float)Frame;
    }
}

/// <summary>Draws the sprites of a sequence at one frame around the origin of the sequence; the pictures come in as their texture pages are decoded.</summary>
public sealed class SequenceCanvas : Control
{
    private readonly GameMakerSequence _sequence;
    private readonly TextureBitmaps _textures;
    private readonly Dictionary<GameMakerSprite, CroppedBitmap?> _pictures = [];
    private Rect? _box;

    public SequenceCanvas(GameMakerSequence sequence, TextureBitmaps textures)
    {
        (_sequence, _textures) = (sequence, textures);
        Name = "SequenceCanvas";
        Height = 280;
        ClipToBounds = true;
        DetachedFromVisualTree += (_, _) =>
        {
            foreach (var picture in _pictures.Values) picture?.Dispose();
            _pictures.Clear();
        };
    }

    public float Frame
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    /// <summary>What is drawn now; a test reads it.</summary>
    public IReadOnlyList<SequenceSprite> Sprites => SequenceEvaluator.At(_sequence, Frame);

    private async Task LoadAsync(GameMakerSprite sprite)
    {
        _pictures[sprite] = null;
        if (sprite.Textures is not { Count: > 0 } || sprite.Textures[0].Texture is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item || await _textures.GetAsync(page) is not { } bitmap) return;
        _pictures[sprite] = new CroppedBitmap(bitmap, new PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight)); // shares the page bitmap, which the texture cache owns
        InvalidateVisual();
    }

    /// <summary>
    /// The area to show, relative to the origin of the sequence: its canvas frame when it has a size, otherwise the extent of its sprites over the whole sequence
    /// (sampled, rotation ignored). Computed once.
    /// shortcut: not computed again after an edit of the keyframes; reopen the tab, or add a refresh on the undo history.
    /// </summary>
    private Rect Box
    {
        get
        {
            if (_box is { } known) return known;
            if (_sequence.Width > 0 && _sequence.Height > 0 && float.IsFinite(_sequence.Width) && float.IsFinite(_sequence.Height))
                return (_box = new Rect(-_sequence.OriginX, -_sequence.OriginY, _sequence.Width, _sequence.Height)).Value;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            var length = double.IsFinite(_sequence.Length) ? Math.Clamp(_sequence.Length, 1, 100_000) : 1;
            for (var i = 0; i <= 40; i++)
                foreach (var shown in SequenceEvaluator.At(_sequence, (float)(length * i / 40)))
                {
                    double left = shown.X - _sequence.OriginX - (shown.Sprite.OriginX * shown.ScaleX), top = shown.Y - _sequence.OriginY - (shown.Sprite.OriginY * shown.ScaleY);
                    double right = left + (shown.Sprite.Width * shown.ScaleX), bottom = top + (shown.Sprite.Height * shown.ScaleY);
                    (minX, maxX, minY, maxY) = (Math.Min(minX, Math.Min(left, right)), Math.Max(maxX, Math.Max(left, right)), Math.Min(minY, Math.Min(top, bottom)), Math.Max(maxY, Math.Max(top, bottom)));
                }

            return (_box = maxX > minX && maxY > minY ? new Rect(minX, minY, maxX - minX, maxY - minY) : new Rect(-100, -100, 200, 200)).Value;
        }
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(24, 24, 28)), new Rect(Bounds.Size));
        var box = Box;
        var zoom = Math.Clamp(Math.Min((Bounds.Width - 16) / box.Width, (Bounds.Height - 16) / box.Height), 0.01, 1);
        // The middle of the area is the middle of the view; the origin of the sequence lands where it is in the area.
        var shift = new Point(Bounds.Width / 2, Bounds.Height / 2) - (new Vector(box.Center.X, box.Center.Y) * zoom);
        var guide = new Pen(new SolidColorBrush(Colors.White, 0.2), 1);
        context.DrawLine(guide, shift - new Vector(8, 0), shift + new Vector(8, 0));
        context.DrawLine(guide, shift - new Vector(0, 8), shift + new Vector(0, 8));
        if (_sequence.Width > 0 && _sequence.Height > 0 && float.IsFinite(_sequence.Width) && float.IsFinite(_sequence.Height))
            context.DrawRectangle(null, guide, new Rect(shift + (new Vector(-_sequence.OriginX, -_sequence.OriginY) * zoom), new Size(_sequence.Width * zoom, _sequence.Height * zoom)));
        foreach (var shown in Sprites)
        {
            if (!_pictures.TryGetValue(shown.Sprite, out var picture))
            {
                _ = LoadAsync(shown.Sprite);
                continue;
            }

            if (picture is null) continue;
            var matrix = Matrix.CreateTranslation(-shown.Sprite.OriginX, -shown.Sprite.OriginY) * Matrix.CreateScale(shown.ScaleX, shown.ScaleY) * Matrix.CreateRotation(-shown.Rotation * Math.PI / 180)
                * Matrix.CreateTranslation(shown.X - _sequence.OriginX, shown.Y - _sequence.OriginY) * Matrix.CreateScale(zoom, zoom) * Matrix.CreateTranslation(shift.X, shift.Y);
            using (context.PushTransform(matrix))
                context.DrawImage(picture, new Rect(picture.Size));
        }
    }
}
