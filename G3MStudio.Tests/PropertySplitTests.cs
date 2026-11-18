using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Every resource tab with a top part and a property list has a splitter between them; the height the user gives the list is kept per editor type.</summary>
public sealed class PropertySplitTests
{
    private static readonly string[] Types = ["general", "sprites", "tileSets", "sounds", "paths", "scripts", "shaders", "fonts", "timelines", "objects", "rooms", "extensions", "sequences", "animationCurves", "particleSystems", "texturePageItems", "embeddedTextures", "uiLayers"];

    private sealed class Store(double? saved = null)
    {
        public Dictionary<string, double> Kept { get; } = [];

        public PaneMemory Memory => new(key => Kept.TryGetValue(key, out var size) ? size : saved, (key, size) => Kept[key] = size);
    }

    private static Control Header(string kind) => kind == "stack"
        ? new StackPanel { Children = { new Border { Height = 180 }, new TextBlock { Text = "events" } } }
        : new Border { Height = 160 };

    // Layout rounding moves a row by a pixel.
    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1.5, expected + 1.5);

    private static Grid SplitOf(Window window) => (Grid)window.GetVisualDescendants().OfType<GridSplitter>().Single(s => s.Name == "PropertySplitter").Parent!;

    private static void Drag(Window window, double bottomHeight)
    {
        var grid = SplitOf(window);
        grid.RowDefinitions[2].Height = new GridLength(bottomHeight); // what the splitter does while it is dragged
        window.UpdateLayout();
        window.GetVisualDescendants().OfType<GridSplitter>().Single(s => s.Name == "PropertySplitter").RaiseEvent(new Avalonia.Input.VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = default });
    }

    private static async Task<Workspace> Open(TempDir dir)
    {
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 2));
    }

    [Theory]
    [InlineData("stack")]
    [InlineData("plain")]
    public async Task TheSplitterKeepsTheHeightGivenToThePropertiesAcrossTabsAndResizes(string kind)
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var store = new Store();
        await HeadlessApp.Run(async () =>
        {
            var tab = new PropertyGridTab(workspace, "objects", "obj_0", Header(kind), memory: store.Memory);
            var window = await LayoutAudit.Host(tab, 900, 700);
            try
            {
                var grid = SplitOf(window);
                Assert.True(grid.Children.OfType<GridSplitter>().Single().IsVisible);
                Drag(window, 260);
                await LayoutAudit.Settle();
                Near(260, store.Kept["properties.objects"]);
                Near(260, grid.RowDefinitions[2].ActualHeight);

                // The list keeps its height when the window grows; the top part takes the rest.
                var top = grid.RowDefinitions[0].ActualHeight;
                window.Height = 900;
                await LayoutAudit.Settle();
                Near(260, grid.RowDefinitions[2].ActualHeight);
                Assert.True(grid.RowDefinitions[0].ActualHeight > top + 100);

                // Another tab of the same type opens with the same height, also after the app restarted (the store stands for the settings file).
                var again = new PropertyGridTab(workspace, "objects", "obj_1", Header(kind), memory: store.Memory);
                var second = await LayoutAudit.Host(again, 900, 700);
                Near(260, SplitOf(second).RowDefinitions[2].ActualHeight);
                second.Close();

                // Another type has its own height.
                var other = await LayoutAudit.Host(new PropertyGridTab(workspace, "sprites", "spr_0", Header(kind), memory: store.Memory), 900, 700);
                Assert.NotEqual(260, SplitOf(other).RowDefinitions[2].ActualHeight, 1);
                other.Close();

                // The property list scrolls inside its own part.
                Drag(window, 130);
                await LayoutAudit.Settle();
                var list = grid.Children.OfType<DockPanel>().Single().GetVisualDescendants().OfType<ScrollViewer>().First();
                Assert.NotEqual(ScrollBarVisibility.Disabled, list.VerticalScrollBarVisibility);
                Assert.True(list.Bounds.Height <= 131);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(5000.0, 700.0)]
    [InlineData(1.0, 700.0)]
    [InlineData(400.0, 420.0)]
    public async Task ASavedHeightIsHeldBetweenTheMinimumsOfBothParts(double saved, double height)
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(async () =>
        {
            var tab = new PropertyGridTab(workspace, "objects", "obj_0", Header("stack"), memory: new Store(saved).Memory);
            var window = await LayoutAudit.Host(tab, 420, height);
            try
            {
                var grid = SplitOf(window);
                var (top, bottom) = (grid.RowDefinitions[0], grid.RowDefinitions[2]);
                Assert.True(bottom.ActualHeight >= bottom.MinHeight - 1, $"list {bottom.ActualHeight}");
                Assert.True(top.ActualHeight >= top.MinHeight - 1, $"top {top.ActualHeight}");
                Assert.Empty(LayoutAudit.Overflows(window));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task EveryEditorWithATopPartHasTheSplitterAndTheHeightSurvivesARestart()
    {
        using var dir = new TempDir();
        var file = Path.Combine(Corpus.Fixtures, "uty", "data.win");
        var withoutSplitter = new List<string>();
        var withSplitter = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var data = window.Workspaces.Current!.Data;
                foreach (var type in Types)
                {
                    if (ResourceTypes.All.First(t => t.Key == type).Items(data)?.FirstOrDefault() is not { } item) continue;
                    var tab = window.Docking.OpenTab("resource", ResourceTypes.KeyOf(type, ResourceTypes.NameOf(item)), false);
                    await LayoutAudit.Settle();
                    var grid = window.GetVisualDescendants().OfType<PropertyGridTab>().FirstOrDefault(t => t.IsEffectivelyVisible && t.GetVisualDescendants().OfType<TextBox>().Any(b => b.Name == "PropertyFilter"));
                    Assert.NotNull(tab);
                    Assert.NotNull(grid);
                    (grid.GetVisualDescendants().OfType<GridSplitter>().Any(s => s.Name == "PropertySplitter") ? withSplitter : withoutSplitter).Add(type);
                }

                // A drag in the open window is written to the settings file and read by the next start.
                window.Docking.OpenTab("resource", ResourceTypes.KeyOf("objects", ResourceTypes.NameOf(data.GameObjects[0])), false);
                await LayoutAudit.Settle();
                Drag(window, 233);
                Near(233, window.Workspaces.Settings.Current.PaneSizes["properties.objects"]);
            }
            finally { window.Close(); }

            var next = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                Near(233, next.Workspaces.Settings.Current.PaneSizes["properties.objects"]);
                await next.OpenAsync(file);
                next.Docking.OpenTab("resource", ResourceTypes.KeyOf("objects", ResourceTypes.NameOf(next.Workspaces.Current!.Data.GameObjects[0])), false);
                await LayoutAudit.Settle();
                Near(233, SplitOf(next).RowDefinitions[2].ActualHeight);
            }
            finally { next.Close(); }
        });
        Assert.True(withSplitter.Count > 0);
        Assert.True(withoutSplitter.All(type => type == "scripts"), "a script has no top part; every other type has the splitter: " + string.Join(", ", withoutSplitter) + " (with: " + string.Join(", ", withSplitter) + ")");
    }
}
