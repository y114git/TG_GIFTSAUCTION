using Avalonia.Controls;
using Avalonia.Threading;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class CopyWindowTests
{
    private static string Write(TempDir dir, string name, Func<GameMakerData> make)
    {
        using var data = make();
        return TestData.Write(dir.File(name), data);
    }

    private static T Named<T>(Window window, string name) where T : Control =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<T>().First(c => c.Name == name);

    [Fact]
    public async Task TheWindowCopiesIntoASecondFileThenSavesItOrDropsTheCopy()
    {
        using var dir = new TempDir();
        var source = Write(dir, "a.win", () => CopyResourcesTests.Source());
        var kept = Write(dir, "kept.win", () => CopyResourcesTests.Target());
        var dropped = Write(dir, "dropped.win", () => CopyResourcesTests.Target());
        var droppedBytes = await File.ReadAllBytesAsync(dropped, TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(source);
                window.Explorer.Select("objects:obj_a");
                var ct = TestContext.Current.CancellationToken;
                var target = kept;
                window.CopyTargetChooser = async _ => await window.Workspaces.OpenDetachedAsync(target, null, ct);
                window.CopyReviewer = async (s, t, items) => await ResourceCopier.PlanAsync(s, t, items, true, CollisionMode.Rename, null, ct);
                window.Confirmer = (_, _) => Task.FromResult(true);
                await window.CopyToOtherFileAsync();
                using (var saved = await GameMakerIO.ReadAsync(kept, cancellationToken: ct))
                {
                    Assert.Contains(saved.GameObjects, o => o.Name.Content == "obj_a");
                    Assert.Contains(saved.Sprites, s => s.Name.Content == "spr_a");
                }

                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("copied", StringComparison.OrdinalIgnoreCase));
                Assert.False(window.Workspaces.Current!.History.IsDirty); // the source is untouched

                target = dropped;
                window.Confirmer = (_, _) => Task.FromResult(false);
                await window.CopyToOtherFileAsync();
                Assert.Equal(droppedBytes, await File.ReadAllBytesAsync(dropped, ct));
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("dropped", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheCopyWithoutASelectionOrWithTheSameFileSaysSo()
    {
        using var dir = new TempDir();
        var source = Write(dir, "a.win", () => CopyResourcesTests.Source());
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(source);
                await window.CopyToOtherFileAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("Select the resources", StringComparison.Ordinal));
                window.Explorer.Select("objects:obj_a");
                window.FilePicker = (_, _) => Task.FromResult<string?>(source);
                await window.CopyToOtherFileAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("Duplicate", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ThePreviewListsTheItemsWithTheirChoices()
    {
        using var dir = new TempDir();
        var a = Write(dir, "a.win", () => CopyResourcesTests.Source());
        var b = Write(dir, "b.win", () => CopyResourcesTests.Target((d, h) => h.Execute(ResourceTemplates.New(d, "sprites", "spr_a"))));
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(a);
                var target = await window.Workspaces.OpenDetachedAsync(b, null, TestContext.Current.CancellationToken);
                using (target)
                {
                    var preview = new CopyToWindow(window.Workspaces.Current!, target, [("objects", "obj_a")]);
                    preview.Show();
                    var list = Named<ListBox>(preview, "CopyPlanList");
                    for (var i = 0; i < 200 && list.ItemCount == 0; i++)
                    {
                        await Task.Delay(25, TestContext.Current.CancellationToken);
                        Dispatcher.UIThread.RunJobs();
                    }

                    Assert.Equal(3, list.ItemCount); // the object, the script and the sprite the target already has
                    Assert.True(Named<Button>(preview, "CopyOk").IsEnabled);
                    Assert.Contains("Skipped: 1", Named<TextBlock>(preview, "CopyNotes").Text, StringComparison.Ordinal);
                    preview.Close();
                }
            }
            finally
            {
                window.Close();
            }
        });
    }
}
