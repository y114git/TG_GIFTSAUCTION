using G3MStudio.Core.Commands;
using G3MStudio.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
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
public sealed class StudioDock
{
    /// <summary>Editor groups at most (design/shell.md 9.3).</summary>
    public const int MaxGroups = 4;

    private readonly AvaloniaDockSerializer _serializer = new();
    private readonly Func<string, Control?> _panel;
    private readonly Func<TabInfo, Control?> _editor;
    private DockControl? _control;
    private IDocumentDock? _group;

    /// <param name="panel">Content of a tool panel by id ("Explorer", ...); "EmptyEditors" is shown in an editor group without tabs.</param>
    /// <param name="editor">Content of an editor tab, or null when it has none.</param>
    public StudioDock(Func<string, Control?>? panel = null, Func<TabInfo, Control?>? editor = null)
    {
        _panel = panel ?? (_ => null);
        _editor = editor ?? (_ => null);
        Factory = new StudioFactory();
        Layout = Create();
        Factory.DockableClosed += (_, e) =>
        {
            if (e.Dockable?.Id is { } id) Tabs.Remove(id);
            Changed?.Invoke();
        };
        Factory.ActiveDockableChanged += (_, e) =>
        {
            if (e.Dockable?.Owner is IDocumentDock group) _group = group;
            Changed?.Invoke();
        };
        Factory.DockableAdded += (_, _) => Changed?.Invoke();
        Factory.DockableRemoved += (_, _) => Changed?.Invoke();
        Factory.DockableMoved += (_, _) => Changed?.Invoke();
    }

    /// <summary>Set on a tab's document while it is a preview tab; the tab header style reads it.</summary>
    public static readonly AttachedProperty<bool> IsPreviewProperty = AvaloniaProperty.RegisterAttached<StudioDock, AvaloniaObject, bool>("IsPreview");

    public static readonly IValueConverter ItalicWhenTrue = new FuncValueConverter<bool, FontStyle>(preview => preview ? FontStyle.Italic : FontStyle.Normal);

    public Factory Factory { get; }

    public IRootDock Layout { get; private set; }

    public EditorTabs Tabs { get; } = new();

    /// <summary>Layout or tabs changed: the owner saves the view state.</summary>
    public event Action? Changed;

    public static IReadOnlyList<string> Panels => AppCommands.Panels;

    /// <summary>The title of a panel in the current language.</summary>
    private static string PanelTitle(string id) => Loc.Current["panel." + id.ToLowerInvariant()];

    /// <summary>Puts the panel titles in the current language again (after a language change).</summary>
    public void RelabelPanels()
    {
        foreach (var tool in All(Layout).OfType<Tool>()) tool.Title = PanelTitle(tool.Id);
    }

    /// <summary>Shows the layout in the control; later Restore and Reset swap the layout of the same control.</summary>
    public void Attach(DockControl control)
    {
        _control = control;
        control.Factory = Factory;
        control.Layout = Layout;
    }

    public string Save() => _serializer.Serialize(Layout);

    public ViewState Capture(string? selected) => new(Save(), [.. Tabs.Tabs], selected);

    /// <summary>Puts a saved layout and its tabs back. Tabs whose resource <paramref name="exists"/> no longer is are dropped silently.</summary>
    public void Restore(ViewState state, Func<TabInfo, bool> exists)
    {
        Tabs.Load(state.Tabs);
        Restore(state.Dock);
        Tabs.Prune(exists);
        foreach (var doc in Documents().Where(d => Tabs.Get(d.Id) is null).ToList()) Factory.CloseDockable(doc);
        Fill(Layout);
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
        foreach (var document in Documents().ToList()) Factory.CloseDockable(document);
        foreach (var tab in tabs) AddDocument(tab, Group());
        Fill(Layout);
        Changed?.Invoke();
    }

    /// <summary>Back to the default placement; every panel returns, none is lost. Open editors stay (design/shell.md section 10 item 7).</summary>
    public void Reset()
    {
        var tabs = Tabs.Tabs.ToList();
        Use(Create());
        foreach (var tab in tabs) AddDocument(tab, Group());
    }

    public T? Find<T>(string id) where T : class, IDockable => All(Layout).OfType<T>().FirstOrDefault(d => d.Id == id);

    public IEnumerable<Document> Documents() => All(Layout).OfType<Document>();

    public Document? DocumentOf(string id) => Documents().FirstOrDefault(d => d.Id == id);

    public IEnumerable<IDocumentDock> Groups() => All(Layout).OfType<IDocumentDock>();

    /// <summary>Opens or focuses the editor tab of a resource. A preview tab replaces the preview tab of the same group.</summary>
    public TabInfo? OpenTab(string kind, string key, bool preview)
    {
        if (Group() is not { } group) return null;
        var (tab, replaced, created) = Tabs.Open(kind, key, preview, t => DocumentOf(t.Id)?.Owner == group);
        if (created) AddDocument(tab, group);
        else if (DocumentOf(tab.Id) is { } doc)
        {
            Mark(doc, tab);
            Activate(doc);
        }

        // After the add, so a group that holds only the old preview tab is not removed first.
        if (replaced is not null && DocumentOf(replaced.Id) is { } old) Factory.CloseDockable(old);
        Changed?.Invoke();
        return tab;
    }

    /// <summary>The tab shown in the group last used, or null.</summary>
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
            var tool = new Tool { Id = id, Title = PanelTitle(id), Content = _panel(id) ?? new TextBlock { Text = id } };
            Factory.AddDockable(bottom, tool);
            panel = tool;
        }

        Factory.SetActiveDockable(panel);
    }

    /// <summary>A resource was renamed: its tabs follow it.</summary>
    public void Rekey(string oldKey, string newKey)
    {
        Tabs.Rekey(oldKey, newKey);
        foreach (var tab in Tabs.Tabs.Where(t => t.Key == newKey))
            if (DocumentOf(tab.Id) is { } doc) doc.Title = Title(tab);
        Changed?.Invoke();
    }

    public void CloseTab(string id)
    {
        if (DocumentOf(id) is { } doc) Factory.CloseDockable(doc);
    }

    /// <summary>Closes every tab that is not pinned.</summary>
    public void CloseAll()
    {
        foreach (var doc in Documents().Where(d => Tabs.Get(d.Id)?.Pinned != true).ToList()) Factory.CloseDockable(doc);
    }

    public void SetPinned(string id, bool pinned)
    {
        Tabs.SetPinned(id, pinned);
        if (DocumentOf(id) is not { Owner: IDock group } doc || Tabs.Get(id) is not { } tab) return;
        Mark(doc, tab);
        // Pinned tabs sit first in the strip (design/shell.md 9.1).
        Factory.RemoveDockable(doc, false);
        var items = group.VisibleDockables ?? [];
        var index = pinned ? items.Count(d => Tabs.Get(d.Id ?? "")?.Pinned == true) : items.Count;
        Factory.InsertDockable(group, doc, index);
        Activate(doc);
        Changed?.Invoke();
    }

    public void KeepOpen(string id)
    {
        Tabs.KeepOpen(id);
        if (DocumentOf(id) is { } doc && Tabs.Get(id) is { } tab) Mark(doc, tab);
        Changed?.Invoke();
    }

    /// <summary>Moves the tab into a new group beside its group; ignored beyond <see cref="MaxGroups"/> groups.</summary>
    public void SplitTab(string id, DockOperation side)
    {
        if (DocumentOf(id) is not { Owner: IDock from } doc || from.VisibleDockables?.Count < 2 || Groups().Count() >= MaxGroups) return;
        // SplitToDock wraps a dockable in a plain proportional dock; handing it an empty group makes the group the sibling.
        var group = new DocumentDock { CanCreateDocument = false, VisibleDockables = Factory.CreateList<IDockable>() };
        Factory.SplitToDock(from, group, side);
        Factory.MoveDockable(from, group, doc, null);
        Activate(doc);
    }

    // The group last used for a tab, else the first.
    private IDocumentDock? Group() => _group is not null && All(Layout).Contains(_group) ? _group : Groups().FirstOrDefault();

    private void AddDocument(TabInfo tab, IDocumentDock? group)
    {
        if (group is null) return;
        var doc = new Document { Id = tab.Id, Title = Title(tab), Content = _editor(tab) ?? new TextBlock { Text = Title(tab) } };
        Mark(doc, tab);
        Factory.AddDockable(group, doc);
        Activate(doc);
    }

    // Pinned tabs have no close button; preview tabs are italic.
    private static void Mark(Document doc, TabInfo tab)
    {
        doc.CanClose = !tab.Pinned;
        doc.SetValue(IsPreviewProperty, tab.Preview);
    }

    private static string Title(TabInfo tab) => tab.Key.IndexOf(':', StringComparison.Ordinal) is var colon and >= 0 ? tab.Key[(colon + 1)..] : tab.Key;

    private void Activate(Document doc)
    {
        Factory.SetActiveDockable(doc);
        if (doc.Owner is IDock owner) Factory.SetFocusedDockable(owner, doc);
    }

    // Walks the docked tree and the floating windows; Factory.Find only sees layouts shown in a control.
    private static IEnumerable<IDockable> All(IRootDock root)
    {
        static IEnumerable<IDockable> Walk(IDockable d) =>
            d is IDock dock ? (dock.VisibleDockables ?? []).SelectMany(Walk).Prepend(d) : [d];
        return Walk(root).Concat((root.Windows ?? []).Where(w => w.Layout is not null).SelectMany(w => Walk(w.Layout!)));
    }

    private void Use(IRootDock layout)
    {
        // A control can have one parent only: release the content from the layout that goes away.
        foreach (var old in All(Layout))
        {
            if (old is Tool tool) tool.Content = null;
            else if (old is Document document) document.Content = null;
        }

        Layout = layout;
        Factory.InitLayout(layout);
        Fill(layout);
        _control?.Layout = layout;
    }

    private IRootDock Create()
    {
        var layout = Factory.CreateLayout();
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
                case Tool tool:
                    tool.Title = PanelTitle(tool.Id);
                    tool.Content = _panel(tool.Id) ?? new TextBlock { Text = tool.Title };
                    break;
                case Document { Id: { } id } document when Tabs.Get(id) is { } tab:
                    document.Content = _editor(tab) ?? new TextBlock { Text = document.Title };
                    Mark(document, tab);
                    break;
                case DocumentDock group: group.EmptyContent = _panel("EmptyEditors"); break;
            }
        }
    }

    private sealed class StudioFactory : Factory
    {
        public override IRootDock CreateLayout()
        {
            Tool Panel(string id) => new() { Id = id, Title = PanelTitle(id) };
            ToolDock Tools(string id, Alignment side, double size, params Tool[] tools) =>
                new() { Id = id, Alignment = side, Proportion = size, VisibleDockables = CreateList<IDockable>(tools), ActiveDockable = tools[0] };

            var editors = new DocumentDock { Id = "Editors", CanCreateDocument = false, IsCollapsable = false, VisibleDockables = CreateList<IDockable>() };
            var middle = new ProportionalDock
            {
                Id = "Middle",
                Orientation = Orientation.Horizontal,
                VisibleDockables = CreateList<IDockable>(
                    Tools("LeftDock", Alignment.Left, 0.2, Panel("Explorer"), Panel("Search")),
                    new ProportionalDockSplitter(),
                    editors,
                    new ProportionalDockSplitter(),
                    Tools("RightDock", Alignment.Right, 0.2, Panel("Inspector"))),
            };
            var main = new ProportionalDock
            {
                Id = "Main",
                Orientation = Orientation.Vertical,
                VisibleDockables = CreateList<IDockable>(middle, new ProportionalDockSplitter(), Tools("BottomDock", Alignment.Bottom, 0.16, Panel("History"), Panel("Git"), Panel("Problems"), Panel("Output"), Panel("Usages"))),
            };
            var root = CreateRootDock();
            root.Id = "Root";
            root.VisibleDockables = CreateList<IDockable>(main);
            root.ActiveDockable = main;
            root.DefaultDockable = main;
            return root;
        }

        public override void InitLayout(IDockable layout)
        {
            HostWindowLocator = new Dictionary<string, Func<IHostWindow?>> { [nameof(IDockWindow)] = () => new HostWindow() };
            base.InitLayout(layout);
        }
    }
}
