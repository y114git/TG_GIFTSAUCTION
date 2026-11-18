using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The Explorer inside the real docked host answers a change at once: the layout of one pass already shows it, at every UI scale (item 49).</summary>
public sealed class ExplorerDockedLayoutTests
{
    private static T Named<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static async Task<MainWindow> Opened(TempDir dir, string language, int uiScale, double renderScale)
    {
        Loc.Current.SetLanguage(language);
        var window = TabsWorkspaceTests.NewWindow(dir);
        await window.OpenAsync(TestData.WriteSample(dir, objects: 40, withCode: true));
        window.Workspaces.Settings.Update(x => x with { UiScale = uiScale });
        Avalonia.Headless.HeadlessWindowExtensions.SetRenderScaling(window, renderScale);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    // A real press and release at the middle of the button, as the mouse does it.
    private static void Click(Button button)
    {
        var window = (Window)TopLevel.GetTopLevel(button)!;
        var at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        Avalonia.Headless.HeadlessWindowExtensions.MouseMove(window, at);
        Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, at, Avalonia.Input.MouseButton.Left);
        Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, at, Avalonia.Input.MouseButton.Left);
    }

    // A finished layout pass leaves nothing to measure or arrange; a control left invalid is drawn from its old measure until something else resizes the window.
    internal static void AssertSettled(Visual root, string when)
    {
        var stale = root.GetSelfAndVisualDescendants().OfType<Layoutable>().Where(l => l.IsEffectivelyVisible && !(l.IsMeasureValid && l.IsArrangeValid)).Select(l => $"{l.GetType().Name} {l.Name} {l.Bounds} desired {l.DesiredSize} parent {l.GetVisualParent()?.GetType().Name} {(l.GetVisualParent() as Layoutable)?.Bounds}").ToList();
        Assert.True(stale.Count == 0, $"{when}: still invalid after the layout pass: {string.Join(", ", stale.Take(5))}");
    }

    // The chevron of the fold button is drawn: it has a size and sits inside the button.
    private static void AssertArrowShown(Button fold, string when)
    {
        Assert.True(fold.IsEffectivelyVisible && fold.Bounds is { Width: > 0, Height: > 0 }, $"{when}: fold button {fold.Bounds}");
        var arrow = Assert.IsAssignableFrom<Control>(fold.Content);
        Assert.True(arrow.IsEffectivelyVisible && arrow.Bounds is { Width: > 0, Height: > 0 }, $"{when}: arrow {arrow.Bounds}");
        Assert.True(arrow.DesiredSize.Width > 0 && arrow.DesiredSize.Height > 0, $"{when}: arrow was not measured");
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(125, 1.0)]
    [InlineData(150, 1.0)]
    [InlineData(100, 1.25)]
    [InlineData(125, 1.5)]
    [InlineData(175, 1.75)]
    public async Task TheFoldArrowFoldsAndUnfoldsTheOptionsBarInTheSameLayoutPassAndNeverLosesItsIcon(int uiScale, double renderScale)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await Opened(dir, "en", uiScale, renderScale);
            try
            {
                var view = window.Explorer;
                var list = Named<ListBox>(view, "List");
                var fold = Named<Button>(view, "OptionsFold");
                var row = Named<DockPanel>(view, "OptionsRow");
                view.Select("scripts:scr_3"); // the count line shows "1 selected" beside the total, as on the owner's screen
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var before = list.Bounds.Height;
                Assert.True(row.IsVisible && row.Bounds.Height > 0);
                AssertArrowShown(fold, "start");
                for (var i = 0; i < 3; i++)
                {
                    Click(fold);
                    window.UpdateLayout(); // one layout pass, no queued jobs in between
                    Assert.True(view.OptionsFolded && !row.IsVisible);
                    Assert.True(list.Bounds.Height > before + 10, $"fold {i}: list {list.Bounds.Height} did not grow from {before}");
                    AssertArrowShown(fold, $"folded {i}");
                    AssertSettled(window, $"folded {i}");

                    Click(fold);
                    window.UpdateLayout();
                    Assert.True(!view.OptionsFolded && row.IsVisible && row.Bounds.Height > 0, $"unfold {i}: row {row.Bounds}");
                    Assert.Equal(before, list.Bounds.Height, 1);
                    AssertArrowShown(fold, $"unfolded {i}");
                    AssertSettled(window, $"unfolded {i}");
                }
            }
            finally { window.Workspaces.Close(); window.Close(); }
        });
    }

    // The text of a label is drawn whole: its box is as wide as the text needs.
    private static void AssertTextFits(TextBlock text, string when)
    {
        var natural = new TextBlock { Text = text.Text, FontSize = text.FontSize, FontFamily = text.FontFamily, FontWeight = text.FontWeight };
        natural.Measure(Size.Infinity);
        Assert.True(text.Bounds.Width >= natural.DesiredSize.Width - 1, $"{when}: \"{text.Text}\" is drawn in {text.Bounds.Width} px, needs {natural.DesiredSize.Width}");
    }

    private static TextBlock LabelOf(Button button) => button.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible);

    [Theory]
    [InlineData("en", 125, 1.0)]
    [InlineData("ru", 125, 1.0)]
    [InlineData("ru", 150, 1.5)]
    [InlineData("ja", 175, 1.75)]
    public async Task TheFooterCountAndTheViewToggleKeepTheirWholeTextWhenTheLanguageChangesOrTheViewSwaps(string language, int uiScale, double renderScale)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await Opened(dir, language == "en" ? "ru" : "en", uiScale, renderScale);
            try
            {
                var view = window.Explorer;
                var footer = Named<TextBlock>(view, "Footer");
                var toggle = Named<Button>(view, "ViewToggle");
                Loc.Current.SetLanguage(language);
                window.UpdateLayout();
                AssertTextFits(footer, "footer after the language change");
                AssertTextFits(LabelOf(toggle), "view toggle after the language change");
                AssertSettled(window, "language change");

                Click(toggle);
                window.UpdateLayout();
                Assert.Equal(Loc.Current[view.Model.FolderView ? "explorer.viewFolders" : "explorer.viewTypes"], LabelOf(toggle).Text);
                AssertTextFits(LabelOf(toggle), "view toggle after the swap");
                AssertSettled(window, "view swap");
                Click(toggle);
                window.UpdateLayout();
                AssertTextFits(LabelOf(toggle), "view toggle swapped back");
                AssertSettled(window, "view swapped back");
            }
            finally { window.Workspaces.Close(); window.Close(); }
        });
    }

    [Theory]
    [InlineData(100, 1.0)]
    [InlineData(110, 1.0)]
    [InlineData(125, 1.0)]
    [InlineData(150, 1.5)]
    [InlineData(175, 1.75)]
    [InlineData(200, 2.0)]
    public async Task EveryPanelOfTheWindowSettlesInOneLayoutPassAtAnyUiScaleAndWindowSize(int uiScale, double renderScale)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await Opened(dir, "en", uiScale, renderScale);
            try
            {
                AssertSettled(window, "open");
                window.Registry.Execute("settings.open");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AssertSettled(window, "settings page");
                foreach (var (width, height) in new[] { (1100.0, 700.0), (1013.0, 641.0), (1380.0, 905.0), (1100.0, 700.0) })
                {
                    window.Width = width;
                    window.Height = height;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    AssertSettled(window, $"{width} x {height}");
                }
            }
            finally { window.Workspaces.Close(); window.Close(); }
        });
    }
}
