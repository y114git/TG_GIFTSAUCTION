using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using G3MStudio.Core.Workspace;
using G3MStudio.Docking;

namespace G3MStudio.Tests;

public sealed class DockingTests
{
    private static readonly string[] TabNames = ["a", "b", "c", "d", "e"];

    private static string Describe(IDockable d) =>
        d is IDock dock ? $"{d.Id ?? d.GetType().Name}[{string.Join(",", (dock.VisibleDockables ?? []).Select(Describe))}]" : d.Id ?? d.GetType().Name;

    private static string Whole(StudioDock dock) =>
        Describe(dock.Layout) + " floating=" + string.Join(";", (dock.Layout.Windows ?? []).Select(w => Describe(w.Layout!)));

    private static Task Run(Action<StudioDock, Window> test) => HeadlessApp.Run(() =>
    {
        var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
        var control = new DockControl();
        dock.Attach(control);
        var window = new Window { Width = 1000, Height = 700, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try { test(dock, window); }
        finally
        {
            foreach (var floating in (dock.Layout.Windows ?? []).ToList()) floating.Host?.Exit();
            window.Close();
        }
    });

    private static string Open(StudioDock dock, string name, bool preview = false) => dock.OpenTab("resource", "sprites:" + name, preview)!.Id;

    private static T Panel<T>(StudioDock dock, string id) where T : class, IDockable => dock.Find<T>(id) ?? throw new InvalidOperationException(id);

    private static string Titles(IDocumentDock group) => string.Join(",", (group.VisibleDockables ?? []).Select(d => d.Title));

    [Fact]
    public Task DefaultLayoutShowsTheDocksAndTheirContent() => Run((dock, window) =>
    {
        Assert.Equal(3, window.GetVisualDescendants().OfType<ToolDockControl>().Count());
        Assert.NotEmpty(window.GetVisualDescendants().OfType<DocumentDockControl>());
        Assert.StartsWith("Root[Main[Middle[LeftDock[Explorer,Search]", Whole(dock), StringComparison.Ordinal);
        Assert.Contains("BottomDock[History,Git,Problems,Output,Usages]", Whole(dock), StringComparison.Ordinal);
    });

    [Fact]
    public Task ShowPanelBringsBackAPanelMissingFromTheLayout() => Run((dock, _) =>
    {
        dock.Factory.CloseDockable(Panel<IDockable>(dock, "Usages"));
        Assert.DoesNotContain("Usages", Whole(dock), StringComparison.Ordinal);
        dock.ShowPanel("Usages");
        Assert.Contains("BottomDock[History,Git,Problems,Output,Usages]", Whole(dock), StringComparison.Ordinal);
        dock.ShowPanel("Usages");
        Assert.Equal(1, Whole(dock).Split("Usages").Length - 1);
        dock.ShowPanel("NoSuchPanel"); // nothing happens
    });

    [Fact]
    public Task DockMovesAPanelToAnotherDock() => Run((dock, _) =>
    {
        var explorer = Panel<IDockable>(dock, "Explorer");
        dock.Factory.MoveDockable((IDock)explorer.Owner!, Panel<IDock>(dock, "BottomDock"), explorer, null);
        var layout = Whole(dock);
        Assert.Contains("LeftDock[Search]", layout, StringComparison.Ordinal);
        Assert.Contains("BottomDock[History,Git,Problems,Output,Explorer,Usages]", layout, StringComparison.Ordinal);
    });

    [Fact]
    public Task FloatOpensAWindowOwnedByTheLayout() => Run((dock, _) =>
    {
        dock.Factory.FloatDockable(Panel<IDockable>(dock, "Inspector"));
        Dispatcher.UIThread.RunJobs();
        Assert.Single(dock.Layout.Windows!);
        Assert.Contains("floating=", Whole(dock), StringComparison.Ordinal);
        Assert.Contains("Inspector", Whole(dock).Split("floating=")[1], StringComparison.Ordinal);
        Assert.DoesNotContain("RightDock[Inspector]", Whole(dock).Split("floating=")[0], StringComparison.Ordinal);
    });

    [Fact]
    public Task ClosedPanelIsGoneUntilReset() => Run((dock, _) =>
    {
        dock.Factory.CloseDockable(Panel<IDockable>(dock, "Search"));
        Assert.Null(dock.Find<IDockable>("Search"));
        dock.Reset();
        Assert.NotNull(dock.Find<IDockable>("Search"));
    });

    [Fact]
    public Task AnEditorOpensAsATabInTheEditorGroup() => Run((dock, window) =>
    {
        Open(dock, "spr_a");
        Open(dock, "spr_b");
        Assert.Equal("spr_a,spr_b", Titles(dock.Groups().Single()));
        Assert.Equal(Open(dock, "spr_a"), dock.ActiveTabId);
        Assert.Equal(2, dock.Tabs.Tabs.Count);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "sprites:spr_a");
    });

    [Fact]
    public Task APreviewTabIsReplacedByTheNextPreviewAndKeptByANormalOpen() => Run((dock, _) =>
    {
        Open(dock, "spr_a", preview: true);
        Open(dock, "spr_b", preview: true);
        Assert.Equal("spr_b", Titles(dock.Groups().Single()));
        Assert.True(dock.Tabs.Tabs.Single().Preview);

        Open(dock, "spr_b");
        Assert.False(dock.Tabs.Tabs.Single().Preview);
        Open(dock, "spr_c", preview: true);
        Assert.Equal("spr_b,spr_c", Titles(dock.Groups().Single()));
    });

    [Fact]
    public Task APreviewTabHeaderIsItalicUntilTheTabIsKept() => Run((dock, window) =>
    {
        var id = Open(dock, "spr_a", preview: true);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var header = window.GetVisualDescendants().OfType<DocumentTabStripItem>().Single();
        Assert.Equal(FontStyle.Italic, header.FontStyle);
        dock.KeepOpen(id);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(FontStyle.Normal, header.FontStyle);
    });

    [Fact]
    public Task PinnedTabsMoveFirstAndHaveNoCloseButton() => Run((dock, _) =>
    {
        Open(dock, "spr_a");
        var b = Open(dock, "spr_b");
        dock.SetPinned(b, true);
        Assert.Equal("spr_b,spr_a", Titles(dock.Groups().Single()));
        Assert.False(dock.DocumentOf(b)!.CanClose);

        dock.CloseAll();
        Assert.Equal("spr_b", Titles(dock.Groups().Single()));
        dock.SetPinned(b, false);
        Assert.True(dock.DocumentOf(b)!.CanClose);
    });

    [Fact]
    public Task SplitMovesTheTabToANewGroupAndAtMostFourGroupsExist() => Run((dock, _) =>
    {
        var ids = TabNames.Select(n => Open(dock, "spr_" + n)).ToArray();
        dock.SplitTab(ids[1], DockOperation.Right);
        Assert.Equal(2, dock.Groups().Count());
        dock.SplitTab(ids[2], DockOperation.Bottom);
        dock.SplitTab(ids[3], DockOperation.Right);
        Assert.Equal(4, dock.Groups().Count());
        dock.SplitTab(ids[4], DockOperation.Right);
        Assert.Equal(4, dock.Groups().Count());
    });

    [Fact]
    public Task ClosingATabForgetsItAndTheLastTabLeavesAnEmptyGroup() => Run((dock, _) =>
    {
        var id = Open(dock, "spr_a");
        dock.CloseTab(id);
        Assert.Empty(dock.Tabs.Tabs);
        Assert.Empty(dock.Documents());
        Assert.Single(dock.Groups());
        Open(dock, "spr_b");
        Assert.Equal("spr_b", Titles(dock.Groups().Single()));
    });

    [Fact]
    public Task SavedViewRestoresTabsLayoutAndContentAndDropsMissingResources() => Run((dock, _) =>
    {
        var explorer = Panel<IDockable>(dock, "Explorer");
        dock.Factory.MoveDockable((IDock)explorer.Owner!, Panel<IDock>(dock, "BottomDock"), explorer, null);
        Open(dock, "spr_a");
        var b = Open(dock, "spr_b", preview: true);
        var c = Open(dock, "spr_c");
        dock.SetPinned(c, true);
        dock.SplitTab(Open(dock, "spr_gone"), DockOperation.Right);
        dock.Factory.FloatDockable(Panel<IDockable>(dock, "Inspector"));
        dock.Factory.CloseDockable(Panel<IDockable>(dock, "Search"));
        var state = dock.Capture("sprites:spr_b");

        var later = new StudioDock(editor: tab => new TextBlock { Text = "later " + tab.Key });
        later.Restore(state, tab => !tab.Key.EndsWith("spr_gone", StringComparison.Ordinal));
        try
        {
            Assert.Equal(["spr_c", "spr_a", "spr_b"], [.. later.Documents().Select(d => d.Title)]);
            Assert.True(later.Tabs.Get(b)!.Preview);
            Assert.True(later.Tabs.Get(c)!.Pinned);
            Assert.False(later.DocumentOf(c)!.CanClose);
            Assert.Equal("later sprites:spr_a", ((TextBlock)later.Documents().Single(d => d.Title == "spr_a").Content!).Text);
            Assert.Contains("BottomDock[History,Git,Problems,Output,Explorer,Usages]", Whole(later), StringComparison.Ordinal);
            Assert.DoesNotContain("Search", Whole(later), StringComparison.Ordinal);
            Assert.Single(later.Layout.Windows!);
            Assert.DoesNotContain(later.Tabs.Tabs, t => t.Key.EndsWith("spr_gone", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var floating in (later.Layout.Windows ?? []).ToList()) floating.Host?.Exit();
        }
    });

    [Fact]
    public Task ResetRestoresTheDefaultPlacementAndKeepsTheOpenTabs() => Run((dock, _) =>
    {
        var original = Whole(dock);
        var explorer = Panel<IDockable>(dock, "Explorer");
        dock.Factory.MoveDockable((IDock)explorer.Owner!, Panel<IDock>(dock, "BottomDock"), explorer, null);
        Open(dock, "spr_a");
        dock.SplitTab(Open(dock, "spr_b"), DockOperation.Right);
        dock.Factory.FloatDockable(Panel<IDockable>(dock, "Inspector"));
        Assert.NotEqual(original, Whole(dock));
        dock.Reset();
        Assert.Empty(dock.Layout.Windows ?? []);
        Assert.Single(dock.Groups());
        Assert.Equal(2, dock.Documents().Count());
        Assert.Contains("LeftDock[Explorer,Search]", Whole(dock), StringComparison.Ordinal);
    });
}

public sealed class EditorTabsTests
{
    private static bool SameGroup(TabInfo _) => true;

    [Fact]
    public void ANormalOpenOfAPreviewTabKeepsItAndOneTabExistsPerResource()
    {
        var tabs = new EditorTabs();
        var (first, _, created) = tabs.Open("resource", "sprites:a", true, SameGroup);
        Assert.True(created);
        var (again, _, createdAgain) = tabs.Open("resource", "sprites:a", false, SameGroup);
        Assert.False(createdAgain);
        Assert.Equal(first.Id, again.Id);
        Assert.False(tabs.Tabs.Single().Preview);
    }

    [Fact]
    public void PinningEndsPreviewAndPruneDropsMissingResources()
    {
        var tabs = new EditorTabs();
        var a = tabs.Open("resource", "sprites:a", true, SameGroup).Tab;
        tabs.Open("code", "code:b", false, SameGroup);
        tabs.SetPinned(a.Id, true);
        Assert.Equal((false, true), (tabs.Get(a.Id)!.Preview, tabs.Get(a.Id)!.Pinned));
        var gone = tabs.Prune(t => t.Key != "code:b");
        Assert.Equal(["code:b"], gone.Select(t => t.Key));
        Assert.Single(tabs.Tabs);
    }

    [Fact]
    public void ViewStateRoundTripsAndADamagedFileReadsAsNone()
    {
        using var dir = new TempDir();
        var store = new ViewStateStore(new UserData(dir.File("user")));
        Assert.Null(store.Load("C:/game/data.win"));
        var state = new ViewState("{}", [new TabInfo("t1", "code", "code:x", false, true)], "code:x");
        store.Save("C:/game/data.win", state);
        var loaded = store.Load("C:/game/data.win")!;
        Assert.Equal("code:x", loaded.Selected);
        Assert.Equal(state.Tabs, loaded.Tabs);
        File.WriteAllText(Directory.GetFiles(dir.File("user"), "*.json", SearchOption.AllDirectories).Single(), "{ broken");
        Assert.Null(store.Load("C:/game/data.win"));
    }
}
