using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MStudio.Rooms;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

public sealed partial class SpriteFramesView
{
    /// <summary>Redraws after an edit or undo that changed the frame list or swapped a picture under this sprite.</summary>
    private void OnHistoryChanged()
    {
        foreach (var marker in _markers) marker.InvalidateVisual(); // an origin edit moves the crosshair
        ShowSpeed();
        var same = _shown.Length == _sprite.Textures.Count && _shownRects == Rects()
            && Enumerable.Range(0, _shown.Length).All(i => ReferenceEquals(_shown[i], PageImage(_sprite, i)) && ReferenceEquals(_items[i], _sprite.Textures[i].Texture));
        if (same) return;
        foreach (var entry in _sprite.Textures)
            if (entry.Texture?.TexturePage is { } page) _textures.Forget(page);
        Ready = RefillAsync();
    }

    // Where each frame sits on its page: a canvas change moves or resizes the item in place, so the item and the page image alone do not tell.
    private int _shownRects;

    private int Rects()
    {
        var hash = new HashCode();
        foreach (var t in _sprite.Textures)
            if (t.Texture is { } item) hash.Add((item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight));
        return hash.ToHashCode();
    }

    private async Task RefillAsync()
    {
        _strip.IsVisible = _sprite.Textures.Count > 1; // a single frame is the canvas itself
        _markers.Clear(); // the crosshairs of the frames that are about to be replaced
        // The same number of frames keeps the old pictures until the new ones are cut: no flash, and the scroll position stays.
        if (_strip.Children.Count != _sprite.Textures.Count)
        {
            _strip.Children.Clear();
            for (var i = 0; i < _sprite.Textures.Count; i++) _strip.Children.Add(EmptyFrame(i));
        }

        _shown = [.. Enumerable.Range(0, _sprite.Textures.Count).Select(i => PageImage(_sprite, i))];
        _items = [.. _sprite.Textures.Select(t => t.Texture)];
        _shownRects = Rects();
        var last = Math.Max(0, _sprite.Textures.Count - 1);
        _selected.RemoveWhere(i => i > last);
        if (_selected.Count == 0) _selected.Add(Math.Min(SelectedFrame, last));
        SelectedFrame = Math.Min(SelectedFrame, last);
        var chosen = Math.Min(Current, last);
        // The canvas follows the history by itself, so the frame is announced only when it moved.
        if (chosen != Current) Choose(chosen);
        else if (_sprite.Textures.Count > 0) Show(chosen);
        await FillAsync(_sprite, _textures, ++_fillRun);
    }

    // An older refill still waiting for a page decode ends when it wakes, so it never writes into the strip a newer one owns.
    private int _fillRun;

    private async Task FillAsync(GameMakerSprite sprite, TextureBitmaps textures, int run)
    {
        for (var i = 0; i < sprite.Textures.Count; i++)
        {
            var item = sprite.Textures[i].Texture;
            var bitmap = item is { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } ? await textures.GetAsync(page) : null;
            if (run != _fillRun) return;
            if (item is null || bitmap is null)
            {
                Blank(i); // no picture now: drop the old one the strip still shows
                continue;
            }

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

    private void Blank(int index)
    {
        if (_strip.Children.Count > index) _strip.Children[index] = EmptyFrame(index);
        Show(Current);
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
