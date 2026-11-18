#pragma warning disable CA2000 // windows and bitmaps made for a lookup only
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Rooms;

namespace G3MStudio.Tests;

public sealed class HoverCardTests
{
    private static readonly string[] ShownKeys = ["sprites:spr_a", "objects:obj_parent", "rooms:rm_a", "scripts:scr_a"];

    [Fact]
    public void TheCardContentHasTheIdTheTypeAndTheNicknameBesideTheRealName()
    {
        using var ws = ThumbnailTests.Generate();
        var sprite = ResourceHover.Describe(ws.Data, ws.Nicknames, "sprites:spr_a")!;
        Assert.Equal(("sprites", "spr_a", null, 0, true), (sprite.Type, sprite.Name, sprite.Nickname, sprite.Id, sprite.HasPicture));
        // The ID is the position in its own list, the index GML uses.
        Assert.Equal(2, ResourceHover.Describe(ws.Data, ws.Nicknames, "objects:obj_bare")!.Id);
        Assert.Equal(1, ResourceHover.Describe(ws.Data, ws.Nicknames, "objects:obj_child")!.Id);
        var script = ResourceHover.Describe(ws.Data, ws.Nicknames, "scripts:scr_a")!;
        Assert.False(script.HasPicture);

        ws.Nicknames.Set("objects:obj_bare", "Bare one");
        var named = ResourceHover.Describe(ws.Data, ws.Nicknames, "objects:obj_bare")!;
        Assert.Equal(("Bare one", "obj_bare", 2), (named.Title, named.Name, named.Id));
        // The real name stays the key; a key that names nothing has no card.
        Assert.Equal("objects:obj_bare", named.Key);
        Assert.Null(ResourceHover.Describe(ws.Data, ws.Nicknames, "objects:missing"));
        Assert.Null(ResourceHover.Describe(ws.Data, ws.Nicknames, "nothing"));
    }

    [Fact]
    public void TheCardOfADataEntryHasTheSameIdAsTheStatusBar()
    {
        using var ws = ThumbnailTests.Generate();
        var name = ws.Data.Strings.Select(ResourceTypes.NameOf).Last()!;
        var key = ResourceTypes.KeyOf("strings", name);
        var info = ResourceHover.Describe(ws.Data, ws.Nicknames, key)!;
        Assert.Equal(ResourceTypes.IdOf(ws.Data, key), info.Id);
        Assert.Equal(ws.Data.Strings.Count - 1, info.Id);
        Assert.Contains("ID " + info.Id, HoverCards.MetaOf(info), StringComparison.Ordinal);
    }

    [Fact]
    public void TheSettingIsOnByDefaultAndAFileFromBeforeItGetsTheDefault()
    {
        Assert.True(new AppSettings().HoverPreviews);
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        File.WriteAllText(userData.Combine("settings.json"), "{ \"codeFontSize\": 14 }");
        using var old = new SettingsStore(userData);
        Assert.True(old.Current.HoverPreviews);
        old.Update(x => x with { HoverPreviews = false });
        using var again = new SettingsStore(userData);
        Assert.False(again.Current.HoverPreviews);
    }

    [Theory]
    [InlineData("sprites", "spr_a")]
    [InlineData("objects", "obj_parent")]
    [InlineData("rooms", "rm_a")]
    [InlineData("fonts", "fnt_a")]
    public async Task ASpriteObjectRoomOrFontCardShowsItsPictureIdAndType(string type, string name)
    {
        using var ws = ThumbnailTests.Generate();
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            using var textures = new TextureBitmaps(ws.Lock);
            using var service = ThumbnailTests.Shared(ws, textures);
            HoverCards.Describe = key => ResourceHover.Describe(ws.Data, ws.Nicknames, key);
            HoverCards.Picture = (key, cancel) => service.GetAsync(key, cancel);
            var key = ResourceTypes.KeyOf(type, name);
            var card = new ResourceHoverCard(new HoverTip(key, name));
            var window = await Host(card);
            try
            {
                await Settle(() => Named<Image>(card, "RowThumbnail").Source is not null);
                Assert.Equal(name, Named<TextBlock>(card, "HoverTitle").Text);
                Assert.False(Named<TextBlock>(card, "HoverName").IsVisible);
                var meta = Named<TextBlock>(card, "HoverMeta").Text!;
                Assert.Contains("ID " + HoverCards.Describe(key)!.Id, meta, StringComparison.Ordinal);
                Assert.Contains(HoverCards.TypeName(type), meta, StringComparison.Ordinal);
                Assert.Empty(LayoutAudit.Overflows(window));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ANicknameIsTheTitleAndTheRealNameStandsUnderIt()
    {
        using var ws = ThumbnailTests.Generate();
        ws.Nicknames.Set("sprites:spr_a", "Hero");
        await HeadlessApp.Run(async () =>
        {
            HoverCards.Describe = key => ResourceHover.Describe(ws.Data, ws.Nicknames, key);
            var card = new ResourceHoverCard(new HoverTip("sprites:spr_a", "Hero (spr_a)"));
            var window = await Host(card);
            try
            {
                Assert.Equal("Hero", Named<TextBlock>(card, "HoverTitle").Text);
                Assert.Equal("spr_a", Named<TextBlock>(card, "HoverName").Text);
                Assert.True(Named<TextBlock>(card, "HoverName").IsVisible);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheCardIsCompleteBeforeItIsInATreeSoAPopupMeasuredEarlyKeepsItsSize()
    {
        using var ws = ThumbnailTests.Generate();
        await HeadlessApp.Run(async () =>
        {
            HoverCards.Describe = key => ResourceHover.Describe(ws.Data, ws.Nicknames, key);
            HoverCards.Picture = (_, _) => new TaskCompletionSource<IImage?>().Task; // the picture is still on its way
            var card = new ResourceHoverCard(new HoverTip("rooms:rm_a", "rm_a"));
            card.Measure(Avalonia.Size.Infinity);
            var early = card.DesiredSize;
            Assert.True(Named<TextBlock>(card, "HoverMeta").IsVisible);
            Assert.True(early.Height >= ResourceHoverCard.MaxSide, "the picture area is reserved before the card is shown");
            var window = await Host(card);
            try
            {
                card.Measure(Avalonia.Size.Infinity);
                // the fonts differ a little before the theme applies
                Assert.InRange(card.DesiredSize.Width, early.Width - 4, early.Width + 4);
                Assert.InRange(card.DesiredSize.Height, early.Height - 4, early.Height + 4);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task WithTheSettingOffTheCardIsPlainTextAndNothingIsRendered()
    {
        using var ws = ThumbnailTests.Generate();
        var asked = 0;
        await HeadlessApp.Run(async () =>
        {
            HoverCards.Enabled = false;
            HoverCards.Describe = key => ResourceHover.Describe(ws.Data, ws.Nicknames, key);
            HoverCards.Picture = (_, _) =>
            {
                asked++;
                return Task.FromResult<IImage?>(null);
            };
            var card = new ResourceHoverCard(new HoverTip("sprites:spr_a", "spr_a"));
            var window = await Host(card);
            try
            {
                Assert.Equal("spr_a", Named<TextBlock>(card, "HoverTitle").Text);
                Assert.False(Named<TextBlock>(card, "HoverMeta").IsVisible);
                Assert.False(Named<Image>(card, "RowThumbnail").IsVisible);
                Assert.False(Named<Image>(card, "HoverIcon").IsVisible);
                Assert.Equal(0, asked);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ACardAttachedAgainRetriesAPictureThatFailedToLoadBefore()
    {
        using var ws = ThumbnailTests.Generate();
        var asked = 0;
        await HeadlessApp.Run(async () =>
        {
            HoverCards.Describe = key => ResourceHover.Describe(ws.Data, ws.Nicknames, key);
            HoverCards.Picture = (_, _) =>
            {
                asked++;
                return Task.FromResult<IImage?>(asked == 1 ? null : new Avalonia.Media.Imaging.WriteableBitmap(new Avalonia.PixelSize(4, 4), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul));
            };
            var card = new ResourceHoverCard(new HoverTip("rooms:rm_a", "rm_a"));
            var window = await Host(card);
            try
            {
                await Settle(() => asked == 1 && !Named<Image>(card, "RowThumbnail").IsVisible);
                var holder = (Border)card.Parent!;
                holder.Child = null; // the tip closes and opens again with the same card
                await Settle(() => true);
                holder.Child = card;
                await Settle(() => asked == 2 && Named<Image>(card, "RowThumbnail").Source is not null);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ClosingTheCardStopsTheWaitForItsPicture()
    {
        using var ws = ThumbnailTests.Generate();
        CancellationToken seen = default;
        await HeadlessApp.Run(async () =>
        {
            HoverCards.Describe = key => ResourceHover.Describe(ws.Data, ws.Nicknames, key);
            HoverCards.Picture = async (_, cancel) =>
            {
                seen = cancel;
                await Task.Delay(Timeout.Infinite, cancel);
                return null;
            };
            var card = new ResourceHoverCard(new HoverTip("rooms:rm_a", "rm_a"));
            var window = await Host(card);
            try
            {
                await Settle(() => seen.CanBeCanceled);
                Assert.False(seen.IsCancellationRequested);
                window.Content = null; // the tip closes: the card leaves the tree
                await Settle(() => seen.IsCancellationRequested);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheSettingsPageHasTheOptionAndItSwitchesTheCardsOff()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Show();
                window.ShowSettings(0);
                await LayoutAudit.Settle();
                var box = window.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "HoverPreviews");
                Assert.Equal(Loc.Current["settings.hoverPreviews"], box.Content);
                Assert.True(box.IsChecked);
                Assert.True(HoverCards.Enabled);
                box.IsChecked = false;
                await LayoutAudit.Settle();
                Assert.False(window.Workspaces.Settings.Current.HoverPreviews);
                Assert.False(HoverCards.Enabled);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ABigExplorerListHoldsOnlyASmallTipPerRowUntilOneOpens()
    {
        // 50 000 rows: only the visible ones are made, each holds one record, and no card or picture exists before a tip opens.
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            using var data = ExplorerModelTests.Generate(Perf.Count(50_000));
            var view = new ExplorerView();
            var window = new Window { Width = 400, Height = 700, Content = view };
            window.Show();
            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                view.Load("big.win", data);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var items = view.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.DataContext is ExplorerRow { IsItem: true }).ToList();
                Assert.NotEmpty(items);
                Assert.All(items, i => Assert.IsType<HoverTip>(ToolTip.GetTip(i)));
                Assert.True(clock.ElapsedMilliseconds < 5000, "the tips must not slow the tree");
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RoomElementRowsAndUsageRowsCarryTheKeyOfTheirResource()
    {
        await HeadlessApp.Run(async () =>
        {
            using var obj = new G3MLib.DataFile.Models.GameMakerGameObject { Name = new G3MLib.DataFile.Models.GameMakerString("obj_a") };
            var layer = new G3MLib.DataFile.Models.GameMakerRoom.Layer { LayerName = new G3MLib.DataFile.Models.GameMakerString("l"), LayerType = G3MLib.DataFile.Models.GameMakerRoom.LayerType.Instances, Data = new G3MLib.DataFile.Models.GameMakerRoom.Layer.LayerInstancesData { Instances = [] } };
            layer.InstancesData.Instances.Add(new G3MLib.DataFile.Models.GameMakerRoom.GameObject { InstanceID = 100001, ObjectDefinition = obj });
            var list = new G3MStudio.Editor.RoomElementList(new G3MLib.DataFile.Models.GameMakerRoom { Layers = [layer] });
            var shown = new Window { Content = list, Width = 400, Height = 200 };
            shown.Show();
            list.Show(layer);
            await LayoutAudit.Settle();
            Assert.Equal("objects:obj_a", Assert.IsType<HoverTip>(ToolTip.GetTip(list.GetVisualDescendants().OfType<ListBoxItem>().Single())).Key);
            shown.Close();

            var usages = new G3MStudio.Panels.UsagesPanel();
            var window = new Window { Content = usages, Width = 400, Height = 300 };
            window.Show();
            try
            {
                await usages.ShowAsync("spr_a", Task.FromResult<IReadOnlyList<UsageHit>>([new UsageHit("objects", "obj_a", "sprite")]));
                await LayoutAudit.Settle();
                var tips = usages.GetVisualDescendants().OfType<TextBlock>().Select(t => ToolTip.GetTip(t)).OfType<HoverTip>().ToList();
                Assert.Equal("objects:obj_a", Assert.Single(tips).Key);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AnOpenTabAndTheMainWindowLookupShowTheCardOfTheirResource()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Show();
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                workspace.Nicknames.Set("objects:obj_1", "Hero");
                var info = HoverCards.Describe!("objects:obj_1")!;
                Assert.Equal(("Hero", "obj_1", 1), (info.Title, info.Name, info.Id));
                Assert.Equal("objects:obj_1", HoverCards.KeyOfName!("obj_1"));

                window.Docking.OpenTab("resource", "objects:obj_1", false);
                await LayoutAudit.Settle();
                var tab = window.GetVisualDescendants().OfType<Dock.Avalonia.Controls.DocumentTabStripItem>().Single();
                Assert.Equal("objects:obj_1", Assert.IsType<HoverTip>(ToolTip.GetTip(tab)).Key);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ATabKeepsTheCardOfItsResourceAfterARenameAndANewNickname()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Show();
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "objects:obj_1", false);
                await LayoutAudit.Settle();
                var tab = window.GetVisualDescendants().OfType<Dock.Avalonia.Controls.DocumentTabStripItem>().Single();
                Assert.Equal(("objects:obj_1", "obj_1"), Tip(tab));

                window.Workspaces.Current!.Nicknames.Set("objects:obj_1", "Hero");
                window.Docking.RetitleTabs();
                Assert.Equal(("objects:obj_1", "Hero"), Tip(tab));

                window.Docking.Rekey("objects:obj_1", "objects:obj_9");
                Assert.Equal("objects:obj_9", Tip(tab).Key);
            }
            finally { window.Close(); }
        });

        static (string? Key, string Text) Tip(Control tab) => Assert.IsType<HoverTip>(ToolTip.GetTip(tab)) is var tip ? (tip.Key, tip.Text) : default;
    }

    [Fact]
    public async Task TheCardsRenderForAVisualCheck()
    {
        using var ws = ThumbnailTests.Generate();
        ws.Nicknames.Set("objects:obj_parent", "Big Boss");
        Directory.CreateDirectory(Corpus.Shots);
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            using var textures = new TextureBitmaps(ws.Lock);
            using var service = ThumbnailTests.Shared(ws, textures);
            HoverCards.Describe = key => ResourceHover.Describe(ws.Data, ws.Nicknames, key);
            HoverCards.Picture = (key, cancel) => service.GetAsync(key, cancel);
            var cards = ShownKeys.Select(k => (Control)new Border { BorderThickness = new Avalonia.Thickness(1), BorderBrush = Brushes.Gray, Padding = new Avalonia.Thickness(8), Child = new ResourceHoverCard(new HoverTip(k, k, k.StartsWith("scripts", StringComparison.Ordinal) ? "Used in 3 places" : "")) }).ToArray();
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Margin = new Avalonia.Thickness(12), Background = Brushes.Black, Children = { cards[0], cards[1], cards[2], cards[3] } };
            var window = await LayoutAudit.Host(row, 760, 260);
            try
            {
                await Settle(() => cards.Take(3).All(c => Named<Image>(c, "RowThumbnail").Source is not null));
                using var target = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(760, 260));
                target.Render(window);
                target.Save(Path.Combine(Corpus.Shots, "hover-cards.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }
            finally { window.Close(); }
        });
    }

    private static async Task<Window> Host(Control card)
    {
        var window = await LayoutAudit.Host(new Border { Padding = new Avalonia.Thickness(8), Child = card }, 360, 260);
        return window;
    }

    private static T Named<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Concat(root is T self ? [self] : []).First(c => c.Name == name);

    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(done());
    }
}
