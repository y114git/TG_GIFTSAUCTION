using Avalonia.Controls;
using G3MLib.DataFile;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ProjectWindowTests
{
    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 500 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "did not settle");
    }

    private static string StateText(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        return (string)window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ProjectButton").Content!;
    }

    private static void Click(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static MainWindow WindowWithRoot(TempDir dir)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        window.Workspaces.Projects.Registry.RememberRoot(Directory.CreateDirectory(dir.File("projects")).FullName);
        return window;
    }

    [Fact]
    public async Task TheOfferEnablesAProjectAndTheStateItemFollowsEditsSyncAndSave()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = WindowWithRoot(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.Equal("Enable project", StateText(window));
                var offer = window.Notifications.Active.Single(n => n.ActionLabel == "Enable project");
                offer.Action!();
                await Settle(() => window.Workspaces.Current!.Project is not null);
                await Settle(() => window.Notifications.History.Any(n => n.Text.StartsWith("Project created", StringComparison.Ordinal)));
                var workspace = window.Workspaces.Current!;
                Assert.Equal("Synced", StateText(window));

                workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
                Assert.Equal("1 changes not synced to data file", StateText(window));
                Assert.True(window.Registry.Execute("project.sync"));
                await Settle(() => StateText(window) == "Synced");
                Assert.Contains(window.Notifications.History, n => n.Text.StartsWith("Saved", StringComparison.Ordinal));

                // Save with the default autosync (On Save) writes the project and the data file.
                workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 3;"));
                Assert.True(window.Registry.Execute("file.save"));
                await Settle(() => StateText(window) == "Synced");
                Assert.False(workspace.IsDirty);

                // The state item itself syncs.
                workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 4;"));
                window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ProjectButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Settle(() => StateText(window) == "Synced");
                Assert.Equal(3, workspace.Project!.Snapshots.List().Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ThePlaceQuestionAsksOnTheFirstEnableWithARememberCheckBoxAndNotAgain()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.True(window.Registry.Execute("project.enable"));
                Dispatcher.UIThread.RunJobs();
                var dialog = window.OwnedWindows.Single();
                var folder = dialog.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "Folder");
                Assert.Equal(window.Workspaces.Projects.DefaultRoot, folder.Text);
                Assert.True(dialog.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked);
                folder.Text = dir.File("chosen");
                Click(dialog, "Ok");
                await Settle(() => window.Workspaces.Current!.Project is not null);
                Assert.StartsWith(dir.File("chosen"), window.Workspaces.Current!.Project!.Folder, StringComparison.Ordinal);
                Assert.Equal(dir.File("chosen"), window.Workspaces.Projects.Registry.RememberedRoot);

                // Cancel in the question leaves Direct mode.
                window.Workspaces.Projects.Disable(window.Workspaces.Current, deleteProject: true);
                window.Workspaces.Projects.Registry.RememberRoot(null);
                window.Registry.Execute("project.enable");
                Dispatcher.UIThread.RunJobs();
                Click(window.OwnedWindows.Single(), "Cancel");
                Dispatcher.UIThread.RunJobs();
                Assert.Null(window.Workspaces.Current.Project);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task SettingsDialogChangesAutosaveAndAutosyncSeparately()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = WindowWithRoot(dir);
            try
            {
                await window.OpenAsync(path);
                window.Registry.Execute("project.enable");
                await Settle(() => window.Workspaces.Current!.Project is not null);
                window.Registry.Execute("project.options");
                Dispatcher.UIThread.RunJobs();
                var dialog = window.OwnedWindows.Single();
                dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "AutoSync").SelectedIndex = (int)Trigger.OnFocusLoss;
                dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "AutoSave").SelectedIndex = (int)Trigger.OnSave;
                dialog.GetVisualDescendants().OfType<NumericUpDown>().Single(n => n.Name == "AutoSaveSeconds").Value = 9;
                Click(dialog, "Ok");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(new ProjectSettings(Trigger.OnSave, 9, Trigger.OnFocusLoss, 30), window.Workspaces.Current!.Project!.Settings);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheLocationDialogCopiesTheDataFileAndShowsAnErrorForAnExistingTarget()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = WindowWithRoot(dir);
            try
            {
                await window.OpenAsync(path);
                window.Registry.Execute("project.enable");
                await Settle(() => window.Workspaces.Current!.Project is not null);
                window.Registry.Execute("project.moveDataFile");
                Dispatcher.UIThread.RunJobs();
                var dialog = window.OwnedWindows.Single();
                var box = dialog.GetVisualDescendants().OfType<TextBox>().Single();
                box.Text = path;
                Click(dialog, "Copy");
                await Settle(() => dialog.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "Error").Text!.Length > 0);

                var target = dir.File(Path.Combine("moved", "game.win"));
                box.Text = target;
                Click(dialog, "Copy");
                await Settle(() => window.Workspaces.Current!.Path == Path.GetFullPath(target));
                Assert.True(File.Exists(path));
                Assert.Contains(window.Workspaces.Recent.Entries, e => e.Path == Path.GetFullPath(target));
                Assert.DoesNotContain(window.Workspaces.Recent.Entries, e => e.Path == Path.GetFullPath(path));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task DisableAsksKeepOrDeleteAndLeavesTheDataFileAlone()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () =>
        {
            var window = WindowWithRoot(dir);
            try
            {
                await window.OpenAsync(path);
                window.Registry.Execute("project.enable");
                await Settle(() => window.Workspaces.Current!.Project is not null);
                var folder = window.Workspaces.Current!.Project!.Folder;
                window.Registry.Execute("project.disable");
                Dispatcher.UIThread.RunJobs();
                Click(window.OwnedWindows.Single(), "Delete");
                await Settle(() => window.Workspaces.Current!.Project is null);
                Assert.False(Directory.Exists(folder));
                Assert.True(File.Exists(path));
                Assert.Equal("Enable project", StateText(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task AChangedDataFileAsksAndBringInChangesMergesThenReloadsTheFile()
    {
        using var dir = new TempDir();
        var (session, path, _) = await ProjectMergeTests.Diverged(dir);
        session.Service.Close();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.Equal("Data file changed outside", StateText(window));
                await Settle(() => window.OwnedWindows.Count == 1);
                var first = window.Workspaces.Current;
                Click(window.OwnedWindows.Single(), "BringIn");

                // Code changed on both sides: the conflict list opens with the project's version chosen.
                await Settle(() => window.OwnedWindows.Count == 1 && window.OwnedWindows[0].GetVisualDescendants().OfType<ComboBox>().Any(c => c.Name == "Side"));
                var dialog = window.OwnedWindows.Single();
                var side = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "Side");
                Assert.Equal("code/gml_Script_probe", side.Tag);
                side.SelectedIndex = 1;
                Click(dialog, "Ok");
                await Settle(() => window.Workspaces.Current is { } now && !ReferenceEquals(now, first));

                var merged = window.Workspaces.Current!;
                Assert.False(merged.Project!.DataFileChanged);
                Assert.Contains("return 99", CodeText.Decompile(merged.Data, "gml_Script_probe"), StringComparison.Ordinal);
                Assert.NotNull(merged.Data.GameObjects.ByName("obj_b"));
                Assert.Equal("Synced", StateText(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task KeepBothArchivesTheProjectAndOpensTheFileInDirectMode()
    {
        using var dir = new TempDir();
        var (session, path, _) = await ProjectMergeTests.Diverged(dir);
        session.Service.Close();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                await Settle(() => window.OwnedWindows.Count == 1);
                Click(window.OwnedWindows.Single(), "KeepBoth");
                await Settle(() => window.Workspaces.Current!.Project is null);
                Assert.Contains(window.Workspaces.Projects.Registry.Projects, p => p.Archived);
                Assert.Equal("Enable project", StateText(window));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
