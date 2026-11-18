using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>The Resize dialogs say what a size does to the texture pages before it is applied, and change the line while the numbers change.</summary>
public sealed class TextureFitDialogTests
{
    private static async Task<Workspace> Open(TempDir dir)
    {
        var path = dir.File("data.win");
        using (var data = CopyResourcesTests.Source()) TestData.Write(path, data);
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static NumericUpDown Spin(Window dialog, string name) => dialog.GetLogicalDescendants().OfType<NumericUpDown>().Single(n => n.Name == name);

    [Fact]
    public async Task TheResizeDialogsSayWhatASizeDoesToTheTexturePages()
    {
        using var dir = new TempDir();
        using var workspace = await Open(dir);
        await HeadlessApp.Run(() =>
        {
            using var session = new WorkspaceImageSession(workspace, "spr_a");
            var dialog = new ResizeImageWindow((32, 32), 1, [], null, false, choice => session.PlanResize(0, choice.Spec, choice.AllFrames) is { } steps ? FitText.Describe(steps) : null);
            var item = workspace.Data.Sprites.First(s => s.Name.Content == "spr_a").Textures[0].Texture!;
            // The language is shared with other tests, so the expected line is worded at the same moment as the dialog's.
            string Expect(int width, int height, FitKind kind)
            {
                var steps = TextureFit.Plan(workspace.Data, [new FitRequest(item, width, height)]);
                Assert.Equal(kind, Assert.Single(steps).Kind);
                return FitText.Describe(steps);
            }

            Assert.Equal(Expect(32, 32, FitKind.InPlace), dialog.FitText);
            Spin(dialog, "ResizeWidth").Value = 100;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Expect(100, 100, FitKind.Grow), dialog.FitText); // the aspect is kept: 100 x 100
            dialog.Close();

            var page = workspace.Data.Sprites.First(s => s.Name.Content == "spr_a").Textures[0].Texture!.TexturePage;
            string Plan((int Width, int Height) size, FitOptions? fit = null)
            {
                using (workspace.Lock.Read()) return FitText.Describe(TextureFit.Plan(workspace.Data, [], page, size, fit));
            }

            var pageDialog = new ResizePageWindow(page.Name.Content!, (64, 64), Plan);
            Assert.Equal(FitText.Describe([]), pageDialog.FitText);
            Spin(pageDialog, "PageWidth").Value = 16;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Plan((16, 64)), pageDialog.FitText);
            Assert.NotEqual(FitText.Describe([]), pageDialog.FitText); // the frame no longer lies on the page and moves
            pageDialog.Close();

            // Several value changes in one pass (a held arrow key) plan once, with the last values.
            var plans = 0;
            var counted = new ResizePageWindow(page.Name.Content!, (64, 64), (size, fit) =>
            {
                plans++;
                return Plan(size, fit);
            });
            for (var width = 32; width <= 128; width *= 2) Spin(counted, "PageWidth").Value = width;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, plans);
            Assert.Equal(Plan((128, 64)), counted.FitText);
            counted.Close();

            // The Advanced choices are collapsed at first and change the line at once: without growing, the frame takes the free 128 x 128 page.
            var withChoices = new ResizeImageWindow((32, 32), 1, [], null, false,
                choice => session.PlanResize(0, choice.Spec, choice.AllFrames, choice.Fit) is { } steps ? FitText.Describe(steps) : null, ["Texture 0"], 2048);
            Spin(withChoices, "ResizeWidth").Value = 100;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Expect(100, 100, FitKind.Grow), withChoices.FitText);
            Assert.False(withChoices.GetLogicalDescendants().OfType<Expander>().Single(e => e.Name == "FitAdvanced").IsExpanded);
            withChoices.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "FitGrow").IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new FitOptions(Grow: false), withChoices.Choice.Fit);
            Assert.Equal(FitText.Describe(TextureFit.Plan(workspace.Data, [new FitRequest(item, 100, 100)], options: new FitOptions(Grow: false))), withChoices.FitText);
            Assert.NotEqual(Expect(100, 100, FitKind.Grow), withChoices.FitText);
            withChoices.Close();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public Task AnotherTextureGroupPlansTheFitAgain() => HeadlessApp.Run(() =>
    {
        var dialog = new ResizeImageWindow((32, 32), 1, ["Default", "Other"], "Default", false, choice => choice.Group);
        try
        {
            Assert.Equal("Default", dialog.FitText);
            dialog.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "ResizeGroup").SelectedItem = "Other";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Other", dialog.FitText);
        }
        finally { dialog.Close(); }

        return Task.CompletedTask;
    });

    [Fact]
    public void ANewPageOrMovedPicturesAreNamedEvenWhenTheLineIsCutShort()
    {
        // six frames that take free space on six pages, and a seventh that needs a new page: the line keeps four reasons, the new page first
        List<FitStep> steps = [.. Enumerable.Range(0, 6).Select(i => new FitStep(FitKind.FreeSpace, $"Texture {i}", (64, 64), (64, 64), 0)), new FitStep(FitKind.NewPage, "Texture 9", (0, 0), (128, 128), 0)];
        var line = FitText.Describe(steps);
        Assert.StartsWith(FitText.Describe([steps[^1]]), line, StringComparison.Ordinal);
        Assert.EndsWith("…", line, StringComparison.Ordinal);
    }

    [Fact]
    public Task TheAdvancedChoicesAreRememberedForTheNextDialogButNotThePage() => HeadlessApp.Run(() =>
    {
        var panel = new FitOptionsPanel(["Texture 1"], 2048);
        var before = panel.Options;
        try
        {
            panel.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "FitNewPage").IsChecked = false;
            panel.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "FitPage").SelectedIndex = 1;
            panel.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "FitMaxSide").SelectedItem = "4096 x 4096";
            Assert.Equal(new FitOptions("Texture 1", NewPage: false, MaxSide: 4096), panel.Options);
            panel.Remember();
            Assert.Equal(new FitOptions(NewPage: false, MaxSide: 4096), new FitOptionsPanel(["Texture 1"], 2048).Options);
            Assert.Equal(new FitOptions(NewPage: false, MaxSide: 4096), FitOptionsPanel.Current); // the canvas handles place with them too
        }
        finally
        {
            // other tests start from the default choices
            var reset = new FitOptionsPanel([], 2048);
            reset.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "FitNewPage").IsChecked = before.NewPage;
            reset.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Name == "FitGrow").IsChecked = before.Grow;
            reset.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Name == "FitMaxSide").SelectedIndex = 0;
            reset.Remember();
        }

        return Task.CompletedTask;
    });
}
