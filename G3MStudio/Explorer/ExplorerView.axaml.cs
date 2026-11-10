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
        Model.Changed += ShowRows;
        List.ItemsSource = _rows;
        FilterBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Model.SetFilter(FilterBox.Text ?? ""); };
        List.Tapped += (_, e) =>
        {
            if ((e.Source as Control)?.DataContext is not ExplorerRow row) return;
            // Ctrl and Shift clicks only change the selection; they must not open a tab.
            if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Shift) return;
            if (row.IsItem) RequestOpen(row, true);
            else if (row.Kind == ExplorerRowKind.Search) FilterBox.Text = row.Text;
            else if (row.IsHeader) Model.Toggle(row.Key);
        };
        List.SelectionMode = SelectionMode.Multiple;
        // The tooltip is made per row when the row appears; its picture is fetched only when the tip opens.
        List.ContainerPrepared += (_, e) =>
        {
            if (e.Container.DataContext is not ExplorerRow row) return;
            var key = row.IsItem ? ResourceTypes.KeyOf(row.Key, row.Text) : null;
            Func<Task<Avalonia.Media.IImage?>>? load = key is null ? null : () => Thumbnail?.Invoke(key) ?? Task.FromResult<Avalonia.Media.IImage?>(null);
            if (ToolTip.GetTip(e.Container) is ExplorerRowTip tip) tip.Retarget(row.Text, load);
            else ToolTip.SetTip(e.Container, new ExplorerRowTip(row.Text, load));
        };
        List.SelectionChanged += (_, _) => SelectionChanged?.Invoke();
        ZoomSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) ApplyZoom();
        };
        ThumbToggle.IsCheckedChanged += (_, _) => ApplyZoom();
        List.ContainerPrepared += (_, e) => Dispatcher.UIThread.Post(() => Dress(e.Container), DispatcherPriority.Loaded);
        // A drag starts after the pointer has moved a few pixels with the button down on an entry.
        List.AddHandler(PointerPressedEvent, (_, e) => _press = e.GetCurrentPoint(List).Properties.IsLeftButtonPressed && e.Source is Control { DataContext: ExplorerRow { IsItem: true } } ? e : null, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        List.PointerReleased += (_, _) => _press = null;
        List.PointerMoved += async (_, e) =>
        {
            if (_press is not { } press || (e.GetPosition(List) - press.GetPosition(List)).X is var dx && (e.GetPosition(List) - press.GetPosition(List)).Y is var dy && dx * dx + dy * dy < 64) return;
            _press = null;
            if (press.Source is not Control { DataContext: ExplorerRow { IsItem: true } row }) return;
            var item = new DataTransferItem();
            var key = ResourceTypes.KeyOf(row.Key, row.Text);
            item.Set(ResourceDrag.Format, key);
            // Dragging one of several selected rows takes them all.
            item.Set(ResourceDrag.Keys, string.Join('\n', List.SelectedItems?.Contains(row) == true && SelectedKeys.Count > 1 ? SelectedKeys : [key]));
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy | DragDropEffects.Move); // the target answers Move, which the allowed effects must include
        };
        List.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(List).Properties.IsRightButtonPressed || e.Source is not Control { DataContext: ExplorerRow row } source) return;
            if (row.IsItem && List.SelectedItems?.Contains(row) != true) List.SelectedItem = row;
            MenuRequested?.Invoke(row, source);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // A middle click opens the entry in a tab of its own, like a double click (UTMT-TREE-02).
        List.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(List).Properties.IsMiddleButtonPressed || e.Source is not Control { DataContext: ExplorerRow { IsItem: true } row }) return;
            RequestOpen(row, false);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        List.DoubleTapped += (_, e) =>
        {
            if (e.Source is not Control { DataContext: ExplorerRow row }) return;
            if (row.IsItem) RequestOpen(row, false);
            else if (ExplorerModel.ScopeOf(row) is { } scope) Model.Scope = scope;
        };
        DragDrop.SetAllowDrop(List, true);
        List.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.TryGetValue(ResourceDrag.Keys) is not null && DropTarget(e) is not null ? DragDropEffects.Move : DragDropEffects.None);
        List.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.DataTransfer.TryGetValue(ResourceDrag.Keys) is not { } text || DropTarget(e) is not { } target) return;
            DropOnto?.Invoke(target, [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries)]);
            e.Handled = true;
        });
        List.AddHandler(TextInputEvent, OnTextInput, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // Tunnel: the list handles the arrow keys itself and marks them handled before a bubbling handler would run.
        List.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private PointerPressedEventArgs? _press;

    private readonly ExplorerRowList _rows = [];

    public ExplorerModel Model { get; }

    public Action<Func<QuickAccess, QuickAccess>>? EditQuick { get; set; }

    /// <summary>The dragged keys were dropped on a row that takes them (a folder, collection or tag).</summary>
    public Action<ExplorerRow, IReadOnlyList<string>>? DropOnto { get; set; }

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

    /// <summary>Loads the picture for the tooltip of the entry with this tab key; null when the entry has none.</summary>
    public Func<string, Task<Avalonia.Media.IImage?>>? Thumbnail { get; set; }

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

    private void Dress(Control container)
    {
        if (container is not ListBoxItem item) return;
        item.MinHeight = RowHeight;
        var thumb = item.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Name == "Thumb");
        if (thumb is null) return;
        var show = Thumbnails && item.DataContext is ExplorerRow { IsItem: true };
        thumb.IsVisible = show;
        thumb.Width = thumb.Height = RowHeight - 4;
        if (!show || item.DataContext is not ExplorerRow row || Thumbnail is null) return;
        var key = ResourceTypes.KeyOf(row.Key, row.Text);
        _ = LoadThumb(thumb, key);
    }

    private async Task LoadThumb(Image thumb, string key)
    {
        var picture = await Thumbnail!(key);
        if (thumb.DataContext is null || thumb.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()?.DataContext is ExplorerRow row && ResourceTypes.KeyOf(row.Key, row.Text) == key) thumb.Source = picture;
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
