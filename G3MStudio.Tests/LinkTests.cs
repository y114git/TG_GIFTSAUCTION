using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>Every reference field can be followed and set: no link is a disabled button, the picker lists the targets, an object's sprite, parent and mask are set in its own editor.</summary>
public sealed class LinkTests
{
    private static async Task<Workspace> Open(string path, TempDir dir)
    {
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EveryLinkRowOfEveryResourceNamesAnEntryThatCanBeOpened()
    {
        var checkedRows = 0;
        foreach (var file in Corpus.Files(Corpus.Folder))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            foreach (var type in ResourceTypes.All)
            {
                foreach (var item in (type.Items(data) ?? []).Take(40))
                {
                    foreach (var row in PropertyModel.Describe(item).Where(r => r.Kind == PropertyKind.Info && r.TargetType is not null))
                    {
                        checkedRows++;
                        if (row.Value.Length == 0)
                        {
                            Assert.Null(row.Target);
                            continue;
                        }

                        Assert.True(row.Target is not null, $"{type.Key}:{ResourceTypes.NameOf(item)} {row.Path} has no way to go to '{row.Value}'");
                        Assert.NotNull(ResourceTypes.Find(data, row.Target));
                        Assert.True(ResourceTypes.All.Any(t => t.Key == row.TargetType), row.TargetType);
                    }
                }
            }
        }

        Assert.True(checkedRows > 50, $"{checkedRows}");
    }

    [Fact]
    public async Task NoGridOfAnyResourceHasADeadLinkButton()
    {
        var file = Corpus.Files(Corpus.Folder).First();
        using var dir = new TempDir();
        using var workspace = await Open(file, dir);
        var rows = 0;
        await HeadlessApp.Run(() =>
        {
            foreach (var type in ResourceTypes.All.Where(t => t.Key != "general"))
            {
                foreach (var item in (type.Items(workspace.Data) ?? []).Take(4))
                {
                    var tab = new PropertyGridTab(workspace, type.Key, ResourceTypes.NameOf(item));
                    var window = new Window { Content = tab, Width = 900, Height = 600 };
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var buttons = tab.GetVisualDescendants().OfType<Button>().ToList();
                    foreach (var button in buttons.Where(b => b.Name?.StartsWith("Open_", StringComparison.Ordinal) == true))
                    {
                        rows++;
                        Assert.True(button.IsEnabled, $"{type.Key}:{ResourceTypes.NameOf(item)} {button.Name} is disabled");
                    }

                    foreach (var row in PropertyModel.Describe(item).Where(r => r.Kind == PropertyKind.Info && r.Reference && r.TargetType is not null && r.Depth <= 1))
                    {
                        // A row further down may not be built yet; the rows that are built must have a Set button that works.
                        if (buttons.Any(b => b.Name == "Open_" + row.Path || b.Name == "Set_" + row.Path)) Assert.Contains(buttons, b => b.Name == "Set_" + row.Path && b.IsEnabled);
                    }

                    window.Close();
                }
            }
        });
        Assert.True(rows > 5, $"{rows}");
    }

    [Fact]
    public async Task AnObjectsSpriteAndParentAreSetByTheGridAndAParentLoopIsRefused()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        using var workspace = await Open(path, dir);
        var (a, b) = (workspace.Data.GameObjects[0], workspace.Data.GameObjects[1]);
        await HeadlessApp.Run(() =>
        {
            var tab = new PropertyGridTab(workspace, "objects", "obj_0");
            var window = new Window { Content = tab, Width = 900, Height = 600 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var set = tab.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "Set_ParentId");
            Assert.True(set.IsEnabled);
            Assert.True(tab.Commit("ParentId", "objects:obj_1"));
            Assert.Same(b, a.ParentId);
            Assert.False(tab.Commit("ParentId", "objects:obj_0")); // itself
            var other = new PropertyGridTab(workspace, "objects", "obj_1");
            Assert.False(other.Commit("ParentId", "objects:obj_0")); // a loop
            Assert.Null(b.ParentId);
            Assert.True(tab.Commit("ParentId", ""));
            Assert.Null(a.ParentId);
            window.Close();
        });
    }

    [Fact]
    public async Task ThePickerListsTheEntriesOfTheTypeSearchesThemAndReturnsTheKey()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 12);
        using var workspace = await Open(path, dir);
        await HeadlessApp.Run(() =>
        {
            var picker = new ResourcePicker(workspace, "objects");
            var window = new Window { Content = picker, Width = 400, Height = 400 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(12, picker.Shown.Count);
            picker.Search = "obj_1";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["obj_1", "obj_10", "obj_11"], picker.Shown);
            string? key = null;
            picker.Chosen += k => key = k;
            picker.Choose("obj_10");
            Assert.Equal("objects:obj_10", key);
            window.Close();
        });
    }

    [Fact]
    public async Task ThePickerShowsTheCurrentEntryAndPicksOnlyOnARow()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 60);
        using var workspace = await Open(path, dir);
        await HeadlessApp.Run(() =>
        {
            var picker = new ResourcePicker(workspace, "objects", "objects:obj_59");
            var window = new Window { Content = picker, Width = 400, Height = 240 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var list = picker.GetVisualDescendants().OfType<ListBox>().Single();
            Assert.NotNull(list.ContainerFromIndex(list.SelectedIndex)); // the current entry is in view

            string? key = null;
            picker.Chosen += k => key = k;
            picker.Search = "obj_59";
            Dispatcher.UIThread.RunJobs();
            list.SelectedIndex = 0;
            // A click in the empty space under the rows picks nothing.
            var below = list.TranslatePoint(new Point(20, list.Bounds.Height - 4), window)!.Value;
            window.MouseDown(below, MouseButton.Left);
            window.MouseUp(below, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Null(key);
            var row = list.ContainerFromIndex(0)!;
            var on = row.TranslatePoint(new Point(20, row.Bounds.Height / 2), window)!.Value;
            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("objects:obj_59", key);
            window.Close();
        });
    }

    [Fact]
    public async Task AnObjectWithoutASpriteGetsOneFromItsOwnEditorAndShowsItsPicture()
    {
        var file = Corpus.Files(Corpus.Folder).First();
        using var dir = new TempDir();
        using var workspace = await Open(file, dir);
        var sprite = workspace.Data.Sprites.First(s => s.Textures.Count > 0);
        var obj = workspace.Data.GameObjects.First(o => o.Sprite is null && o.ParentId is null);
        await HeadlessApp.Run(() =>
        {
            var view = new ObjectLinksView(obj, workspace, _ => { }, _ => { });
            var window = new Window { Content = view, Width = 700, Height = 300 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.NotNull(view.GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(t => t.Name == "ObjectPreviewEmpty"));
            var set = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ObjectSet_Sprite");
            Assert.True(set.IsEnabled);
            Assert.Null(view.GetVisualDescendants().OfType<Button>().SingleOrDefault(b => b.Name == "ObjectClear_Sprite"));
            Assert.False(view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ObjectEditImage").IsEnabled);

            workspace.Edit(new SetPropertyCommand("objects", obj.Name.Content!, "Sprite", "sprites:" + sprite.Name.Content));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Same(sprite, obj.Sprite);
            Assert.NotNull(view.GetVisualDescendants().OfType<Button>().SingleOrDefault(b => b.Name == "ObjectOpen_Sprite"));
            Assert.NotNull(view.GetVisualDescendants().OfType<Button>().SingleOrDefault(b => b.Name == "ObjectClear_Sprite"));
            Assert.NotNull(view.GetVisualDescendants().OfType<Image>().SingleOrDefault(i => i.Name == "ObjectPreviewImage"));
            Assert.NotNull(view.GetVisualDescendants().OfType<GridSplitter>().SingleOrDefault(s => s.Name == "ObjectPreviewSplitter"));

            // Renamed with the tab open: the buttons act on the new name.
            workspace.Edit(new RenameResourceCommand("objects", obj.Name.Content!, "obj_links_renamed"));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ObjectClear_Sprite").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Null(obj.Sprite);
            workspace.Undo();
            Assert.Same(sprite, obj.Sprite);
            window.Close();
        });
    }

    [Fact]
    public async Task ALinkListTakesANewEntryFromTheChosenTargetAndRemovesIt()
    {
        var file = Corpus.Files(Corpus.Folder).First();
        using var dir = new TempDir();
        using var workspace = await Open(file, dir);
        var group = workspace.Data.TextureGroupInfo.First(g => g.Sprites.Count > 0);
        var groupName = ResourceTypes.NameOf(group);
        var sprite = workspace.Data.Sprites.First(s => !group.Sprites.Any(e => ReferenceEquals(e.Resource, s)));
        var before = group.Sprites.Count;
        var edit = new ListEntryCommand("textureGroups", groupName, "Sprites", ListEntryCommand.Add, -1, "sprites:" + sprite.Name.Content);
        workspace.Edit(edit);
        Assert.Equal(before + 1, group.Sprites.Count);
        Assert.Same(sprite, group.Sprites[^1].Resource);
        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, workspace.Data, cancellationToken: TestContext.Current.CancellationToken); // the file still writes with the new link
        workspace.Edit(new ListEntryCommand("textureGroups", groupName, "Sprites", ListEntryCommand.Remove, before));
        Assert.Equal(before, group.Sprites.Count);
        workspace.Undo();
        Assert.Equal(before + 1, group.Sprites.Count);
        Assert.Throws<InvalidOperationException>(() => workspace.Edit(new ListEntryCommand("textureGroups", groupName, "Sprites", ListEntryCommand.Add, -1, "objects:" + workspace.Data.GameObjects[0].Name.Content)));
    }

    [Fact]
    public async Task TheCheckBoxOfAnInstanceRowShowsOrHidesItAndIsNotTheSelection()
    {
        var file = Corpus.Files(Corpus.Folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.Layers is not null && r.Layers.Any(l => l.InstancesData?.Instances.Count > 1));
        await HeadlessApp.Run(() =>
        {
            var list = new RoomElementList(room);
            var window = new Window { Content = list, Width = 500, Height = 400 };
            window.Show();
            list.Show(null);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var items = list.GetVisualDescendants().OfType<ListBoxItem>().Take(2).ToList();
            var boxes = items.Select(i => i.GetVisualDescendants().OfType<CheckBox>().Single()).ToList();
            Assert.All(boxes, b => Assert.True(b.IsChecked)); // shown by default
            Assert.All(boxes, b => Assert.True(b.IsHitTestVisible));

            items[0].IsSelected = true; // selecting a row does not change its box
            Assert.True(boxes[0].IsChecked);
            boxes[1].IsChecked = false; // the box hides the element and does not select the row
            Assert.False(items[1].IsSelected);
            Assert.Single(list.Hidden);
            window.Close();
        });
    }
}
