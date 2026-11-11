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

    private readonly TextBlock _size = new() { Name = "PageSize", Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

    // actions: export, replace and groups buttons above the picture; null shows none.
    public TexturePagePreview(GameMakerEmbeddedTexture page, TextureBitmaps textures, EditHistory? history = null, IEnumerable<GameMakerTexturePageItem>? items = null, (Action Export, Action Replace, Action Groups)? actions = null, Action? edit = null)
    {
        Name = "TexturePagePreview";
        Height = 350;
        var scroll = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = Stack(page, items) };
        var bar = new WrapPanel { ItemSpacing = 8, LineSpacing = 4, Margin = new Thickness(0, 0, 0, 4) };
        bar.Children.Add(new TextBlock { Name = "PageItemCount", Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Text = Loc.Current.Format("pages.itemCount", ("count", ItemsOn(page, items))) });
        if (actions is { } a)
            foreach (var (name, key, run) in new[] { ("PageExport", "pages.export", a.Export), ("PageReplace", "pages.replace", a.Replace), ("PageGroups", "pages.groups", a.Groups) })
            {
                var button = new Button { Name = name, Content = Loc.Current[key] };
                button.Click += (_, _) => run();
                bar.Children.Add(button);
            }

        if (edit is not null)
        {
            var paint = new Button { Name = "PageEditImage", Content = Loc.Current["textureItem.edit"] };
            paint.Click += (_, _) => edit();
            bar.Children.Add(paint);
            _image.DoubleTapped += (_, _) => edit();
        }

        bar.Children.Add(_size);
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        Child = new DockPanel { Children = { bar, scroll } };
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

    /// <summary>How many page items sit on the page.</summary>
    public static int ItemsOn(GameMakerEmbeddedTexture page, IEnumerable<GameMakerTexturePageItem>? items) => items?.Count(i => ReferenceEquals(i.TexturePage, page)) ?? 0;

    /// <summary>The smallest page item of the page that holds the point (x, y) of the picture, or null; the smallest because a frame can lie inside a larger one.</summary>
    public static GameMakerTexturePageItem? ItemAt(GameMakerEmbeddedTexture page, IEnumerable<GameMakerTexturePageItem> items, double x, double y) =>
        items.Where(i => ReferenceEquals(i.TexturePage, page) && x >= i.SourceX && y >= i.SourceY && x < i.SourceX + i.SourceWidth && y < i.SourceY + i.SourceHeight)
            .OrderBy(i => i.SourceWidth * i.SourceHeight).FirstOrDefault();

    // The picture with an outline of the item under the pointer and its place and size as the tooltip (UTMT-TEX-05).
    private Grid Stack(GameMakerEmbeddedTexture page, IEnumerable<GameMakerTexturePageItem>? items)
    {
        _hover.Bind(Shape.StrokeProperty, this.GetResourceObservable("Text.Accent"));
        var stack = new Grid { Children = { new Chequer(), _image, _hover } }; // a transparent or tiny page still shows where its pixels are
        if (items is null) return stack;
        stack.PointerMoved += (_, e) =>
        {
            var at = e.GetPosition(_image);
            _hovered = ItemAt(page, items, at.X, at.Y);
            if (_hovered is not { } item)
            {
                _hover.IsVisible = false;
                ToolTip.SetTip(stack, null);
                return;
            }

            (_hover.Margin, _hover.Width, _hover.Height, _hover.IsVisible) = (new Thickness(item.SourceX, item.SourceY, 0, 0), item.SourceWidth, item.SourceHeight, true);
            ToolTip.SetTip(stack, Loc.Current.Format("pages.item", ("x", item.SourceX), ("y", item.SourceY), ("width", item.SourceWidth), ("height", item.SourceHeight)));
        };
        stack.PointerExited += (_, _) => _hover.IsVisible = false;
        var find = new MenuItem { Header = Loc.Current["cmd.edit.findUsages"] };
        find.Click += (_, _) =>
        {
            if (_hovered is { } item) FindRequested?.Invoke(item);
        };
        stack.ContextMenu = new ContextMenu { Items = { find } };
        return stack;
    }

    /// <summary>The decoded page size, or null when the page has no picture.</summary>
    public (int Width, int Height)? PictureSize => _image.Source is { } source ? ((int)source.Size.Width, (int)source.Size.Height) : null;

    public Task Ready { get; private set; }

    /// <summary>Find Usages was chosen on the page item under the pointer.</summary>
    public Action<GameMakerTexturePageItem>? FindRequested { get; init; }

    private GameMakerTexturePageItem? _hovered;

    private async Task ShowAsync(GameMakerEmbeddedTexture page, TextureBitmaps textures)
    {
        _image.Source = await textures.GetAsync(page);
        _size.Text = PictureSize is var (width, height) ? $"{width} x {height}" : "";
    }
}
