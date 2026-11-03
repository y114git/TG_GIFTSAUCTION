using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Editor;

/// <summary>A whole texture page above its properties, scrollable at its real size; the picture comes in off the UI thread.</summary>
public sealed class TexturePagePreview : Border
{
    private readonly Image _image = new() { Stretch = Stretch.None, Name = "PageImage" };
    private readonly Rectangle _hover = new() { Name = "PageHover", StrokeThickness = 2, IsVisible = false, IsHitTestVisible = false, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };

    public TexturePagePreview(GameMakerEmbeddedTexture page, TextureBitmaps textures, EditHistory? history = null, IEnumerable<GameMakerTexturePageItem>? items = null)
    {
        Name = "TexturePagePreview";
        Height = 320;
        Child = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = Stack(page, items) };
        Ready = ShowAsync(page, textures);
        if (history is null) return;
        // A replaced page picture is a new object: drop the decoded copy and draw the new one.
        var shown = page.TextureData?.Image;
        void OnChanged()
        {
            if (ReferenceEquals(shown, page.TextureData?.Image)) return;
            shown = page.TextureData?.Image;
            textures.Forget(page);
            Ready = ShowAsync(page, textures);
        }

        DetachedFromVisualTree += (_, _) => history.Changed -= OnChanged;
        AttachedToVisualTree += (_, _) => history.Changed += OnChanged;
    }

    /// <summary>The smallest page item of the page that holds the point (x, y) of the picture, or null; the smallest because a frame can lie inside a larger one.</summary>
    public static GameMakerTexturePageItem? ItemAt(GameMakerEmbeddedTexture page, IEnumerable<GameMakerTexturePageItem> items, double x, double y) =>
        items.Where(i => ReferenceEquals(i.TexturePage, page) && x >= i.SourceX && y >= i.SourceY && x < i.SourceX + i.SourceWidth && y < i.SourceY + i.SourceHeight)
            .OrderBy(i => i.SourceWidth * i.SourceHeight).FirstOrDefault();

    // The picture with an outline of the item under the pointer and its place and size as the tooltip (UTMT-TEX-05).
    private Grid Stack(GameMakerEmbeddedTexture page, IEnumerable<GameMakerTexturePageItem>? items)
    {
        _hover.Bind(Shape.StrokeProperty, this.GetResourceObservable("Text.Accent"));
        var stack = new Grid { Children = { _image, _hover } };
        if (items is null) return stack;
        stack.PointerMoved += (_, e) =>
        {
            var at = e.GetPosition(_image);
            if (ItemAt(page, items, at.X, at.Y) is not { } item)
            {
                _hover.IsVisible = false;
                ToolTip.SetTip(stack, null);
                return;
            }

            (_hover.Margin, _hover.Width, _hover.Height, _hover.IsVisible) = (new Thickness(item.SourceX, item.SourceY, 0, 0), item.SourceWidth, item.SourceHeight, true);
            ToolTip.SetTip(stack, Loc.Current.Format("pages.item", ("x", item.SourceX), ("y", item.SourceY), ("width", item.SourceWidth), ("height", item.SourceHeight)));
        };
        stack.PointerExited += (_, _) => _hover.IsVisible = false;
        return stack;
    }

    /// <summary>The decoded page size, or null when the page has no picture.</summary>
    public (int Width, int Height)? PictureSize => _image.Source is { } source ? ((int)source.Size.Width, (int)source.Size.Height) : null;

    public Task Ready { get; private set; }

    private async Task ShowAsync(GameMakerEmbeddedTexture page, TextureBitmaps textures) => _image.Source = await textures.GetAsync(page);
}
