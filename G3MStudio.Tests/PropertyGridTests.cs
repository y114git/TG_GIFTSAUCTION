using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class PropertyGridTests
{
    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path);
    }

    [Fact]
    public async Task EditingAPropertyIsOneUndoableEditAndABadValueIsRefused()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        var obj = workspace.Data.GameObjects[0];
        var number = PropertyModel.Describe(obj).First(r => r.Kind == PropertyKind.Whole && !r.ReadOnly);
        await HeadlessApp.Run(() =>
        {
            var tab = new PropertyGridTab(workspace, "objects", "obj_0");
            var window = new Window { Content = tab, Width = 700, Height = 500 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var box = tab.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Prop_" + number.Path);

            Assert.True(tab.Commit(number.Path, "9"));
            Assert.Equal("9", PropertyModel.Read(obj, number.Path));
            Assert.Equal(["objects"], workspace.History.Items.Select(c => c.Editor));

            Assert.False(tab.Commit(number.Path, "nine"));
            Assert.Contains(number.Path, tab.Message, StringComparison.Ordinal);
            Assert.Equal("9", PropertyModel.Read(obj, number.Path));
            Assert.Single(workspace.History.Items);

            // Undo from anywhere shows in the open tab.
            workspace.Undo();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(PropertyModel.Read(obj, number.Path), tab.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Prop_" + number.Path).Text);
            window.Close();
        });
    }

    [Fact]
    public async Task PhysicsFieldsSitInAClosedSectionThatOpensOnAClickAndAFilterShowsThemInPlace()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(() =>
        {
            var tab = new PropertyGridTab(workspace, "objects", "obj_0");
            var window = new Window { Content = tab, Width = 700, Height = 500 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            bool Shown() => tab.GetVisualDescendants().OfType<CheckBox>().Any(c => c.Name == "Prop_UsesPhysics");
            Assert.False(Shown());
            tab.Toggle("§properties.section.physics");
            Assert.True(Shown());
            tab.Toggle("§properties.section.physics");
            tab.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PropertyFilter").Text = "UsesPhysics";
            Dispatcher.UIThread.RunJobs();
            Assert.True(Shown());
            window.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AReferenceIsALinkThatOpensTheEntryItPointsAt()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        workspace.Data.GameObjects[1].ParentId = workspace.Data.GameObjects[0];
        await HeadlessApp.Run(() =>
        {
            var tab = new PropertyGridTab(workspace, "objects", "obj_1");
            var window = new Window { Content = tab, Width = 700, Height = 500 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            string? opened = null;
            tab.OpenRequested += key => opened = key;
            var link = tab.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "Open_ParentId");
            Assert.Equal("obj_0", link.Content);
            link.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("objects:obj_0", opened);
            window.Close();
        });
    }

    [Fact]
    public async Task EveryResourceOpensInThePropertyGridFromTheExplorer()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var tab = window.Docking.OpenTab("resource", "objects:obj_1", false);
                Assert.NotNull(tab);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Contains(window.GetVisualDescendants().OfType<PropertyGridTab>(), _ => true);
            }
            finally { window.Close(); }
        });
    }
}
