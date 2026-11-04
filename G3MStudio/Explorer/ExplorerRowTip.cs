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

    private readonly Image _picture = new() { Name = "RowThumbnail", MaxWidth = MaxSide, MaxHeight = MaxSide, Stretch = Stretch.Uniform, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    private readonly TextBlock _text = new();
    private Func<Task<IImage?>>? _load;
    private int _version;

    public ExplorerRowTip(string text, Func<Task<IImage?>>? load)
    {
        Spacing = 4;
        RenderOptions.SetBitmapInterpolationMode(_picture, BitmapInterpolationMode.None); // pixel art stays sharp
        Children.Add(_text);
        Children.Add(_picture);
        Retarget(text, load);
    }

    /// <summary>Points the tip at another row, so a recycled list item keeps its tip instead of making a new control each time it scrolls into view.</summary>
    public void Retarget(string text, Func<Task<IImage?>>? load)
    {
        _version++;
        (_text.Text, _load) = (text, load);
        _picture.Source = null;
        // The tip popup is sized once, so reserve the picture area up front; it is hidden again if there is no picture.
        (_picture.Width, _picture.Height, _picture.IsVisible) = (MaxSide, MaxSide, load is not null);
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
        _picture.IsVisible = _load is not null;
        base.OnDetachedFromVisualTree(e);
    }

    private async Task ShowAsync(int version)
    {
        var bitmap = await _load!();
        if (version != _version) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != _version) return;
            (_picture.Source, _picture.IsVisible) = (bitmap, bitmap is not null);
        });
    }
}
