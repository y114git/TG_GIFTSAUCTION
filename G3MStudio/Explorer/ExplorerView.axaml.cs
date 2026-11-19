using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Commands;
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
    private bool _legacy;

    public ExplorerView()
    {
        InitializeComponent();
        // A GameMaker Studio 1 game has backgrounds, not tile sets (UTMT-TREE-10).
        Model = new ExplorerModel(key => key == "null" ? Loc.Current["explorer.nullEntry"] : QuickTitle(key) ?? Loc.Current["explorer.type." + (key == "tileSets" && _legacy ? "backgrounds" : key)]);
        ToolTip.SetTip(FilterOptions, Loc.Current["explorer.filterOptions"]);
        Avalonia.Automation.AutomationProperties.SetName(FilterOptions, Loc.Current["explorer.filterOptions"]);
        FilterOptions.Click += (_, _) => ShowFilterOptions();
        HiddenCount.Tapped += (_, _) => ShowFilterOptions();
        ViewToggle.Click += (_, _) => ViewToggleRequested?.Invoke();
        FilterOptions.Content = G3MStudio.Theme.GlyphIcons.Chevron(Avalonia.Layout.Orientation.Vertical, true);
        OptionsFold.Click += (_, _) => OptionsFolded = !OptionsFolded;
        ApplyFold();
        Model.Changed += ShowRows;
        LanguageWatch.Follow(this, OnLocalizerChanged);
        List.ItemsSource = _rows;
        WireList();
        WireSticky();
        WireThumbnails();
        WireDrag();
        WireClicks();
        WireDrop();
    }

    private readonly ExplorerRowList _rows = [];

    public ExplorerModel Model { get; }

    public Action<Func<QuickAccess, QuickAccess>>? EditQuick { get; set; }

    /// <summary>The settings the popup edits (what code entries to hide) and the way to change them; the window sets both.</summary>
    public Func<G3MStudio.Core.Settings.AppSettings>? CurrentSettings { get; set; }

    public Action<Func<G3MStudio.Core.Settings.AppSettings, G3MStudio.Core.Settings.AppSettings>>? EditSettings { get; set; }

    private void ShowFilterOptions()
    {
        var settings = CurrentSettings?.Invoke();
        var content = ExplorerFilterPopup.Build(Model.Options, Model.Quick.TagNames, FilterBox.Text ?? "", change => EditQuick?.Invoke(change), settings, EditSettings);
        // Scrolls when the window is short or the text scaling is large: the popup never grows past the screen.
        new Flyout { Content = new ScrollViewer { Content = content, MaxHeight = Math.Max(200, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 700) * 0.8), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } }.ShowAt(FilterOptions);
    }

    /// <summary>Types text into the filter box, as the user would.</summary>
    public void SetFilterText(string text)
    {
        FilterBox.Text = text;
        FlushFilter();
    }

    /// <summary>The dragged keys were dropped on a row that takes them (a folder, collection or tag).</summary>
    public Action<ExplorerRow, IReadOnlyList<string>, bool>? DropOnto { get; set; }

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
        OptionsFold.Content = G3MStudio.Theme.GlyphIcons.Chevron(Avalonia.Layout.Orientation.Vertical, !OptionsFolded);
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

    /// <summary>The selected folder, collection, group, type or search row; null for an entry or a null slot.</summary>
    public ExplorerRow? SelectedHeader => SelectedRow is { IsItem: false, Kind: not ExplorerRowKind.Null } row ? row : null;

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

    /// <summary>The selected entries with the folder each row is listed under (null outside a folder), so a cut takes each one out of its own folder.</summary>
    public IReadOnlyList<(string Key, string? Folder)> SelectedInFolders => [.. List.SelectedItems?.OfType<ExplorerRow>().Where(r => r.IsItem)
        .Select(r => (ResourceTypes.KeyOf(r.Key, r.Text), ParentOf(r) is { Kind: ExplorerRowKind.Folder } holder ? holder.Key[ExplorerModel.FolderPrefix.Length..] : null)) ?? []];

    /// <summary>Selects the entry with the given tab key, opening its type first. <paramref name="flash"/> lights its row for a moment (a new item, a single change).</summary>
    public void Select(string? key, bool flash = false)
    {
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (Model.Rows.Any(r => r is { IsHeader: true, Expanded: false } && r.Key == type))
        {
            Model.Toggle(type);
            // Rows opened above the selected one must be laid out before the selection moves: selecting while that insertion is still pending makes the list
            // realize every inserted row on the way (6,000 rows took 8 seconds, the freeze when a code tab is opened after a texture tab).
            List.UpdateLayout();
        }
        var index = Model.Rows.ToList().FindIndex(r => r.IsItem && r.Key == type && r.Text == name);
        if (index < 0 && Model.Reveal(key)) index = Model.Rows.ToList().FindIndex(r => r.IsItem && r.Key == type && r.Text == name);
        if (index < 0) return;
        List.SelectedIndex = index;
        G3MStudio.Theme.Motion.ScrollTo(List, index);
        KeepSelectedClearOfSticky(); // a row scrolled in from above lands under the pinned heading
        if (flash) Flash(key);
    }

    private void OnLocalizerChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Loc.Current.Language)) Model.Reload();
    }

    /// <summary>The row height and text size follow the zoom slider (50 to 250 percent); with thumbnails on, resource rows show their picture at <see cref="ThumbnailSize"/> scaled by the zoom.</summary>
    public double Zoom => ZoomSlider.Value / 100;

    /// <summary>Sets the zoom in percent, kept between 50 and 250.</summary>
    public void SetZoom(double percent) => ZoomSlider.Value = Math.Clamp(percent, 50, 250);

    public bool Thumbnails => ThumbToggle.IsChecked == true;

    /// <summary>The size in pixels of a row's picture at 100 percent zoom (the Settings option); the zoom scales it along with the text and the row.</summary>
    public double ThumbnailSize
    {
        get;
        set
        {
            value = Math.Clamp(value, 16, 96);
            if (field == value) return;
            field = value;
            ApplyZoom();
        }
    } = Core.Settings.AppSettings.DefaultThumbnailSize;

    // A row is as tall as the theme's row, or as the picture and a margin when the picture is larger.
    private double RowHeight => (Thumbnails ? Math.Max(this.Size("Row.Height", 26), ThumbnailSize + 4) : this.Size("Row.Height", 26)) * Zoom;

    // At the top zoom a large picture would leave the name no room: it never takes more than a third of the list width.
    private double ThumbSide => Math.Min(ThumbnailSize * Zoom, Math.Max(24, List.Bounds.Width / 3));

    private const double ChevronSlot = 16;

    private void ApplyZoom()
    {
        // Rows grow or shrink together, so the same share of the list stays above the view: the scroll position is kept as a share and put back once the new size is laid out.
        var scroll = List.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var share = scroll is { Extent.Height: > 0 } ? scroll.Offset.Y / scroll.Extent.Height : 0;
        List.FontSize = this.Size("FontSize.Body", 13) * Zoom;
        StickyChevron.Width = ChevronSlot * Zoom;
        StickyText.FontSize = StickyCount.FontSize = StickyChevron.FontSize = List.FontSize;
        // The row kept for the keyboard focus stays in the tree while scrolled away and is not among the realized ones: dress it too, or it comes back with the old size.
        foreach (var container in List.GetVisualDescendants().OfType<ListBoxItem>()) Dress(container);
        StickyRow = null;
        UpdateSticky();
        if (scroll is null || share <= 0) return;
        void Restore() => scroll.Offset = new Vector(scroll.Offset.X, share * scroll.Extent.Height);
        Avalonia.Threading.Dispatcher.UIThread.Post(Restore, Avalonia.Threading.DispatcherPriority.Loaded);
        Avalonia.Threading.Dispatcher.UIThread.Post(Restore, Avalonia.Threading.DispatcherPriority.Background);
    }

    private readonly Dictionary<Image, CancellationTokenSource> _pending = [];

    private Avalonia.Media.Color IconColour => this.TryFindResource("Text.Muted", ActualThemeVariant, out var found) && found is Avalonia.Media.ISolidColorBrush brush ? brush.Color : Avalonia.Media.Colors.Gray;

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListBoxItem, object> _watched = [];

    // The icon is a vector in one colour: grey on the plain row, the text colour under the pointer, the dark text on the selection fill (the grey is 1.0 to 1.5:1 on it).
    private Avalonia.Media.Color RowIconColour(ListBoxItem item) =>
        this.TryFindResource(item.IsSelected ? "Text.OnAccent" : item.IsPointerOver ? "Text" : "Text.Muted", ActualThemeVariant, out var found) && found is Avalonia.Media.ISolidColorBrush brush ? brush.Color : IconColour;

    private void RetintIcon(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ListBoxItem.IsSelectedProperty && e.Property != IsPointerOverProperty) return;
        if (sender is ListBoxItem { DataContext: ExplorerRow row } item && item.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Name == "Thumb") is { Source: Avalonia.Media.DrawingImage } thumb)
            thumb.Source = ResourceIcons.Of(row.Key, RowIconColour(item));
    }

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
        // A child code entry the settings ask to gray out: italic and muted, so it reads as part of another entry and not only as a lighter color.
        var dim = item.DataContext is ExplorerRow { Dim: true };
        if (item.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name != "RowChevron" && Grid.GetColumn(t) == 3) is { } label)
        {
            label.FontStyle = dim ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal;
            label.Opacity = dim ? 0.65 : 1;
        }
        // The chevron grows with the text, so its slot (and the gap to the name) grows with it.
        if (item.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "RowChevron") is { } chevron) chevron.Width = ChevronSlot * Zoom;
        var thumb = item.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Name == "Thumb");
        if (thumb is null) return;
        CancelThumb(thumb);
        var show = Thumbnails && item.DataContext is ExplorerRow { Kind: ExplorerRowKind.Item or ExplorerRowKind.Type or ExplorerRowKind.Group or ExplorerRowKind.Folder or ExplorerRowKind.Collection or ExplorerRowKind.Search };
        thumb.IsVisible = show;
        thumb.Width = thumb.Height = ThumbSide;
        if (!show || item.DataContext is not ExplorerRow row)
        {
            thumb.Source = null;
            return;
        }

        thumb.Source = ResourceIcons.Of(row.Key, RowIconColour(item));
        if (_watched.TryAdd(item, true)) item.PropertyChanged += RetintIcon;
        if (!row.IsItem || Pictures is null || !ResourceThumbnails.CanHavePicture(row.Key)) return;
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

    public void FocusFilter() => FilterBox.FocusSelected();

    public void Load(string name, GameMakerData? data)
    {
        _name = name;
        _legacy = data is not null && !data.IsGameMaker2();
        FilterBox.Text = "";
        FlushFilter();
        Model.Load(data);
        ShowHeader();
    }

    /// <summary>Names changed (rename, undo); the unsaved mark lives in the window title only.</summary>
    public void Refresh(bool namesChanged = true)
    {
        if (namesChanged) Model.Refresh();
    }

    private void ShowHeader() => Header.Text = _name;

    private string _shownFilter = "";

    private void ShowRows()
    {
        // Opening or closing a type is shown as an insertion or removal, which keeps the shown rows and the scroll position. A list that is replaced
        // (a filter) resets the scroll position; it is put back so a language change or an edit does not jump.
        var scroll = List.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var offset = scroll?.Offset ?? default;
        var header = SelectedHeader;
        if (_rows.Update(Model.Rows) && scroll is not null && !Model.IsFiltering) Dispatcher.UIThread.Post(() => scroll.Offset = offset, DispatcherPriority.Loaded);
        // A new filter shows its matches from the top; the rows that left in single steps would otherwise leave the old offset past the end, an empty list.
        if (scroll is not null && Model.Filter != _shownFilter) Dispatcher.UIThread.Post(() => scroll.Offset = default, DispatcherPriority.Loaded);
        _shownFilter = Model.Filter;
        // A renamed folder or a heading in another language is a new row; the selection follows it by its key.
        if (header is not null && SelectedRow is null && Model.Rows.ToList().FindIndex(r => r.Kind == header.Kind && r.Key == header.Key) is var at and >= 0) List.SelectedIndex = at;

        IndexHeadings();
        Dispatcher.UIThread.Post(UpdateSticky, DispatcherPriority.Loaded);
        Footer.Text = Model.IsFiltering
            ? Loc.Current.Format("explorer.matches", ("matches", Model.Matches.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("total", Model.Total.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)))
            : Loc.Current.Format("explorer.count", ("count", Model.Total.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("n", Model.Total));
        HiddenCount.IsVisible = Model.Hidden > 0 && !Model.IsFiltering;
        if (HiddenCount.IsVisible) HiddenCount.Text = Loc.Current.Format("explorer.hidden", ("count", Model.Hidden.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)), ("n", Model.Hidden));
        ShowBreadcrumb();
        NoMatch.IsVisible = Model.IsFiltering && Model.Matches == 0;
        NoMatch.Text = Loc.Current.Format("explorer.noMatch", ("filter", Model.Filter));
    }

    private void ShowBreadcrumb()
    {
        ApplyFold();
        var word = Loc.Current[Model.FolderView ? "explorer.viewFolders" : "explorer.viewTypes"];
        if (!Equals(ViewToggle.Content, word)) ViewToggle.Content = word;
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
        G3MStudio.Theme.Motion.ScrollTo(List, at);
        KeepSelectedClearOfSticky();
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
        // Ctrl+X remembers the selected entries and Ctrl+V on a folder moves them there; a text box keeps its own cut and paste.
        if (e.KeyModifiers == Shortcuts.Command && e.Key is Key.X or Key.V && e.Source is not TextBox && (e.Key == Key.V || SelectedKeys.Count > 0))
        {
            CommandRequested?.Invoke(e.Key == Key.X ? "organize.cutToFolder" : "organize.pasteIntoFolder");
            e.Handled = true;
            return;
        }

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

        if (e.Key == Key.Enter && e.KeyModifiers == Shortcuts.Command && List.SelectedItem is ExplorerRow box && ExplorerModel.ScopeOf(box) is { } boxScope)
        {
            Model.Scope = boxScope;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && List.SelectedItem is ExplorerRow { Kind: ExplorerRowKind.Search } search)
        {
            FilterBox.Text = search.Text;
            FlushFilter();
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
        G3MStudio.Theme.Motion.Fold(List);
        e.Handled = true;
    }
}
