using Avalonia.LogicalTree;
using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data of a test is disposed with its using
public sealed class FinishOrganizeTests
{
    private static string Key(string type, string name) => ResourceTypes.KeyOf(type, name);

    [Fact]
    public void UiNodesOfAllKindsTakeTypedPropertiesAndUndoRestoresThem()
    {
        using var data = CopyResourcesTests.Source();
        new UiLayerCommand(UiLayerCommand.AddLayer, "", "ui").Apply(data);
        new UiLayerCommand(UiLayerCommand.AddPanel, "ui", "box").Apply(data);
        new UiLayerCommand(UiLayerCommand.AddSprite, "ui/box", "pic", "spr_a").Apply(data);
        new UiLayerCommand(UiLayerCommand.AddObject, "ui/box", "", "obj_a").Apply(data);
        Assert.Throws<InvalidOperationException>(() => new UiLayerCommand(UiLayerCommand.AddSprite, "ui/box", "x", "missing").Apply(data));
        Assert.Throws<InvalidOperationException>(() => new UiLayerCommand(UiLayerCommand.AddObject, "ui/box", "", "obj_a").Apply(data)); // the object node is named by its object
        var box = (GameMakerUIFlexPanel)UiLayerCommand.NodeAt(data, "ui/box")!.Node;
        Assert.Equal(["pic", "obj_a"], box.Children.Select(c => UiLayerCommand.NameOf(c.Node)));

        var set = new UiLayerCommand(UiLayerCommand.SetProp, "ui/box", "FlexDirection", "", "Row");
        set.Apply(data);
        Assert.Equal(IGameMakerFlexProperties.FlexDirectionKind.Row, box.FlexDirection);
        new UiLayerCommand(UiLayerCommand.SetProp, "ui/box", "Width", "", "50%").Apply(data);
        Assert.Equal("50%", UiLayerProps.Get(box, "Width"));
        new UiLayerCommand(UiLayerCommand.SetProp, "ui/box/pic", "Anchor", "", "4").Apply(data);
        Assert.Throws<InvalidOperationException>(() => new UiLayerCommand(UiLayerCommand.SetProp, "ui/box", "GapRow", "", "wide").Apply(data));
        Assert.Throws<InvalidOperationException>(() => new UiLayerCommand(UiLayerCommand.SetProp, "ui/box", "Name", "", "x").Apply(data));
        set.Revert(data);
        Assert.Equal(IGameMakerFlexProperties.FlexDirectionKind.Column, box.FlexDirection);
        Assert.Equal(set.Args, UiLayerCommand.FromArgs(set.Args).Args);
    }

    [Fact]
    public void TheFlexPreviewLaysOutRowsGrowGapAndPadding()
    {
        using var data = TestData.Create(1);
        new UiLayerCommand(UiLayerCommand.AddLayer, "", "ui").Apply(data);
        new UiLayerCommand(UiLayerCommand.AddPanel, "ui", "a").Apply(data);
        new UiLayerCommand(UiLayerCommand.AddPanel, "ui", "b").Apply(data);
        foreach (var (path, prop, value) in new[] { ("ui", "FlexDirection", "Row"), ("ui", "GapColumn", "10"), ("ui", "PaddingLeft", "20"), ("ui/a", "Width", "100"), ("ui/b", "FlexGrow", "1") })
            new UiLayerCommand(UiLayerCommand.SetProp, path, prop, "", value).Apply(data);
        var boxes = UiFlexLayout.Compute(data.FORM.UILR!.List.Single(r => UiLayerCommand.NameOf(r.Node) == "ui"), 400, 200);
        var a = boxes.Single(b => b.Path == "ui/a");
        var b = boxes.Single(b => b.Path == "ui/b");
        Assert.Equal((20, 100), (a.X, a.Width));
        Assert.Equal(130, b.X); // after the padding, the first box and the gap
        Assert.Equal(400, b.X + b.Width); // the grow box takes the rest of the row
        Assert.Equal(200, a.Height); // the cross axis stretches
    }

    [Fact]
    public async Task TheUiLayerTabEditsTypedFieldsAndDrawsThePreview()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        workspace.Edit(new UiLayerCommand(UiLayerCommand.AddLayer, "", "ui"));
        var root = workspace.Data.FORM.UILR!.List.Single(r => UiLayerCommand.NameOf(r.Node) == "ui");
        await HeadlessApp.Run(() =>
        {
            var view = new UiLayerView(root, workspace);
            var window = new Window { Content = view, Width = 800, Height = 600 };
            window.Show();
            Assert.True(view.Add(UiLayerCommand.AddPanel));
            view.Select("ui/panel");
            Assert.NotNull(view.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault(c => c.Name == "UiProp_FlexDirection"));
            Assert.True(view.SetProperty("Width", "120"));
            Assert.Equal(120, view.Boxes.Single(b => b.Path == "ui/panel").Width);
            Assert.False(view.SetProperty("Width", "wide"));
            Assert.True(view.AddAsset(UiLayerCommand.AddObject, "obj_0"));
            workspace.History.Undo();
            workspace.History.Undo();
            Assert.Equal(UiLayerView.PreviewWidth, view.Boxes.Single(b => b.Path == "ui/panel").Width); // the width edit is gone, the panel stretches again
            window.Close();
        });
    }

    [Fact]
    public async Task AFieldEditedWithEnterAndThenLeftMakesOneUndoStep()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
        workspace.Edit(new UiLayerCommand(UiLayerCommand.AddLayer, "", "ui"));
        var root = workspace.Data.FORM.UILR!.List.Single(r => UiLayerCommand.NameOf(r.Node) == "ui");
        await HeadlessApp.Run(() =>
        {
            var view = new UiLayerView(root, workspace);
            var window = new Window { Content = view, Width = 800, Height = 600 };
            window.Show();
            Assert.True(view.Add(UiLayerCommand.AddPanel));
            view.Select("ui/panel");
            var box = view.GetVisualDescendants().OfType<TextBox>().First(b => b.Name == "UiProp_Width");
            var before = workspace.History.Position;
            box.Text = "120";
            box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
            box.RaiseEvent(new Avalonia.Input.FocusChangedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
            Assert.Equal(before + 1, workspace.History.Position);
            window.Close();
        });
    }

    [Fact]
    public async Task TheObjectTabHeaderHasASplitterInsteadOfACap()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(() =>
        {
            var tab = new PropertyGridTab(workspace, "objects", "obj_0", new StackPanel { Children = { new TextBlock { Text = "events" } } });
            Assert.True(tab.GetLogicalDescendants().OfType<GridSplitter>().Single().IsVisible);
        });
    }

    [Fact]
    public async Task AFolderOrCollectionIsExportedAsOnePackage()
    {
        using var dir = new TempDir();
        var path = TestData.Write(dir.File("a.win"), CopyResourcesTests.Source());
        var zip = dir.File("pack.zip");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Explorer.Model.Organizer = Organizer.Empty.AddToCollection("Hero", [Key("objects", "obj_a")]);
                window.FilePicker = (_, _) => Task.FromResult<string?>(zip);
                window.Explorer.Select(Key("objects", "obj_a"));
                await window.ExportPackageAsync();
                await using var archive = await ZipFile.OpenReadAsync(zip, TestContext.Current.CancellationToken);
                Assert.Contains(archive.Entries, e => e.FullName.Contains("obj_a", StringComparison.Ordinal));
                Assert.Contains(archive.Entries, e => e.FullName.Contains("gml_Object_obj_a_Create_0", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        });
        Assert.Empty(FolderPackage.Packable([Key("general", "game"), "bad"]));
    }

    [Fact]
    public async Task AnExportedPackageImportsIntoAnotherDataFile()
    {
        using var dir = new TempDir();
        var source = TestData.Write(dir.File("a.win"), CopyResourcesTests.Source());
        var target = TestData.WriteSample(dir, "b.win", 1, true);
        var zip = dir.File("pack.zip");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.FilePicker = (_, _) => Task.FromResult<string?>(zip);
                window.Confirmer = (_, _) => Task.FromResult(true);
                await window.OpenAsync(source);
                window.Explorer.Select(Key("objects", "obj_a"));
                await window.ExportPackageAsync();
                Assert.True(File.Exists(zip));

                await window.OpenAsync(target);
                var workspace = window.Workspaces.Current!;
                Assert.DoesNotContain(workspace.Data.GameObjects, o => o.Name.Content == "obj_a");
                await window.ImportPackageAsync();
                Assert.Contains(workspace.Data.GameObjects, o => o.Name.Content == "obj_a");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task AHostilePackageIsRefusedBeforeAnythingIsImported()
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, "b.win", 1, true);
        var ct = TestContext.Current.CancellationToken;
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(target, cancellationToken: ct);
        foreach (var entry in new[] { "Unknown/x/x.json", "GameObjects/" + new string('a', 201) + "/a.json", "../evil/x.json" })
        {
            var zip = dir.File("hostile" + Math.Abs(entry.GetHashCode(StringComparison.Ordinal)) + ".zip");
            await using (var archive = await ZipFile.OpenAsync(zip, ZipArchiveMode.Create, ct))
            await using (var content = await archive.CreateEntry(entry).OpenAsync(ct))
                await content.WriteAsync("{}"u8.ToArray(), ct);
            var scratch = Directory.CreateTempSubdirectory("g3mstudio-test-").FullName;
            try
            {
                Assert.True(Record.Exception(() => ResourceTransfer.ImportPackage(workspace, zip, scratch, null, ct)) is not null, entry);
            }
            finally
            {
                CopyPackage.TryDelete(scratch);
            }
        }

        Assert.Single(workspace.Data.GameObjects);
    }

    [Fact]
    public async Task APackageNameCompletedWithZipAsksBeforeReplacingAFile()
    {
        using var dir = new TempDir();
        var path = TestData.Write(dir.File("a.win"), CopyResourcesTests.Source());
        var existing = dir.File("pack.zip");
        await File.WriteAllTextAsync(existing, "keep", TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.FilePicker = (_, _) => Task.FromResult<string?>(dir.File("pack")); // no extension: the ".zip" is added here
                window.Confirmer = (_, _) => Task.FromResult(false);
                window.Explorer.Select(Key("objects", "obj_a"));
                await window.ExportPackageAsync();
            }
            finally
            {
                window.Close();
            }
        });
        Assert.Equal("keep", await File.ReadAllTextAsync(existing, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CopiedResourcesKeepFolderCollectionAndTagInTheTarget()
    {
        var (source, id) = Organizer.Empty.AddFolder("Heroes", null);
        (source, var sub) = source.AddFolder("Bosses", id);
        source = source.MoveTo([Key("objects", "a")], sub).AddToCollection("Act 1", [Key("objects", "a")]).ColourTag("boss", "#E5534B");
        var quick = QuickAccess.Empty.AddTag(Key("objects", "a"), "boss");
        Assert.True(OrganizerCopy.HasMembership(source, quick, [Key("objects", "a")]));
        Assert.False(OrganizerCopy.HasMembership(source, quick, [Key("objects", "z")]));

        var (existing, topId) = Organizer.Empty.AddFolder("heroes", null); // found by name, not made twice
        var (target, targetQuick) = OrganizerCopy.Merge(source, quick, existing, QuickAccess.Empty, new Dictionary<string, string> { [Key("objects", "a")] = Key("objects", "a_copy") });
        Assert.Equal(2, target.Folders.Count);
        var bosses = target.Folders.Single(f => f.Name == "Bosses");
        Assert.Equal(topId, bosses.Parent);
        Assert.Equal(bosses.Id, target.FolderOf[Key("objects", "a_copy")]);
        Assert.Equal([Key("objects", "a_copy")], target.Collection("Act 1")!.Keys);
        Assert.Equal(["boss"], targetQuick.TagsOf(Key("objects", "a_copy")));
        Assert.Equal("#E5534B", target.TagColours["boss"]);
    }

    [Fact]
    public async Task ACopyToAnotherFileKeepsMembershipWhenAskedAndTheChoiceCanBeRemembered()
    {
        using var dir = new TempDir();
        var source = TestData.Write(dir.File("a.win"), CopyResourcesTests.Source());
        var target = TestData.Write(dir.File("b.win"), CopyResourcesTests.Target());
        var userData = new UserData(dir.File("user"));
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var ct = TestContext.Current.CancellationToken;
                await window.OpenAsync(source);
                var (organizer, folder) = Organizer.Empty.AddFolder("Heroes", null);
                window.Explorer.Model.Organizer = organizer.MoveTo([Key("objects", "obj_a")], folder);
                window.Explorer.Select(Key("objects", "obj_a"));
                window.CopyTargetChooser = async _ => await window.Workspaces.OpenDetachedAsync(target, null, ct);
                window.CopyReviewer = async (s, t, items) => await ResourceCopier.PlanAsync(s, t, items, true, CollisionMode.Rename, null, ct);
                window.Confirmer = (_, _) => Task.FromResult(true);
                await window.CopyToOtherFileAsync();
                var kept = new OrganizerStore(userData).Load(target);
                Assert.Equal("Heroes", Assert.Single(kept.Folders).Name);
                Assert.Equal(kept.Folders[0].Id, kept.FolderOf[Key("objects", "obj_a")]);
                Assert.Equal(PromptChoice.Ask, Prompts.Get(window.Workspaces.Settings.Current, Prompts.CopyKeepOrganize));
                Assert.Contains(Prompts.All, p => p.Id == Prompts.CopyKeepOrganize);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SavedSearchesAndRecentOpenAsScopesWithCounts()
    {
        var model = new ExplorerModel(key => key);
        model.Load(TestData.Create(6));
        model.Quick = QuickAccess.Empty.AddSearch("obj:obj_1").AddSearch("obj:obj_").Touch(Key("objects", "obj_3")).Touch(Key("objects", "obj_2"));
        model.Toggle(ExplorerModel.QuickSearches);
        var searches = model.Rows.Where(r => r.Kind == ExplorerRowKind.Search).ToList();
        Assert.Equal([1, 6], searches.Select(r => r.Count));
        Assert.True(searches[0].HasCount);

        model.Scope = ExplorerModel.ScopeOf(searches[0]);
        Assert.Equal([Key("objects", "obj_1")], model.Rows.Where(r => r.IsItem).Select(r => ResourceTypes.KeyOf(r.Key, r.Text)));
        Assert.Equal("obj:obj_1", model.Breadcrumb[^1].Text);

        model.Scope = null;
        var recent = model.Rows.Single(r => r.Key == ExplorerModel.QuickRecent);
        model.Scope = ExplorerModel.ScopeOf(recent);
        Assert.Equal(["obj_2", "obj_3"], model.Rows.Where(r => r.IsItem).Select(r => r.Text)); // newest first
        Assert.Equal([Key("objects", "obj_2"), Key("objects", "obj_3")], model.KeysOf(recent));

        model.Quick = model.Quick.RemoveSearch("obj:obj_1");
        model.Scope = ExplorerModel.SearchPrefix + "obj:obj_1";
        Assert.Null(model.Scope); // a removed search is forgotten like a deleted folder
    }
}
