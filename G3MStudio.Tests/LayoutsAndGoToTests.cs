using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Navigation;
using G3MStudio.Core.Workspace;
using G3MStudio.Docking;

namespace G3MStudio.Tests;

public sealed class LayoutsAndGoToTests
{
    private static readonly GoToEntry[] Entries =
    [
        new(GoToKind.Resource, "obj_player", "objects:obj_player", "objects"),
        new(GoToKind.Resource, "obj_enemy", "objects:obj_enemy", "objects"),
        new(GoToKind.Resource, "spr_player", "sprites:spr_player", "sprites"),
        new(GoToKind.Code, "gml_Script_player_move", "code:gml_Script_player_move", "code"),
        new(GoToKind.Function, "player_jump", "functions:player_jump", "functions"),
        new(GoToKind.Variable, "player_hp", "variables:player_hp", "variables"),
        new(GoToKind.Text, "player died", "strings:player died", "strings"),
        new(GoToKind.Command, "Save", "file.save", "", "Ctrl+S"),
        new(GoToKind.Setting, "Appearance", "appearance"),
        new(GoToKind.Tab, "obj_player", "objects:obj_player", "resource"),
    ];

    private static string[] Names(string query, params string[] recent) => [.. GoToSearch.Run(Entries, query, recent).Select(e => e.Name)];

    [Fact]
    public void NamesOfThingsComeBeforeStringsFunctionsAndVariablesWhenNoPrefixIsTyped()
    {
        // "player_hp" starts with the needle and the object only contains it, but the object is what a person types a name for
        var found = GoToSearch.Run([new(GoToKind.Variable, "playerbuffer", "variables:playerbuffer", "variables"), new(GoToKind.Resource, "obj_player", "objects:obj_player", "objects")], "player", []);
        Assert.Equal("obj_player", found[0].Name);
        Assert.Equal("playerbuffer", GoToSearch.Run([.. Entries, new GoToEntry(GoToKind.Variable, "playerbuffer", "variables:playerbuffer", "variables")], "v:playerb", [])[0].Name);
    }

    private static readonly string[] Ranked = ["player", "player_hp", "my_player", "p_l_a_y_e_r"];

    [Fact]
    public void FuzzyRanksExactThenStartThenSubstringThenLetters()
    {
        var scores = Ranked.Select(t => Fuzzy.Score("player", t)).ToArray();
        Assert.Equal(scores.OrderByDescending(s => s), scores);
        Assert.Equal(Fuzzy.Exact, scores[0]);
        Assert.True(Fuzzy.Score("oph", "obj_player_hit") > 0);
        Assert.True(Fuzzy.Score("xyz", "obj_player_hit") < 0);
        Assert.True(Fuzzy.Score("ob", "obj_x") > Fuzzy.Score("ob", "my_obj_x"));
    }

    [Fact]
    public void GoToPutsTheBestNameFirstAndPrefersWhatWasOpenedLast()
    {
        Assert.Equal("spr_player", Names("spr_pl")[0]);
        Assert.Equal("player_jump", Names("@player_j")[0]);
        // Same score: the recent key wins.
        Assert.Equal("spr_player", Names("_pla", "sprites:spr_player")[0]);
        Assert.Equal("obj_player", Names("_pla", "objects:obj_player")[0]);
        Assert.Equal(["obj_player"], Names("", "objects:obj_player", "objects:missing"));
        // A resource and the tab that shows it are one result.
        Assert.Single(Names("obj_player"), "obj_player");
    }

    [Fact]
    public void GoToPrefixesNarrowTheKindAndTheResourceType()
    {
        Assert.Equal(["obj_enemy", "obj_player"], Names("o:"));
        Assert.Equal(["spr_player"], Names("s:pl"));
        Assert.Equal(["Save"], Names(">sa"));
        Assert.Equal(["player_hp", "player_jump"], Names("@play"));
        Assert.Equal(["player died"], Names("#play"));
        Assert.Equal(["gml_Script_player_move"], Names("c:play"));
        Assert.Equal(["Appearance"], Names("p:app"));
        Assert.Equal(["obj_player"], Names("t:obj"));
    }

    [Fact]
    public void BigListsStayOutOfAPlainSearchUntilTwoLetters()
    {
        Assert.DoesNotContain("player_hp", Names("p"));
        Assert.Contains("player_hp", Names("pl"));
    }

    [Fact]
    public void LayoutBookSavesRenamesAndDeletesAndRefusesBuiltInNames()
    {
        IReadOnlyDictionary<string, string> book = new Dictionary<string, string>();
        book = LayoutBook.Save(book, " mine ", "{}");
        Assert.Equal(["mine"], LayoutBook.Names(book));
        Assert.Same(book, LayoutBook.Save(book, "code", "{}"));
        Assert.Same(book, LayoutBook.Save(book, "  ", "{}"));
        book = LayoutBook.Save(book, "other", "{1}");
        Assert.Same(book, LayoutBook.Rename(book, "mine", "other"));
        book = LayoutBook.Rename(book, "mine", "renamed");
        Assert.Equal(["other", "renamed"], LayoutBook.Names(book));
        Assert.Equal("{}", book["renamed"]);
        Assert.Equal(["other"], LayoutBook.Names(LayoutBook.Delete(book, "renamed")));
    }

    private static Task RunDock(Action<StudioDock, Window> test) => HeadlessApp.Run(() =>
    {
        var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
        var control = new DockControl();
        dock.Attach(control);
        var window = new Window { Width = 1000, Height = 700, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try { test(dock, window); }
        finally { window.Close(); }
    });

    private static double Width(Window window, string id) => window.GetVisualDescendants().OfType<ToolDockControl>().First(c => (c.DataContext as IToolDock)?.Id == id).Bounds.Width;

    [Fact]
    public Task TheDefaultLayoutIs19_59_19WithTheBottomDockUnderTheCentreOnly() => RunDock((dock, window) =>
    {
        Assert.Equal(0.19, dock.Find<IToolDock>("LeftDock")!.Proportion, 3);
        Assert.Equal(0.19, dock.Find<IToolDock>("RightDock")!.Proportion, 3);
        var centre = dock.Find<IDock>("Centre")!;
        Assert.Contains(centre.VisibleDockables!, d => d.Id == "BottomDock");
        Assert.Contains(centre.VisibleDockables!, d => d.Id == "Editors");
        Assert.InRange(Width(window, "LeftDock") / window.Bounds.Width, 0.15, 0.23);
        Assert.InRange(Width(window, "BottomDock") / window.Bounds.Width, 0.5, 0.65);
    });

    [Fact]
    public Task EveryBuiltInPresetKeepsEveryPanelAndEditor() => RunDock((dock, _) =>
    {
        dock.OpenTab("resource", "sprites:spr_a", false);
        foreach (var preset in LayoutBook.BuiltIn)
        {
            dock.ApplyPreset(preset);
            foreach (var panel in StudioDock.Panels) Assert.NotNull(dock.Find<Dock.Model.Core.IDockable>(panel));
            Assert.Equal(["spr_a"], dock.Documents().Select(d => d.Title));
        }

        dock.ApplyPreset("wide");
        Assert.True(dock.Find<IToolDock>("LeftDock")!.Proportion < 0.19);
        dock.ApplyPreset("minimal");
        Assert.True(dock.IsFolded("right") && dock.IsFolded("bottom") && !dock.IsFolded("left"));
    });

    [Fact]
    public Task AFoldedDockTakesNoWidthAndOpensAgainWithItsShare() => RunDock((dock, window) =>
    {
        dock.ToggleDock("left");
        Dispatcher.UIThread.RunJobs();
        Assert.True(dock.IsFolded("left"));
        Assert.True(Width(window, "LeftDock") < 8);
        dock.ToggleDock("left");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0.19, dock.Find<IToolDock>("LeftDock")!.Proportion, 3);
        Assert.True(Width(window, "LeftDock") > 100);
    });

    [Fact]
    public Task HideAllThenShowAllFoldsAndOpensEveryDock() => RunDock((dock, _) =>
    {
        dock.ToggleAll();
        Assert.True(dock.AllFolded);
        dock.ToggleAll();
        Assert.False(dock.IsFolded("left") || dock.IsFolded("right") || dock.IsFolded("bottom"));
        dock.ToggleDock("right");
        dock.ToggleAll(); // some are open: fold the rest
        Assert.True(dock.AllFolded);
        dock.ShowAll();
        Assert.False(dock.IsFolded("right"));
    });

    [Fact]
    public Task ASavedLayoutRoundTripsThroughTheSettingsText() => RunDock((dock, _) =>
    {
        dock.OpenTab("resource", "sprites:spr_a", false);
        dock.ApplyPreset("code");
        dock.ToggleDock("right");
        var json = dock.Save();
        var book = LayoutBook.Save(new Dictionary<string, string>(), "mine", json);
        dock.Reset();
        Assert.False(dock.IsFolded("right"));
        dock.ApplyLayout(book["mine"]);
        Assert.True(dock.IsFolded("right"));
        Assert.Equal(0.18, dock.Find<IToolDock>("LeftDock")!.Proportion, 3);
        Assert.Equal(["spr_a"], dock.Documents().Select(d => d.Title)); // the open editor stayed
    });

    [Fact]
    public void ATooltipShowsTheShortcutFromTheLiveKeymap()
    {
        var registry = AppCommands.Create(new FakeShell());
        Assert.Equal("Save (Ctrl+S)", KeyTips.For(registry, "file.save", "Save"));
        registry.SetShortcut("file.save", "Ctrl+Alt+S");
        Assert.Equal("Save (Ctrl+Alt+S)", KeyTips.For(registry, "file.save", "Save"));
        registry.SetShortcut("file.save", null);
        Assert.Equal("Save", KeyTips.For(registry, "file.save", "Save"));
        Assert.Equal("Go To (Ctrl+T)", KeyTips.For(registry, "go.goTo", "Go To"));
        Assert.Equal("Ctrl+P", registry.Get("go.quickOpen").Shortcut);
    }

    [Fact]
    public void LayoutAndGoToCommandsAreRegisteredAndEveryMenuIdExists()
    {
        var registry = AppCommands.Create(new FakeShell());
        foreach (var id in AppCommands.Menus.SelectMany(m => m.Ids).Concat(AppCommands.ResourceMenu).Concat(AppCommands.TabMenu).Where(id => id != "-")) Assert.NotNull(registry.Find(id));
        Assert.Contains(AppCommands.Menus, m => m.TitleKey == "menu.layout" && m.Ids.Contains("dock.toggleAll") && m.Ids.Contains("window.showAll") && m.Ids.Contains("window.closeAll"));
        Assert.NotEmpty(AppCommands.MenuContexts);
    }

    [Fact]
    public async Task MenusFollowTheTabAndMenuItemsCarryTheirKeyInTheTooltip()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                MenuItem Top(string key) => window.MainMenu.Items.OfType<MenuItem>().Single(m => Avalonia.Automation.AutomationProperties.GetAutomationId(m) == key);
                MenuItem Leaf(string id) => window.MainMenu.Items.OfType<MenuItem>().SelectMany(m => m.Items.OfType<MenuItem>()).Single(m => Avalonia.Automation.AutomationProperties.GetAutomationId(m) == id);
                Assert.False(Top("menu.room").IsVisible);
                Assert.False(Top("menu.image").IsVisible);
                Assert.False(Top("menu.code").IsVisible);
                window.Registry.SetContext(AppCommands.RoomTab, true);
                Assert.True(Top("menu.room").IsVisible);
                Assert.False(Top("menu.image").IsVisible);
                window.Registry.SetContext(AppCommands.RoomTab, false);
                window.Registry.SetContext(AppCommands.ImageTab, true);
                Assert.True(Top("menu.image").IsVisible);
                Assert.False(Top("menu.room").IsVisible);
                Assert.True(Top("menu.layout").IsVisible);

                Assert.EndsWith("(Ctrl+S)", (string?)ToolTip.GetTip(Leaf("file.save")), StringComparison.Ordinal);
                window.Registry.SetShortcut("file.save", "Ctrl+Alt+K");
                Assert.EndsWith("(Ctrl+Alt+K)", (string?)ToolTip.GetTip(Leaf("file.save")), StringComparison.Ordinal);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task GoToOpensAResourceBesideAndRunsACommandFromThePrefix()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.True(window.Registry.Execute("go.goTo"));
                var box = window.GoToBox!;
                box.Text = "o:obj_1";
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("obj_1", box.Items[0].Name);
                box.Choose(GoToMode.Open);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(["obj_1"], window.Docking.Documents().Select(d => d.Title));
                Assert.Contains("objects:obj_1", window.Explorer.Model.Quick.Recent);

                window.Registry.Execute("go.goTo");
                window.GoToBox!.Text = "o:obj_2";
                Dispatcher.UIThread.RunJobs();
                window.GoToBox.Choose(GoToMode.Beside);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, window.Docking.Groups().Count());

                window.Registry.Execute("go.goTo");
                window.GoToBox!.Text = ">hide or show";
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("dock.toggleAll", window.GoToBox.Items[0].Key);
                window.GoToBox.Choose(GoToMode.Open);
                Assert.True(window.Docking.AllFolded);

                // The palette key opens Go To with the command prefix; quick open lists resources and code only.
                Assert.True(window.Registry.Execute("view.commandPalette"));
                Assert.Equal(">", window.GoToBox!.Text);
                window.GoToBox.Close();
                Assert.True(window.Registry.Execute("go.quickOpen"));
                window.GoToBox!.Text = "obj_";
                Dispatcher.UIThread.RunJobs();
                Assert.All(window.GoToBox.Items, e => Assert.True(e.Kind is GoToKind.Resource or GoToKind.Code));
                window.GoToBox.Text = ">hide or show";
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(window.GoToBox.Items);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ACommandIsFoundByItsAlternativeText()
    {
        var entries = new[] { new GoToEntry(GoToKind.Command, "Apply Mod", "mods.applyMod", "", "", "Where do I put the result"), new GoToEntry(GoToKind.Command, "Other", "x") };
        Assert.Equal(["mods.applyMod"], GoToSearch.Run(entries, ">where do", []).Select(e => e.Key));
    }

    private static string[] Headers(IEnumerable<Control> items) => [.. items.OfType<MenuItem>().Select(i => i.Header as string ?? "")];

    [Fact]
    public async Task EveryResourceMenuHasTheSameCommandsInTheSameOrder()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var tab = window.Docking.OpenTab("resource", "objects:obj_0", false)!;
                var resource = Headers(window.ResourceMenuFor("objects:obj_0"));
                Assert.StartsWith("Rename", resource[0], StringComparison.Ordinal);
                Assert.Contains("Reveal in Explorer", resource);
                var tabMenu = Headers(window.TabMenu(tab.Id).Items.OfType<Control>());
                Assert.Equal(resource, tabMenu[^resource.Length..]);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void QuickAccessKeepsRecentFavouritesSearchesAndTagsAndFollowsRenames()
    {
        var q = QuickAccess.Empty.Touch("a").Touch("b").Touch("a");
        Assert.Equal(["a", "b"], q.Recent);
        q = q.ToggleFavourite("a").AddSearch("spr:").AddSearch("spr:").AddTag("a", "boss").AddTag("b", "boss").AddTag("a", "boss");
        Assert.Equal(["a"], q.Favourites);
        Assert.Equal(["spr:"], q.Searches);
        Assert.Equal(["a", "b"], q.KeysOf("boss"));
        Assert.Equal(["boss"], q.TagsOf("a"));
        q = q.Rekey("a", "c").Rekey("b", null);
        Assert.Equal(["c"], q.Recent);
        Assert.Equal(["c"], q.KeysOf("boss"));
        Assert.Empty(q.ToggleFavourite("c").Favourites);
        var round = System.Text.Json.JsonSerializer.Deserialize<QuickAccess>(System.Text.Json.JsonSerializer.Serialize(q))!;
        Assert.Equal(q.KeysOf("boss"), round.KeysOf("boss"));
        Assert.Equal(q.Recent, round.Recent);
    }

    [Fact]
    public void TheExplorerListsQuickAccessAndAppliesSortTypeAndTagFilters()
    {
        using var data = TestData.Create(3, withCode: true);
        var model = new ExplorerModel(key => key);
        model.Load(data);
        Assert.Equal(ExplorerModel.QuickGroup, model.Rows[0].Key);
        model.Toggle(ExplorerModel.QuickRecent);
        model.Toggle(ExplorerModel.QuickFavourites);
        model.Toggle(ExplorerModel.QuickTags);
        model.Quick = QuickAccess.Empty.Touch("objects:obj_2").Touch("objects:obj_1").ToggleFavourite("objects:obj_0").AddTag("objects:obj_2", "boss");
        var rows = model.Rows;
        var recent = rows.SkipWhile(r => r.Key != ExplorerModel.QuickRecent).Skip(1).TakeWhile(r => r.IsItem).Select(r => r.Text);
        Assert.Equal(["obj_1", "obj_2"], recent);
        Assert.Contains(rows, r => r.IsItem && r.Text == "obj_0" && r.Depth == 3);
        Assert.Contains(rows, r => r is { Key: "tag:boss", Count: 1 });

        model.Quick = model.Quick with { Filter = new ExplorerFilter(ExplorerSort.NameDescending) };
        var objects = model.Rows.SkipWhile(r => r.Kind != ExplorerRowKind.Type || r.Key != "objects").Skip(1).TakeWhile(r => r.IsItem).Select(r => r.Text);
        Assert.Equal(["obj_2", "obj_1", "obj_0"], objects);
        model.Quick = model.Quick with { Filter = model.Quick.Filter.Toggle("objects") };
        Assert.DoesNotContain(model.Rows, r => r.Kind == ExplorerRowKind.Type && r.Key == "objects");
        model.Quick = model.Quick with { Filter = new ExplorerFilter(Tag: "boss") };
        Assert.Equal(["obj_2"], model.Rows.Where(r => r is { IsItem: true, Key: "objects", Depth: 2 }).Select(r => r.Text));
    }
}
