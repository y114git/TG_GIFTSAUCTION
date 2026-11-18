using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>A small picture of an entry that fills in from the shared picture cache once the control is on screen; the icon of the type shows until then.</summary>
public static class LinkThumb
{
    public const int Side = 24;

    public static Image Make(string key, string name = "Thumb")
    {
        var image = new Image { Name = name, Width = Side, Height = Side, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
        Bind(image, key);
        return image;
    }

    /// <summary>Shows the icon of the entry's type, then its picture. Only a control that is shown asks, so a long list asks for the rows in view.</summary>
    public static void Bind(Image image, string key)
    {
        CancellationTokenSource? pending = null;
        var type = ResourceTypes.SplitKey(key)?.Type ?? "";
        image.AttachedToVisualTree += (_, _) =>
        {
            var colour = image.TryFindResource("Text.Muted", image.ActualThemeVariant, out var found) && found is ISolidColorBrush brush ? brush.Color : Colors.Gray;
            image.Source = ResourceIcons.Of(type, colour);
            if (!ResourceThumbnails.CanHavePicture(type) || LinkContext.GetPictures(image) is not { } pictures) return;
            if (pictures.TryGet(key, out var cached))
            {
                Show(image, cached);
                return;
            }

            pending?.Cancel();
            var source = pending = new CancellationTokenSource();
            _ = Load(image, pictures, key, source);
        };
        image.DetachedFromVisualTree += (_, _) =>
        {
            pending?.Cancel();
            pending = null;
        };
    }

    private static async Task Load(Image image, ThumbnailService pictures, string key, CancellationTokenSource source)
    {
        IImage? picture;
        try { picture = await pictures.GetAsync(key, source.Token); }
        catch (OperationCanceledException) { return; }
        if (!source.IsCancellationRequested) Show(image, picture);
    }

    private static void Show(Image image, IImage? picture)
    {
        if (picture is null) return;
        RenderOptions.SetBitmapInterpolationMode(image, picture.Size.Width <= 48 && picture.Size.Height <= 48 ? Avalonia.Media.Imaging.BitmapInterpolationMode.None : Avalonia.Media.Imaging.BitmapInterpolationMode.MediumQuality);
        image.Source = picture;
    }
}

/// <summary>
/// The list a link is set from: every entry of one type with its picture, a search box above it. Choosing a row, by a click or Enter, raises <see cref="Chosen"/> with the Explorer key.
/// </summary>
public sealed class ResourcePicker : DockPanel
{
    public const int MaxShown = 2000;

    private readonly Workspace _workspace;
    private readonly string _type;
    private readonly string[] _names;
    private readonly TextBox _search = new() { Name = "PickerSearch" };
    private readonly ListBox _list = new() { Name = "PickerList", MinHeight = 160 };
    private readonly TextBlock _empty = new() { Name = "PickerEmpty", Classes = { "muted" }, IsVisible = false, Margin = new Thickness(4) };

    public ResourcePicker(Workspace workspace, string type, string? current = null)
    {
        (_workspace, _type) = (workspace, type);
        _names = ResourceTypes.All.FirstOrDefault(t => t.Key == type)?.Items(workspace.Data)?.Select(ResourceTypes.NameOf).Where(n => n.Length > 0).ToArray() ?? [];
        _search.PlaceholderText = Loc.Current["link.search"];
        _empty.Text = Loc.Current["link.noMatch"];
        _list.ItemTemplate = new FuncDataTemplate<string>((name, _) => name is null ? new TextBlock() : Row(name));
        SetDock(_search, Avalonia.Controls.Dock.Top);
        SetDock(_empty, Avalonia.Controls.Dock.Bottom);
        Children.Add(_search);
        Children.Add(_empty);
        Children.Add(_list);
        _search.TextChanged += (_, _) => Refill();
        // Only a tap on a row picks; one on the scroll bar or the empty space under the rows would pick the row selected before.
        _list.Tapped += (_, e) =>
        {
            if (e.Source is Visual source && Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<ListBoxItem>(source, includeSelf: true) is not null) Pick();
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            Pick();
        };
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && _list.ItemCount > 0)
            {
                _list.SelectedIndex = Math.Max(0, _list.SelectedIndex);
                _list.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _list.ItemCount > 0)
            {
                _list.SelectedIndex = Math.Max(0, _list.SelectedIndex);
                Pick();
                e.Handled = true;
            }
        };
        Refill(current);
    }

    public event Action<string>? Chosen;

    /// <summary>The names listed now, in order.</summary>
    public IReadOnlyList<string> Shown => [.. _list.Items.OfType<string>()];

    public string Search
    {
        get => _search.Text ?? "";
        set => _search.Text = value;
    }

    public void Choose(string name) => Chosen?.Invoke(ResourceTypes.KeyOf(_type, name));

    private void Pick()
    {
        if (_list.SelectedItem is string name) Choose(name);
    }

    private StackPanel Row(string name)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(LinkThumb.Make(ResourceTypes.KeyOf(_type, name)));
        var label = _workspace.Nicknames.Label(_type, name);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        if (label != name) ToolTip.SetTip(row, name);
        return row;
    }

    private void Refill(string? select = null)
    {
        var text = Search.Trim();
        var shown = _names.Where(n => text.Length == 0 || n.Contains(text, StringComparison.OrdinalIgnoreCase) || _workspace.Nicknames.Label(_type, n).Contains(text, StringComparison.OrdinalIgnoreCase)).Take(MaxShown).ToList();
        _list.ItemsSource = shown;
        _empty.IsVisible = shown.Count == 0;
        if (select is not null && ResourceTypes.SplitKey(select) is var (_, name) && shown.IndexOf(name) is >= 0 and var at) _list.SelectedIndex = at;
    }

    /// <summary>Opens the picker under <paramref name="anchor"/>. <paramref name="chosen"/> gets the Explorer key of the entry picked.</summary>
    public static ResourcePicker Show(Control anchor, Workspace workspace, string type, string? current, Action<string> chosen)
    {
        var picker = new ResourcePicker(workspace, type, current) { Width = 340, Height = 380 };
        var flyout = new Flyout { Content = picker, Placement = PlacementMode.BottomEdgeAlignedLeft };
        picker.Chosen += key =>
        {
            flyout.Hide();
            chosen(key);
        };
        flyout.Opened += (_, _) => picker._search.Focus();
        flyout.ShowAt(anchor);
        return picker;
    }
}
