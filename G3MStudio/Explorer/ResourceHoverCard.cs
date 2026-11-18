using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace G3MStudio.Explorer;

/// <summary>
/// The one hover card for a resource, shown by every surface through <see cref="HoverCards"/>: the icon of its kind, its name or nickname (the real name under a nickname), its type and ID, and a picture
/// where the kind has one. The content is made when the card opens, so a list of 50 000 rows carries one small record per row; the picture is fetched then (cached, off the UI thread) and given up when the card closes.
/// With the setting off, or for a key that is no resource, the card is the plain text it was given. A control carries only a <see cref="HoverTip"/>; the card is made from it each time the tip opens.
/// </summary>
public sealed class ResourceHoverCard : StackPanel
{
    public const int MaxSide = 128;

    /// <summary>The widest a card gets; longer text wraps.</summary>
    public const int MaxCard = 420;

    private readonly Image _icon = new() { Name = "HoverIcon", Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    // No text sets a width of its own: the tip that holds the card limits it, and a name without spaces (a script or event name) breaks wherever the line ends instead of being cut.
    private readonly TextBlock _title = new() { Name = "HoverTitle", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _real = new() { Name = "HoverName", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _meta = new() { Name = "HoverMeta", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { Name = "HoverDetail", TextWrapping = TextWrapping.Wrap };
    private readonly Image _picture = new() { Name = "RowThumbnail", MaxWidth = MaxSide, MaxHeight = MaxSide, Stretch = Stretch.Uniform, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    private readonly string? _key;
    private readonly string _text, _more;
    private Action? _cancel; // stops the wait for the picture of the card that was open
    private int _version;
    private string? _iconType;

    public ResourceHoverCard(HoverTip tip)
    {
        (_key, _text, _more) = (tip.Key, tip.Text, tip.More);
        Spacing = 4;
        MaxWidth = MaxCard; // a card shown without a tip around it (the completion list) is limited too
        RenderOptions.SetBitmapInterpolationMode(_picture, BitmapInterpolationMode.None); // pixel art stays sharp
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(_title, 1);
        head.Children.Add(_icon);
        head.Children.Add(_title);
        Children.Add(head);
        Children.Add(_real);
        Children.Add(_meta);
        Children.Add(_detail);
        Children.Add(_picture);
        // Colours are local values: no style of the row, tab or button the card hangs on (they reach it through the logical tree, or by inheritance) may change them.
        foreach (var (text, key) in new[] { (_title, "Text"), (_real, "Text.Muted"), (_meta, "Text.Muted"), (_detail, "Text") })
            text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable(key));
        // Filled here and not on attach: a popup that is measured before the card reaches its tree would take the size of the plain card and clip the rest.
        Fill();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Fill(); // a card attached again asks for its picture again, also when the earlier try came back empty and hid the picture
        if (_iconType is not null) _icon.Source = ResourceIcons.Of(_iconType, IconColour); // the colour needs the theme of the tree
        Show();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Stop();
        _picture.Source = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void Stop()
    {
        _version++;
        _cancel?.Invoke();
        _cancel = null;
    }

    // The text parts, made from the current setting and workspace; the picture is asked for in Show.
    private void Fill()
    {
        var info = _key is not null && HoverCards.Enabled ? HoverCards.Describe?.Invoke(_key) : null;
        if (info is null)
        {
            Plain();
            return;
        }

        (_icon.IsVisible, _iconType) = (true, info.Type);
        _title.Text = info.Title;
        (_real.Text, _real.IsVisible) = (info.Name, info.Nickname is not null);
        (_meta.Text, _meta.IsVisible) = (HoverCards.MetaOf(info), true);
        (_detail.Text, _detail.IsVisible) = (_more, _more.Length > 0);
        // The picture area is reserved up front (and hidden again when no picture comes), so the card keeps its size when the picture arrives.
        (_picture.Width, _picture.Height, _picture.IsVisible) = (MaxSide, MaxSide, info.HasPicture && HoverCards.Picture is not null);
    }

    private void Plain()
    {
        _title.Text = _text;
        (_icon.IsVisible, _real.IsVisible, _meta.IsVisible, _detail.IsVisible, _picture.IsVisible) = (false, false, false, false, false);
    }

    private Color IconColour => this.TryFindResource("Text.Muted", ActualThemeVariant, out var found) && found is ISolidColorBrush brush ? brush.Color : Colors.Gray;

    private void Show()
    {
        Stop();
        if (!_picture.IsVisible || _key is null || HoverCards.Picture is not { } load) return;
        var version = _version;
        _ = ShowAsync(load, _key, version);
    }

    private async Task ShowAsync(Func<string, CancellationToken, Task<IImage?>> load, string key, int version)
    {
        using var cancel = new CancellationTokenSource();
        _cancel = cancel.Cancel;
        IImage? bitmap;
        try { bitmap = await load(key, cancel.Token); }
        catch (OperationCanceledException) { return; }
        finally { if (version == _version) _cancel = null; } // a card that was closed or retargeted has a newer wait; the finished one must not be cancelled after its source is gone
        Dispatcher.UIThread.Post(() =>
        {
            if (version != _version) return;
            (_picture.Source, _picture.IsVisible) = (bitmap, bitmap is not null);
        });
    }
}
