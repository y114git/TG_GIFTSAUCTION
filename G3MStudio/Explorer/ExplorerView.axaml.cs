using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Explorer;

/// <summary>The resource tree: a filter box over a virtualized flat list of <see cref="ExplorerRow"/> (design/shell.md section 5).</summary>
public partial class ExplorerView : UserControl
{
    public static readonly IValueConverter Indent = new FuncValueConverter<int, Thickness>(depth => new Thickness(depth * 14, 0, 0, 0));

    /// <summary>The colour dot of a folder, collection or tag row.</summary>
    public static readonly IValueConverter ColourBrush = new FuncValueConverter<string?, Avalonia.Media.IBrush?>(text => Avalonia.Media.Color.TryParse(text ?? "", out var color) ? new Avalonia.Media.SolidColorBrush(color) : null);

    private string _name = "";
    private string _typed = "";
    private DateTime _typedAt;
    private bool _dirty;
    private bool _legacy;

    public ExplorerView()
    {
        InitializeComponent();
        // A GameMaker Studio 1 game has backgrounds, not tile sets (UTMT-TREE-10).
        Model = new ExplorerModel(key => key == "null" ? Loc.Current["explorer.nullEntry"] : QuickTitle(key) ?? Loc.Current["explorer.type." + (key == "tileSets" && _legacy ? "backgrounds" : key)]);
        ToolTip.SetTip(FilterOptions, Loc.Current["explorer.filterOptions"]);
        Avalonia.Automation.AutomationProperties.SetName(FilterOptions, Loc.Current["explorer.filterOptions"]);
        FilterOptions.Click += (_, _) => new Flyout { Content = ExplorerFilterPopup.Build(Model.Options, Model.Quick.TagNames, FilterBox.Text ?? "", change => EditQuick?.Invoke(change)) }.ShowAt(FilterOptions);
        ViewToggle.Click += (_, _) => ViewToggleRequested?.Invoke();
        OptionsFold.Click += (_, _) => OptionsFolded = !OptionsFolded;
        ApplyFold();
        Model.Changed += ShowRows;
        List.ItemsSource = _rows;
        WireList();
        WireThumbnails();
        WireDrag();
        WireClicks();
        WireDrop();
    }

    private readonly ExplorerRowList _rows = [];

    public ExplorerModel Model { get; }

    public Action<Func<QuickAccess, QuickAccess>>? EditQuick { get; set; }

    /// <summary>The dragged keys were dropped on a row that takes them (a folder, collection or tag).</summary>
    public Action<ExplorerRow, IReadOnlyList<string>>? DropOnto { get; set; }

    /// <summary>The bar under the list (count, Thumbnails toggle, zoom slider) is folded to a thin strip with one chevron, so the list gets its height.</summary>
    public bool OptionsFolded
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            ApplyFold();
            OptionsFoldChanged?.Invoke(value);
        }
    }

    /// <summary>The chevron folded or unfolded the bar: the new state, to be remembered.</summary>
    public event Action<bool>? OptionsFoldChanged;

    private void ApplyFold()
    {
        OptionsRow.IsVisible = !OptionsFolded; // the count stays on the strip beside the chevron
        OptionsFold.Content = OptionsFolded ? "⌃" : "⌄";
        var title = Loc.Current[OptionsFolded ? "explorer.showOptions" : "explorer.hideOptions"];
        ToolTip.SetTip(OptionsFold, title);
        Avalonia.Automation.AutomationProperties.SetName(OptionsFold, title);
    }

    /// <summary>The view button was pressed (type view and folder view swap).</summary>
    public event Action? ViewToggleRequested;

    /// <summary>A key that runs a registry command (Delete): the command id.</summary>
    public event Action<string>? CommandRequested;

    /// <summary>Whether a row takes dropped resources: a folder, a collection, or a tag.</summary>
    public static bool AcceptsDrop(ExplorerRow row) => row.Kind is ExplorerRowKind.Folder or ExplorerRowKind.Collection || row.Kind == ExplorerRowKind.Type && row.Key.StartsWith(ExplorerModel.TagPrefix, StringComparison.Ordinal);

    private static ExplorerRow? DropTarget(DragEventArgs e) =>
        (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>()?.DataContext is ExplorerRow row && AcceptsDrop(row) ? row : null;

    /// <summary>The highlighted row (the last one selected), including headings.</summary>
    public ExplorerRow? SelectedRow => List.SelectedItem as ExplorerRow;

    /// <summary>The heading, folder or collection row that a row sits under, or null at the top.</summary>
    public ExplorerRow? ParentOf(ExplorerRow row)
    {
        var rows = Model.Rows;
        for (var i = rows.ToList().IndexOf(row) - 1; i >= 0; i--)
            if (rows[i].Depth < row.Depth) return rows[i];
        return null;
    }

    /// <summary>Opens the container row and selects the entries with these tab keys among the rows below it.</summary>
    public void SelectKeys(IReadOnlyCollection<string> keys, ExplorerRow? container)
    {
        if (container is not null) Model.OpenAll(container);
        var rows = Model.Rows;
        var start = container is null ? 0 : Math.Max(0, rows.ToList().FindIndex(r => r.Key == container.Key && r.Kind == container.Kind));
        var seen = new HashSet<string>();
        List.SelectedItems?.Clear();
        for (var i = start; i < rows.Count; i++)
            if (rows[i].IsItem && ResourceTypes.KeyOf(rows[i].Key, rows[i].Text) is var key && keys.Contains(key) && seen.Add(key)) List.SelectedItems?.Add(_rows[i]);
    }

    // The headings of the Quick Access group; literal keys, so the language test sees them.
    private static string? QuickTitle(string key) => key switch
    {
        ExplorerModel.QuickGroup => Loc.Current["explorer.quick"],
        ExplorerModel.QuickRecent => Loc.Current["explorer.quickRecent"],
        ExplorerModel.QuickFavourites => Loc.Current["explorer.quickFavourites"],
        ExplorerModel.QuickSearches => Loc.Current["explorer.quickSearches"],
        ExplorerModel.QuickTags => Loc.Current["explorer.quickTags"],
        ExplorerModel.QuickFolders => Loc.Current["explorer.quickFolders"],
        ExplorerModel.QuickCollections => Loc.Current["explorer.quickCollections"],
        ExplorerModel.BreadcrumbRoot => Loc.Current["explorer.breadcrumbRoot"],
        _ => null,
    };

    /// <summary>An entry was clicked (preview true) or double-clicked or confirmed with Enter (preview false).</summary>
    public event Action<ExplorerRow, bool>? OpenRequested;

    public event Action? SelectionChanged;

    /// <summary>A right click on a row (already selected): the row and the control to open the menu at.</summary>
    public event Action<ExplorerRow, Control>? MenuRequested;

    /// <summary>The pictures of the rows and tooltips; set by the window.</summary>
    public ThumbnailService? Pictures { get; set; }

    /// <summary>Loads the picture of the entry with this tab key; null when the entry has none.</summary>
    public Func<string, Task<Avalonia.Media.IImage?>>? Thumbnail => Pictures is null ? null : key => Pictures.GetAsync(key);

    public void RequestOpen(ExplorerRow row, bool preview) => OpenRequested?.Invoke(row, preview);

    /// <summary>Tab key of the selected entry, or null.</summary>
    public string? SelectedKey => List.SelectedItem is ExplorerRow { IsItem: true } row ? ResourceTypes.KeyOf(row.Key, row.Text) : null;

    /// <summary>Tab keys of every selected entry (Ctrl and Shift clicks select several), in list order; headings are left out.</summary>
    public IReadOnlyList<string> SelectedKeys => [.. List.SelectedItems?.OfType<ExplorerRow>().Where(r => r.IsItem).Select(r => ResourceTypes.KeyOf(r.Key, r.Text)) ?? []];

    /// <summary>Selects the entry with the given tab key, opening its type first.</summary>
    public void Select(string? key)
    {
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (Model.Rows.Any(r => r is { IsHeader: true, Expanded: false } && r.Key == type)) Model.Toggle(type);
        var index = Model.Rows.ToList().FindIndex(r => r.IsItem && r.Key == type && r.Text == name);
        if (index < 0) return;
        List.SelectedIndex = index;
        List.ScrollIntoView(index);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Loc.Current.PropertyChanged += OnLocalizerChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Loc.Current.PropertyChanged -= OnLocalizerChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLocalizerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Loc.Current.Language)) Model.Reload();
    }

    /// <summary>The row height and text size follow the zoom slider (50 to 250 percent); with thumbnails on, resource rows show their picture at the row height.</summary>
    public double Zoom => ZoomSlider.Value / 100;

    /// <summary>Sets the zoom in percent, kept between 50 and 250.</summary>
    public void SetZoom(double percent) => ZoomSlider.Value = Math.Clamp(percent, 50, 250);

    public bool Thumbnails => ThumbToggle.IsChecked == true;

    private double RowHeight => this.Size("Row.Height", 24) * Zoom * (Thumbnails ? 1.6 : 1);

    private void ApplyZoom()
    {
        List.FontSize = this.Size("FontSize.Body", 13) * Zoom;
        foreach (var container in List.GetRealizedContainers()) Dress(container);
    }

    private readonly Dictionary<Image, CancellationTokenSource> _pending = [];

    private Avalonia.Media.Color IconColour => this.TryFindResource("Text.Muted", ActualThemeVariant, out var found) && found is Avalonia.Media.ISolidColorBrush brush ? brush.Color : Avalonia.Media.Colors.Gray;

    private void CancelThumb(Image thumb)
    {
        if (!_pending.Remove(thumb, out var source)) return;
        source.Cancel();
        source.Dispose();
    }

    // With thumbnails on, every entry row shows the icon of its type at once (so the names line up), and a type that can have a picture swaps it in when it is ready.
    private void Dress(Control container)
    {
        if (container is not ListBoxItem item) return;
        item.MinHeight = RowHeight;
        var thumb = item.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Name == "Thumb");
        if (thumb is null) return;
        CancelThumb(thumb);
        var show = Thumbnails && item.DataContext is ExplorerRow { IsItem: true };
        thumb.IsVisible = show;
        // At the top zoom a row-high picture would leave the name no room: it never takes more than a third of the list width.
        thumb.Width = thumb.Height = Math.Min(RowHeight - 4, Math.Max(24, List.Bounds.Width / 3));
        if (!show || item.DataContext is not ExplorerRow row)
        {
            thumb.Source = null;
            return;
        }

        thumb.Source = ResourceIcons.Of(row.Key, IconColour);
        if (Pictures is null || !ResourceThumbnails.CanHavePicture(row.Key)) return;
        var key = ResourceTypes.KeyOf(row.Key, row.Text);
        if (Pictures.TryGet(key, out var cached))
        {
            if (cached is not null) ShowPicture(thumb, cached);
            return;
        }

        var source = _pending[thumb] = new CancellationTokenSource();
        _ = LoadThumb(thumb, key, source);
    }

    private async Task LoadThumb(Image thumb, string key, CancellationTokenSource source)
    {
        Avalonia.Media.IImage? picture;
        try { picture = Pictures is null ? null : await Pictures.GetAsync(key, source.Token); }
        catch (OperationCanceledException) { return; }
        if (source.IsCancellationRequested || picture is null) return;
        if (_pending.TryGetValue(thumb, out var own) && own == source) CancelThumbKeep(thumb);
        ShowPicture(thumb, picture);
    }

    private void CancelThumbKeep(Image thumb)
    {
        if (_pending.Remove(thumb, out var source)) source.Dispose();
    }

    // Small pictures are pixel art and stay sharp when the row makes them larger; larger ones are smoothed.
    private static void ShowPicture(Image thumb, Avalonia.Media.IImage picture)
    {
        Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(thumb, picture.Size.Width <= 48 && picture.Size.Height <= 48 ? Avalonia.Media.Imaging.BitmapInterpolationMode.None : Avalonia.Media.Imaging.BitmapInterpolationMode.MediumQuality);
        thumb.Source = picture;
    }

    public void FocusFilter() => FilterBox.Focus();

    public void Load(string name, GameMakerData? data)
    {
        _name = name;
        _legacy = data is not null && !data.IsGameMaker2();
        FilterBox.Text = "";
        Model.Load(data);
        ShowHeader();
    }

    /// <summary>Names changed (rename, undo) or the dirty mark moved.</summary>
    public void Refresh(bool dirty, bool namesChanged = true)
    {
        _dirty = dirty;
        ShowHeader();
        if (namesChanged) Model.Refresh();
    }

    private void ShowHeader() => Header.Text = (_dirty ? "● " : "") + _name;

    private void ShowRows()
    {
        // Opening or closing a type is shown as an insertion or removal, which keeps the shown rows and the scroll position. A list that is replaced
        // (a filter) resets the scroll position; it is put back so a language change or an edit does not jump.
        var scroll = List.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var offset = scroll?.Offset ?? default;
        if (_rows.Update(Model.Rows) && scroll is not null && !Model.IsFiltering) Dispatcher.UIThread.Post(() => scroll.Offset = offset, DispatcherPriority.Loaded);

        Footer.Text = Model.IsFiltering
            ? Loc.Current.Format("explorer.matches", ("matches", Model.Matches.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("total", Model.Total.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)))
            : Loc.Current.Format("explorer.count", ("count", Model.Total.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)));
        ShowBreadcrumb();
        NoMatch.IsVisible = Model.IsFiltering && Model.Matches == 0;
        NoMatch.Text = Loc.Current.Format("explorer.noMatch", ("filter", Model.Filter));
    }

    private void ShowBreadcrumb()
    {
        ApplyFold();
        ViewToggle.Content = Loc.Current[Model.FolderView ? "explorer.viewTypes" : "explorer.viewFolders"];
        ToolTip.SetTip(ViewToggle, Loc.Current["explorer.viewToggle"]);
        Avalonia.Automation.AutomationProperties.SetName(ViewToggle, Loc.Current["explorer.viewToggle"]);
        BreadcrumbScroll.IsVisible = Model.Scope is not null;
        Breadcrumb.Children.Clear();
        if (Model.Scope is null) return;
        var parts = Model.Breadcrumb;
        for (var i = 0; i < parts.Count; i++)
        {
            var scope = parts[i].Scope;
            if (i > 0) Breadcrumb.Children.Add(new TextBlock { Text = "›", Classes = { "muted" }, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            var part = new Button { Content = i == 0 ? Loc.Current["explorer.breadcrumbRoot"] : parts[i].Text, Padding = new Thickness(4, 0), IsEnabled = i < parts.Count - 1 };
            part.Click += (_, _) => Model.Scope = scope;
            Breadcrumb.Children.Add(part);
        }
    }

    // Typed letters jump to the next row that starts with them; a pause of a second starts a new word.
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text)) return;
        var now = DateTime.UtcNow;
        _typed = now - _typedAt > TimeSpan.FromSeconds(1) ? e.Text : _typed + e.Text;
        _typedAt = now;
        var at = ExplorerModel.TypeAhead(Model.Rows, List.SelectedIndex, _typed);
        if (at < 0) return;
        List.SelectedIndex = at;
        List.ScrollIntoView(at);
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None && List.SelectedItem is ExplorerRow { IsItem: true } or ExplorerRow { Kind: ExplorerRowKind.Folder or ExplorerRowKind.Collection })
        {
            CommandRequested?.Invoke("edit.delete");
            e.Handled = true;
            return;
        }

        // Backspace goes one level up out of a folder; Left on an entry moves to the row it sits under.
        if (e.Key == Key.Back && Model.Scope is not null)
        {
            var parts = Model.Breadcrumb;
            Model.Scope = parts.Count > 2 ? parts[^2].Scope : null;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && List.SelectedItem is ExplorerRow { IsHeader: false } leaf && ParentOf(leaf) is { } up)
        {
            List.SelectedItem = _rows.FirstOrDefault(r => r.Equals(up));
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control && List.SelectedItem is ExplorerRow box && ExplorerModel.ScopeOf(box) is { } boxScope)
        {
            Model.Scope = boxScope;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && List.SelectedItem is ExplorerRow { Kind: ExplorerRowKind.Search } search)
        {
            FilterBox.Text = search.Text;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && List.SelectedItem is ExplorerRow { IsItem: true } item)
        {
            RequestOpen(item, false);
            e.Handled = true;
            return;
        }

        if (List.SelectedItem is not ExplorerRow { IsHeader: true } row) return;
        var toggle = e.Key is Key.Enter or Key.Space || e.Key == Key.Right && !row.Expanded || e.Key == Key.Left && row.Expanded;
        if (!toggle) return;
        Model.Toggle(row.Key);
        e.Handled = true;
    }
}
