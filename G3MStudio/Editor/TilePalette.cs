using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using G3MLib.DataFile.Models;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>The picture of a tile set with a click to choose a tile. Index 0 is the empty tile; the tile set's own border and column count give the cell of each index.</summary>
public sealed class TilePalette : ScrollViewer
{
    private readonly GameMakerBackground _set;
    private readonly Canvas _canvas = new();
    private readonly Border _marker = new() { BorderBrush = Brushes.DeepSkyBlue, BorderThickness = new Thickness(2), IsHitTestVisible = false };

    public TilePalette(GameMakerBackground set, TextureBitmaps textures)
    {
        _set = set;
        Name = "TilePalette";
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        Content = _canvas;
        Ready = ShowAsync(textures);
        PointerPressed += OnPointer;
        Select(1);
    }

    public int SelectedIndex { get; private set; }

    /// <summary>Mirror (bit 0), flip (bit 1) and quarter turn (bit 2) put on tiles painted from this palette.</summary>
    public int Flags { get; set; }

    /// <summary>The value to store in a tile map cell: the index with the flags in bits 28 to 30.</summary>
    public uint TileValue => (uint)SelectedIndex | (uint)(Flags & 7) << 28;

    public Task Ready { get; }

    private double CellWidth => _set.GMS2TileWidth + 2 * _set.GMS2OutputBorderX;

    private double CellHeight => _set.GMS2TileHeight + 2 * _set.GMS2OutputBorderY;

    private async Task ShowAsync(TextureBitmaps textures)
    {
        if (_set.Texture is not { TexturePage: { } page, SourceWidth: > 0, SourceHeight: > 0 } item || await textures.GetAsync(page) is not { } bitmap) return;
        var picture = new CroppedBitmap(bitmap, new PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight));
        var image = new Image { Source = picture, Width = item.SourceWidth, Height = item.SourceHeight, Stretch = Stretch.None };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        _canvas.Width = item.SourceWidth;
        _canvas.Height = item.SourceHeight;
        _canvas.Children.Clear();
        _canvas.Children.Add(image);
        _canvas.Children.Add(_marker);
        Select(SelectedIndex);
    }

    /// <summary>Chooses a tile by index and moves the marker to its cell.</summary>
    public void Select(int index)
    {
        SelectedIndex = Math.Max(0, index);
        var columns = Math.Max(1, (int)_set.GMS2TileColumns);
        _marker.Width = _set.GMS2TileWidth;
        _marker.Height = _set.GMS2TileHeight;
        Canvas.SetLeft(_marker, SelectedIndex % columns * CellWidth + _set.GMS2OutputBorderX);
        Canvas.SetTop(_marker, SelectedIndex / columns * CellHeight + _set.GMS2OutputBorderY);
    }

    private void OnPointer(object? sender, PointerPressedEventArgs e)
    {
        var at = e.GetPosition(_canvas);
        var columns = Math.Max(1, (int)_set.GMS2TileColumns);
        if (at.X < 0 || at.Y < 0 || CellWidth <= 0 || CellHeight <= 0) return;
        var index = (int)(at.Y / CellHeight) * columns + (int)(at.X / CellWidth);
        if (index < _set.GMS2TileCount) Select(index);
    }
}
