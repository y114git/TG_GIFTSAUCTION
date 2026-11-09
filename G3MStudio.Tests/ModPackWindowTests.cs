using Avalonia.Controls;
using G3MLib.DataFile;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Workspace;
using G3MStudio.Mods;

namespace G3MStudio.Tests;

public sealed class ModPackWindowTests
{
    private static string Write(TempDir dir, string name, Func<GameMakerData> make)
    {
        using var data = make();
        return TestData.Write(dir.File(name), data);
    }

    private static GameMakerData Base() => CopyResourcesTests.Source();

    private static GameMakerData Modded()
    {
        var data = Base();
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "sprites", "spr_new"));
        history.Execute(new EditCodeCommand("gml_Script_scr_use", "return 9;"));
        return data;
    }

    [Fact]
    public async Task APackIsExportedFromOneFileAndAppliedToAnotherInOneUndoStep()
    {
        using var dir = new TempDir();
        var original = Write(dir, "orig.win", Base);
        var modded = Write(dir, "mod.win", Modded);
        var target = Write(dir, "target.win", Base);
        var pack = dir.File("m.g3mpack");
        var ct = TestContext.Current.CancellationToken;
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                // The remembered base would answer the export itself (and its background save races the test): this test is about picking the original.
                window.Workspaces.Settings.Update(x => x with { ModBaseEnabled = false });
                await window.OpenAsync(modded);
                var picks = new Queue<string>([original, pack]);
                window.FilePicker = (_, _) => Task.FromResult<string?>(picks.Dequeue());
                window.PackInfoAsker = (_, name) => Task.FromResult<PackInfo?>(new PackInfo(name, "2.0.0", ["me"]));
                await window.ExportModPackAsync();
                Assert.Equal("2.0.0", ModPackFile.Read(pack).Manifest.Version);
                Assert.Equal(2, ModPackFile.Read(pack).Entries.Count);

                await window.OpenAsync(target);
                ReapplyPlan? seen = null;
                window.PackReviewer = plan =>
                {
                    seen = plan;
                    return Task.FromResult<IReadOnlyDictionary<string, Resolution>?>(new Dictionary<string, Resolution>());
                };
                await window.ApplyModPackAsync(pack);
                Assert.Equal(2, seen?.Count(PackState.Apply));
                var workspace = window.Workspaces.Current!;
                Assert.Contains(workspace.Data.Sprites, s => s.Name.Content == "spr_new");
                Assert.Equal("return 9;", CodeText.Decompile(workspace.Data, "gml_Script_scr_use").Trim());
                workspace.Undo();
                Assert.DoesNotContain(workspace.Data.Sprites, s => s.Name.Content == "spr_new");
                Assert.Equal("return 7;", CodeText.Decompile(workspace.Data, "gml_Script_scr_use").Trim());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheDiffWindowListsTheDifferencesAndAppliesOneToTheOpenFile()
    {
        using var dir = new TempDir();
        var mine = Write(dir, "mine.win", Base);
        var other = Write(dir, "other.win", Modded);
        var ct = TestContext.Current.CancellationToken;
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(mine);
                using var right = await window.Workspaces.OpenDetachedAsync(other, null, ct);
                var left = window.Workspaces.Current!;
                var diff = await window.ComputeDiffAsync(left, right);
                Assert.Equal(2, diff?.Items.Count);
                var view = new DiffWindow(window, left, right, diff!, new Core.Settings.DiffOptions());
                view.Show();
                try
                {
                    var summary = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<TextBlock>().First(t => t.Name == "DiffSummary");
                    Assert.Contains("1 added", summary.Text, StringComparison.Ordinal);
                    var tree = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<TreeView>().First(t => t.Name == "DiffTree");
                    Assert.Equal(2, tree.Items.Count);
                }
                finally
                {
                    view.Close();
                }

                var item = diff!.Items.Single(i => i.Key == "sprites:spr_new");
                Assert.True(await window.ApplyDiffItemAsync(left, right, item, null));
                Assert.Contains(left.Data.Sprites, s => s.Name.Content == "spr_new");
                left.Undo();
                Assert.DoesNotContain(left.Data.Sprites, s => s.Name.Content == "spr_new");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DiffSettingsAreClamped()
    {
        var clamped = new Core.Settings.AppSettings { Diff = new Core.Settings.DiffOptions { ContextLines = 900, PictureMode = "x" } }.Clamped().Diff;
        Assert.Equal((50, "overlay"), (clamped.ContextLines, clamped.PictureMode));
    }
}
