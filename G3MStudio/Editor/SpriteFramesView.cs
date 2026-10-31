using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Util;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>
/// The frames of a sprite side by side, each cut out of its texture page. Every frame starts as an empty box; the pictures come in as the pages
/// are decoded off the UI thread (<see cref="Ready"/>), so opening a sprite never freezes the window.
/// </summary>
public sealed class SpriteFramesView : ScrollViewer
{
    private const double MaxSide = 160;

    private readonly StackPanel _strip = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8) };

    private readonly GameMakerSprite _sprite;
    private readonly TextureBitmaps _textures;
    private GMImage?[] _shown = [];
    private readonly DispatcherTimer _playback = new();
    private readonly ToggleButton _play = new() { Name = "SpritePlay" };
    private readonly TextBlock _position = new() { Name = "SpritePosition", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };

    public SpriteFramesView(GameMakerSprite sprite, TextureBitmaps textures, EditHistory? history = null)
    {
        (_sprite, _textures) = (sprite, textures);
        Name = "SpriteFrames";
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        MaxHeight = MaxSide + 96;
        // GameMaker's playback speed is frames per second, or frames per game frame at an assumed 30 game frames per second.
        var fps = Math.Max(1, _sprite.GMS2PlaybackSpeed * (_sprite.GMS2PlaybackSpeedType == AnimSpeedType.FramesPerGameFrame ? 30 : 1));
        _playback.Interval = TimeSpan.FromSeconds(1 / fps);
        _playback.Tick += (_, _) => Show((Current + 1) % Math.Max(1, _sprite.Textures.Count));
        _play.Content = Loc.Current["sprites.play"];
        _play.IsCheckedChanged += (_, _) =>
        {
            _play.Content = Loc.Current[_play.IsChecked == true ? "sprites.pause" : "sprites.play"];
            if (_play.IsChecked == true && _sprite.Textures.Count > 1) _playback.Start();
            else _playback.Stop();
        };
        DetachedFromVisualTree += (_, _) => _playback.Stop();
        Content = new StackPanel { Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 4), Children = { _play, _position } }, _strip } };
        Show(0);
        Ready = RefillAsync();
        if (history is null) return;
        history.Changed += OnHistoryChanged;
        DetachedFromVisualTree += (_, _) => history.Changed -= OnHistoryChanged;
    }

    /// <summary>The frame the next replace applies to; click a frame to choose it.</summary>
    public int SelectedFrame { get; private set; }

    /// <summary>The frame shown by playback, outlined in the strip.</summary>
    public int Current { get; private set; }

    private void Show(int frame)
    {
        Current = frame;
        _position.Text = $"{Loc.Current["rooms.inspector.frame"]} {frame + 1} / {_sprite.Textures.Count}";
        for (var i = 0; i < _strip.Children.Count; i++)
            if (_strip.Children[i] is StackPanel { Children: [Border cell, ..] }) cell.BorderThickness = new Thickness(i == frame ? 2 : 0);
    }

    public int FrameCount => _strip.Children.Count;

    /// <summary>Frames that show a picture now.</summary>
    public int Pictures => _strip.Children.Count(c => c is StackPanel { Children: [Border { Child: Image }, ..] });

    /// <summary>Completes when every frame has its picture, or is known to have none.</summary>
    public Task Ready { get; private set; }

    private static GMImage? PageImage(GameMakerSprite sprite, int frame) => sprite.Textures[frame].Texture?.TexturePage?.TextureData?.Image;

    /// <summary>Redraws after an edit or undo that swapped a page picture under this sprite.</summary>
    private void OnHistoryChanged()
    {
        if (_shown.Length == _sprite.Textures.Count && Enumerable.Range(0, _shown.Length).All(i => ReferenceEquals(_shown[i], PageImage(_sprite, i)))) return;
        for (var i = 0; i < _sprite.Textures.Count; i++)
            if (_sprite.Textures[i].Texture?.TexturePage is { } page) _textures.Forget(page);
        Ready = RefillAsync();
    }

    private async Task RefillAsync()
    {
        _strip.Children.Clear();
        for (var i = 0; i < _sprite.Textures.Count; i++) _strip.Children.Add(EmptyFrame(i));
        _shown = [.. Enumerable.Range(0, _sprite.Textures.Count).Select(i => PageImage(_sprite, i))];
        SelectedFrame = Math.Min(SelectedFrame, Math.Max(0, _sprite.Textures.Count - 1));
        await FillAsync(_sprite, _textures);
    }

    private async Task FillAsync(GameMakerSprite sprite, TextureBitmaps textures)
    {
        for (var i = 0; i < sprite.Textures.Count; i++)
        {
            if (sprite.Textures[i].Texture is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item || await textures.GetAsync(page) is not { } bitmap) continue;
            var picture = new CroppedBitmap(bitmap, new PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight));
            var scale = Math.Max(1, Math.Floor(MaxSide / Math.Max(item.SourceWidth, item.SourceHeight)));
            var image = new Image { Source = picture, Width = item.SourceWidth * scale, Height = item.SourceHeight * scale, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            var index = i;
            // Every frame sits in a cell of the same size, so the captions line up whatever the picture size.
            image.HorizontalAlignment = HorizontalAlignment.Center;
            image.VerticalAlignment = VerticalAlignment.Center;
            var frame = new StackPanel { Spacing = 2, Children = { new Border { Width = MaxSide, Height = MaxSide, Child = image }, new TextBlock { Text = $"{i}  {item.SourceWidth}x{item.SourceHeight}", Classes = { "muted" } } } };
            frame.PointerPressed += (_, _) => SelectedFrame = index;
            if (_strip.Children.Count > i) _strip.Children[i] = frame;
            Show(Current);
        }
    }

    private static StackPanel EmptyFrame(int index) => new()
    {
        Spacing = 2,
        Children =
        {
            new Border { Width = 64, Height = 64, BorderThickness = new Thickness(2), BorderBrush = Brushes.Gray },
            new TextBlock { Text = index.ToString(System.Globalization.CultureInfo.InvariantCulture), Classes = { "muted" } },
        },
    };
}
