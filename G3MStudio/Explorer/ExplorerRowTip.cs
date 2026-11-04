using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace G3MStudio.Explorer;

/// <summary>The tooltip of an Explorer row: the name, and for a sprite, tile set or font a small picture that loads when the tip opens (the page decode is cached and runs off the UI thread).</summary>
public sealed class ExplorerRowTip : StackPanel
{
    public const int MaxSide = 128;

    private readonly Image _picture = new() { Name = "RowThumbnail", MaxWidth = MaxSide, MaxHeight = MaxSide, Stretch = Stretch.Uniform, IsVisible = false, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    private readonly Func<Task<IImage?>>? _load;
    private int _version;

    public ExplorerRowTip(string text, Func<Task<IImage?>>? load)
    {
        _load = load;
        Spacing = 4;
        RenderOptions.SetBitmapInterpolationMode(_picture, BitmapInterpolationMode.None); // pixel art stays sharp
        Children.Add(new TextBlock { Text = text });
        Children.Add(_picture);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_load is null) return;
        var version = ++_version;
        _ = ShowAsync(version);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _version++;
        _picture.Source = null;
        _picture.IsVisible = false;
        base.OnDetachedFromVisualTree(e);
    }

    private async Task ShowAsync(int version)
    {
        var bitmap = await _load!();
        if (version == _version && bitmap is not null) Dispatcher.UIThread.Post(() => (_picture.Source, _picture.IsVisible) = (bitmap, true));
    }
}
