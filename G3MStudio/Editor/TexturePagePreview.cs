using Avalonia.Controls;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>A whole texture page above its properties, scrollable at its real size; the picture comes in off the UI thread.</summary>
public sealed class TexturePagePreview : Border
{
    private readonly Image _image = new() { Stretch = Stretch.None, Name = "PageImage" };

    public TexturePagePreview(GameMakerEmbeddedTexture page, TextureBitmaps textures)
    {
        Name = "TexturePagePreview";
        Height = 320;
        Child = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = _image };
        Ready = ShowAsync(page, textures);
    }

    /// <summary>The decoded page size, or null when the page has no picture.</summary>
    public (int Width, int Height)? PictureSize => _image.Source is { } source ? ((int)source.Size.Width, (int)source.Size.Height) : null;

    public Task Ready { get; }

    private async Task ShowAsync(GameMakerEmbeddedTexture page, TextureBitmaps textures) => _image.Source = await textures.GetAsync(page);
}
