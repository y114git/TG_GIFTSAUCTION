using G3MStudio.Core.Commands;
using G3MStudio.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Avalonia.Json;
using Dock.Model.Controls;
using Dock.Model.Core;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Docking;

/// <summary>
/// The panel layout (design/shell.md sections 9 and 10): a left, right and bottom tool dock around the editor area, whose groups
/// hold the editor tabs. The layout is JSON text; panel and editor content is not part of it and is attached again by id after
/// every load. Tab state (preview, pinned) lives in <see cref="Tabs"/>.
/// </summary>
public sealed partial class StudioDock
{
    /// <summary>Editor groups at most (design/shell.md 9.3).</summary>
    public const int MaxGroups = 4;

    /// <summary>Class on the dock control while a moved tab gets its new tab control (see <c>Moved</c>); the tab fills of Motion do not fade under it.</summary>
    public const string MovingClass = "tabsMoving";

    private readonly AvaloniaDockSerializer _serializer = new();
    private readonly Func<string, Control?> _panel;
    private readonly Func<TabInfo, Control?> _editor;
    private DockControl? _control;
    private Point _pointer; // the last pointer position over the dock, to tell where a dragged tab was let go
    private IDocumentDock? _group;

    // The dock a tab strip item belongs to, so the one class handler below reaches the right tab list.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DockControl, StudioDock> Docks = [];

    static StudioDock()
    {
        Control.LoadedEvent.AddClassHandler<DocumentTabStripItem>((item, _) =>
        {
            if (Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<DockControl>(item) is { } control && Docks.TryGetValue(control, out var dock) && item.DataContext is IDockable { Id: { } id } && dock.Tabs.Get(id) is { } tab)
                dock.Hover(item, tab);
        });

        Control.LoadedEvent.AddClassHandler<ToolChromeControl>((chrome, _) => ToolHeaderFit.Watch(chrome));
        Control.LoadedEvent.AddClassHandler<DocumentTabStrip>((strip, _) => TabStripFit.Watch(strip));
        Control.LoadedEvent.AddClassHandler<ToolTabStrip>((strip, _) => TabStripFit.Watch(strip));

        // The arrows that scroll an overflowing tab strip are RepeatButtons in the outer columns of a three column grid inside a strip.
        Control.LoadedEvent.AddClassHandler<RepeatButton>((button, _) =>
        {
            if (button.Parent is not Grid { ColumnDefinitions.Count: 3 }) return;
            var column = Grid.GetColumn(button);
            if (column == 0) button.Classes.Add("tabScrollLeft");
            else if (column == 2) button.Classes.Add("tabScrollRight");
        });
    }

    /// <summary>Called with each new floating window, so it gets what the main window gives its own content (the link pictures and the link handler).</summary>
    public Action<Window>? FloatCreated { get; set; }

    /// <param name="panel">Content of a tool panel by id ("Explorer", ...); "EmptyEditors" is shown in an editor group without tabs.</param>
    /// <param name="editor">Content of an editor tab, or null when it has none.</param>
    public StudioDock(Func<string, Control?>? panel = null, Func<TabInfo, Control?>? editor = null)
    {
        _panel = panel ?? (_ => null);
        _editor = editor ?? (_ => null);
        Factory = _factory = new StudioFactory { Home = this, Leave = Leave, Abort = d => { if (TabItemOf(d) is { } item) G3MStudio.Theme.Motion.Cancel(item); } };
        Layout = Create();
        Factory.DockableClosed += (_, e) =>
        {
            // The last tab of a group leaves its host in the dock's tree until another tab replaces it; without this the editor and what it holds (the open file) stay reachable.
            if (e.Dockable is Document closed) Release(closed);
            if (e.Dockable?.Id is { } id)
            {
                Tabs.Remove(id);
                _history.Forget(id);
            }

            Changed?.Invoke();
        };
        Factory.ActiveDockableChanged += (_, e) =>
        {
            if (e.Dockable?.Owner is IDocumentDock group) _group = group;
            if (e.Dockable is Document doc && !_stepping) _history.Record(doc.Id);
            SwitchFade(e.Dockable);
            Changed?.Invoke();
        };
        Factory.DockableAdded += (_, e) =>
        {
            AnimateTabs();
            OpenMotion(e.Dockable);
            Changed?.Invoke();
        };
        Factory.DockableRemoved += (_, _) => { CloseEmptyFloats(); Changed?.Invoke(); };
        Factory.DockableMoved += (_, _) => { CloseEmptyFloats(); Changed?.Invoke(); };
    }

    /// <summary>Set on a tab's document while it is a preview tab; the tab header style reads it.</summary>
    public static readonly AttachedProperty<bool> IsPreviewProperty = AvaloniaProperty.RegisterAttached<StudioDock, AvaloniaObject, bool>("IsPreview");

    public static readonly IValueConverter ItalicWhenTrue = new FuncValueConverter<bool, FontStyle>(preview => preview ? FontStyle.Italic : FontStyle.Normal);

    public Factory Factory { get; }
    private readonly StudioFactory _factory;

    public IRootDock Layout { get; private set; }

    public EditorTabs Tabs { get; } = new();

    /// <summary>Layout or tabs changed: the owner saves the view state.</summary>
    public event Action? Changed;

    /// <summary>A tab was opened or brought to the front by <see cref="OpenTab"/>.</summary>
    public event Action<TabInfo>? Opened;

    public static IReadOnlyList<string> Panels => AppCommands.Panels;

    // The tab of the long "Function Reference" title shows a short one; menus and the palette keep the full title.
    private static string PanelTitle(string id) => Loc.Current[id == "Reference" ? "panel.referenceTab" : "panel." + id.ToLowerInvariant()];

    /// <summary>Shows the layout in the control; later Restore and Reset swap the layout of the same control.</summary>
    public void Attach(DockControl control)
    {
        _control = control;
        Docks.AddOrUpdate(control, this);
        control.Factory = Factory;
        control.Layout = Layout;
        control.AddHandler(InputElement.PointerMovedEvent, (_, e) => _pointer = e.GetPosition(control), RoutingStrategies.Tunnel, handledEventsToo: true);
        ToolMenu.Hook(control, this);
        AnimateTabs();
    }

    public string Save() => _serializer.Serialize(Layout);

    public ViewState Capture(string? selected) => new(Save(), [.. Tabs.Tabs], selected);

    /// <summary>Puts a saved layout and its tabs back. Tabs whose resource <paramref name="exists"/> no longer is are dropped silently.</summary>
    public void Restore(ViewState state, Func<TabInfo, bool> exists)
    {
        Tabs.Load(state.Tabs);
        try
        {
            Restore(state.Dock);
        }
        catch (FormatException)
        {
            // A damaged saved layout must not keep the file from opening: the default layout stays and the saved tabs are dropped with it.
            Tabs.Load([]);
            return;
        }

        Tabs.Prune(exists);
        foreach (var doc in Documents().Where(d => Tabs.Get(d.Id) is null).ToList()) CloseNow(doc);
        Fill(Layout);
        RetitleTabs(); // the layout holds the titles of the last save, and a nickname may have changed since
    }

    public void Restore(string json)
    {
        IRootDock? layout;
        try
        {
            layout = _serializer.Deserialize<IRootDock>(json);
        }
        catch (System.Text.Json.JsonException e)
        {
            throw new FormatException("The saved layout could not be read.", e);
        }

        Use(layout ?? throw new FormatException("The saved layout could not be read."));
    }

    /// <summary>Places the panels as a saved layout does and keeps the editors that are open (the documents inside the saved layout are dropped).</summary>
    public void ApplyLayout(string json)
    {
        var tabs = Tabs.Tabs.ToList();
        Restore(json);
        foreach (var document in Documents().ToList()) CloseNow(document);
        foreach (var tab in tabs) AddDocument(tab, Group());
        Fill(Layout);
        Changed?.Invoke();
    }

    /// <summary>The Code preset: a narrow Inspector and a tall bottom dock that starts on Problems, for working in code. Every panel is still there and open editors stay.</summary>
    public void ApplyCodePreset() => ApplyPreset("code");

    /// <summary>The Canvas preset (Rooms focus): slim side docks and a short bottom dock, so a room or a sprite gets most of the window. Every panel is still there and open editors stay.</summary>
    public void ApplyCanvasPreset() => ApplyPreset("canvas");

    /// <summary>One of <see cref="LayoutBook.BuiltIn"/>: studio (19, 59, 19 with the bottom dock under the centre), wide, code, canvas or minimal. Open editors stay.</summary>
    public void ApplyPreset(string preset)
    {
        var tabs = Tabs.Tabs.ToList();
        Use(Create(preset));
        foreach (var tab in tabs) AddDocument(tab, Group());
        Changed?.Invoke();
    }

    public T? Find<T>(string id) where T : class, IDockable => All(Layout).OfType<T>().FirstOrDefault(d => d.Id == id);

    public IEnumerable<Document> Documents() => All(Layout).OfType<Document>();

    public Document? DocumentOf(string id) => Documents().FirstOrDefault(d => d.Id == id);

    public IEnumerable<IDocumentDock> Groups() => All(Layout).OfType<IDocumentDock>();

    /// <summary>Opens or focuses the editor tab of a resource. A preview tab replaces the preview tab of the same group.</summary>
    public TabInfo? OpenTab(string kind, string key, bool preview)
    {
        if (Group() is not { } group) return null;
        HangMonitor.Note("open tab " + kind);
        var (tab, replaced, created) = Tabs.Open(kind, key, preview, t => DocumentOf(t.Id)?.Owner == group);
        if (created) AddDocument(tab, group);
        else if (DocumentOf(tab.Id) is { } doc)
        {
            Mark(doc, tab);
            Activate(doc);
        }

        // After the add, so a group that holds only the old preview tab is not removed first.
        if (replaced is not null && DocumentOf(replaced.Id) is { } old) CloseNow(old);
        Tabs.Visit(tab.Id, kind, key);
        Changed?.Invoke();
        Opened?.Invoke(tab);
        return tab;
    }

    /// <summary>Reopens the tab closed last (Ctrl+Shift+T) whose resource still exists; false when none is left.</summary>
    public bool ReopenClosed(Func<string, string, bool> exists)
    {
        if (Tabs.PopClosed(exists) is not { } closed) return false;
        OpenTab(closed.Kind, closed.Key, false);
        return true;
    }

    /// <summary>Brings the next (1) or previous (-1) tab of the group in front, wrapping around.</summary>
    public void CycleTab(int direction)
    {
        if (Group() is not { VisibleDockables: { Count: > 1 } items } group) return;
        var at = items.ToList().FindIndex(d => d == group.ActiveDockable);
        if (items[(Math.Max(at, 0) + direction + items.Count) % items.Count] is Document next) Activate(next);
    }

    /// <summary>Back (-1) or forward (1) through the resources the tab in front showed, skipping the ones that are gone; the tab then shows that resource.</summary>
    public bool Navigate(int direction, Func<string, string, bool> exists, Action<string>? retargeting = null)
    {
        if (ActiveTabId is not { } id || Tabs.Step(id, direction, exists) is not { } tab || DocumentOf(id) is not { } doc) return false;
        retargeting?.Invoke(id);
        doc.Title = Title(tab);
        Show(doc, tab);
        Changed?.Invoke();
        return true;
    }

    /// <summary>The tab shown in the group last used, or null.</summary>
    /// <summary>True when the tab is the front tab of its group, so its editor is on screen.</summary>
    public bool IsInFront(string id) => DocumentOf(id) is { Owner: IDock { ActiveDockable: { } front } } doc && front == doc;

    public string? ActiveTabId => (Group()?.ActiveDockable as Document)?.Id;

    /// <summary>Brings a tool panel to the front of its dock.</summary>
    /// <summary>
    /// Brings a panel to the front. A panel missing from the layout (a layout saved before the panel existed, or one the user closed) is added
    /// to the bottom dock, so no feature needs Reset Layout to show its results.
    /// </summary>
    public void ShowPanel(string id)
    {
        if (Find<IDockable>(id) is not { } panel)
        {
            if (!Panels.Contains(id) || Find<IDock>("BottomDock") is not { } bottom) return;
            var tool = new Tool { Id = id, Title = PanelTitle(id), Content = PanelHost(id, _panel(id) ?? new TextBlock { Text = id }) };
            Factory.AddDockable(bottom, tool);
            panel = tool;
        }

        Factory.SetActiveDockable(panel);
    }

    /// <summary>A resource was renamed: its tabs follow it and show the editor again, because the old one is bound to the old name.</summary>
    public void Rekey(string oldKey, string newKey, Action<string>? retargeting = null)
    {
        Tabs.Rekey(oldKey, newKey);
        foreach (var tab in Tabs.Tabs.Where(t => t.Key == newKey).ToList())
        {
            if (DocumentOf(tab.Id) is not { } doc) continue;
            retargeting?.Invoke(tab.Id);
            Retitle(doc, tab);
            Show(doc, tab);
        }

        Changed?.Invoke();
    }

    // The group last used for a tab, else the first.
    private IDocumentDock? Group() => _group is not null && All(Layout).Contains(_group) ? _group : Groups().FirstOrDefault();

    private void AddDocument(TabInfo tab, IDocumentDock? group)
    {
        if (group is null) return;
        var doc = new Document { Id = tab.Id, Title = Title(tab) };
        Show(doc, tab);
        Mark(doc, tab);
        Factory.AddDockable(group, doc);
        Activate(doc);
    }

    /// <summary>The tab keeps one host control for its whole life and the editor is swapped inside it: the dock does not show a new Content once it has shown one.</summary>
    private void Show(Document doc, TabInfo tab)
    {
        var editor = _editor(tab) ?? new TextBlock { Text = Title(tab) };
        if (doc.Content is ContentControl host) host.Content = editor;
        else doc.Content = new ContentControl { Content = editor, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
    }

    // Pinned tabs have no close button; preview tabs are italic.
    private static void Mark(Document doc, TabInfo tab)
    {
        doc.CanClose = !tab.Pinned;
        doc.SetValue(IsPreviewProperty, tab.Preview);
    }

    /// <summary>The text of a tab title for its tab, when it is not the plain name of its key (a nickname); null shows the name.</summary>
    public Func<TabInfo, string?>? TitleOf { get; set; }

    private string Title(TabInfo tab) => TitleOf?.Invoke(tab) ?? (tab.Key.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0 ? tab.Key[(colon + 1)..] : tab.Key);

    /// <summary>The titles changed without the tabs changing (a nickname was set).</summary>
    public void RetitleTabs()
    {
        foreach (var doc in Documents().ToList())
            if (Tabs.Get(doc.Id) is { } tab) Retitle(doc, tab);
    }

    private void Retitle(Document doc, TabInfo tab)
    {
        doc.Title = Title(tab);
        if (TabItemOf(doc) is { } item) Hover(item, tab); // the card of the tab holds its key and its plain text, so a rename or a new title needs a new one
    }

    // Hovering the tab of a resource shows its card (picture, ID), like the Explorer row of it.
    private void Hover(Control item, TabInfo tab)
    {
        if (ResourceTypes.SplitKey(tab.Key) is not null) G3MStudio.Explorer.HoverCards.Attach(item, tab.Key, Title(tab));
    }

    private void Activate(Document doc)
    {
        Factory.SetActiveDockable(doc);
        if (doc.Owner is IDock owner) Factory.SetFocusedDockable(owner, doc);
    }

    // Walks the docked tree and the floating windows; Factory.Find only sees layouts shown in a control.
    private static IEnumerable<IDockable> All(IRootDock root) => Docked(root).Concat((root.Windows ?? []).Select(w => w.Layout).OfType<IDockable>().SelectMany(Docked));

    private static IEnumerable<IDockable> Docked(IDockable d) => d is IDock dock ? (dock.VisibleDockables ?? []).SelectMany(Docked).Prepend(d) : [d];

    /// <summary>The editor leaves its host and the document its host, so a tab that is gone keeps neither the editor nor what it holds (the open file) reachable through the dock's tree.</summary>
    private static void Release(Document document)
    {
        if (document.Content is ContentControl host) host.Content = null;
        document.Content = null;
    }

    private void Use(IRootDock layout)
    {
        // A control can have one parent only: release the content from the layout that goes away.
        foreach (var old in All(Layout))
        {
            if (old is Tool tool)
            {
                (tool.Content as MinSizeScroller)?.Release();
                tool.Content = null;
            }
            else if (old is Document document) Release(document);
            else if (old is DocumentDock group) group.EmptyContent = null; // its parents are the old tree, which the compositor of a hidden panel still points at
        }

        // The windows of floated panels belong to the old layout: close them, or an empty one stays on screen.
        foreach (var floating in (Layout.Windows ?? []).ToList()) floating.Host?.Exit();
        Layout = layout;
        _group = null; // a group of the old layout would keep it, and the tabs it showed, reachable
        Factory.InitLayout(layout);
        Fill(layout);
        _control?.Layout = layout;
    }

    private IRootDock Create(string preset = "studio")
    {
        var layout = ((StudioFactory)Factory).CreatePreset(preset);
        Factory.InitLayout(layout);
        Fill(layout);
        return layout;
    }

    private void Fill(IRootDock layout)
    {
        foreach (var dockable in All(layout))
        {
            switch (dockable)
            {
                case IToolDock side: LimitWidth(side); break;
                case Tool tool: tool.Title = PanelTitle(tool.Id); tool.Content = PanelHost(tool.Id, _panel(tool.Id) ?? new TextBlock { Text = tool.Title }); break;
                case Document { Id: { } id } document when Tabs.Get(id) is { } tab:
                    document.Content = null;
                    Show(document, tab);
                    Mark(document, tab);
                    break;
                case DocumentDock group: group.EmptyContent = _panel("EmptyEditors"); break;
            }
        }
    }
}
