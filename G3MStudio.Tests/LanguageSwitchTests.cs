using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia.Controls;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class LanguageSwitchTests(ITestOutputHelper output)
{
    private static readonly string[] Languages = ["ru", "ja", "en"];
    private static readonly string[] PanelIds = ["Explorer", "Search", "Inspector", "Problems", "Output", "Usages", "Reference", "History", "Git"];

    private static string[] Titles(string language)
    {
        Loc.Current.SetLanguage(language);
        return [.. PanelIds.Select(id => Loc.Current[id == "Reference" ? "panel.referenceTab" : "panel." + id.ToLowerInvariant()])];
    }

    private static string[] Shown(MainWindow window) => [.. window.DockHost.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text ?? "")];

    private static (int Strips, int Items) Shape(MainWindow window)
    {
        var tree = window.DockHost.GetVisualDescendants().ToList();
        return (tree.OfType<ToolTabStrip>().Count() + tree.OfType<DocumentTabStrip>().Count(), tree.OfType<ToolTabStripItem>().Count() + tree.OfType<DocumentTabStripItem>().Count());
    }

    private static void Settle(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [Fact]
    public async Task EveryDockTitleSwitchesAtOnceWithoutDuplicateStrips()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            // Switching the language tells every bound control, so it must happen on the dispatcher thread.
            var titles = new Dictionary<string, string[]> { ["en"] = Titles("en"), ["ru"] = Titles("ru"), ["ja"] = Titles("ja") };
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                window.Docking.OpenTab("resource", "sprites:spr_new", false);
                await Task.Delay(200);
                Settle(window);
                var shape = Shape(window);
                Assert.True(shape.Items >= PanelIds.Length, "the dock did not draw its tabs");
                var previous = "en";
                foreach (var language in Languages)
                {
                    var clock = Stopwatch.StartNew();
                    Loc.Current.SetLanguage(language);
                    Settle(window);
                    output.WriteLine($"switch to {language}: {clock.ElapsedMilliseconds} ms");
                    Assert.Equal(shape, Shape(window));
                    foreach (var id in PanelIds) Assert.Equal(Loc.Current[id == "Reference" ? "panel.referenceTab" : "panel." + id.ToLowerInvariant()], window.Docking.Find<Tool>(id)!.Title);
                    var shown = Shown(window);
                    foreach (var title in titles[language]) Assert.Contains(title, shown);
                    foreach (var title in titles[previous].Except(titles[language])) Assert.DoesNotContain(title, shown);
                    previous = language;
                }
            }
            finally { Loc.Current.SetLanguage("en"); window.Close(); }
        });
    }

    [Fact]
    public async Task HiddenTabsAreRelabelledWhenTheyComeToTheFront()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                string[] keys = ["objects:obj_0", "objects:obj_1", "objects:obj_2"];
                foreach (var key in keys) window.Docking.OpenTab("resource", key, false);
                Settle(window);
                Loc.Current.SetLanguage("ru");
                Settle(window);
                var english = Loc.Current["editors.object.addEventMenu"];
                Loc.Current.SetLanguage("en");
                var untranslated = Loc.Current["editors.object.addEventMenu"];
                Loc.Current.SetLanguage("ru");
                Assert.NotEqual(untranslated, english);
                foreach (var key in keys)
                {
                    window.Docking.OpenTab("resource", key, false);
                    Settle(window);
                    var buttons = window.DockHost.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "AddEventMenu" && b.IsEffectivelyVisible).ToList();
                    Assert.Single(buttons);
                    Assert.Equal(english, buttons[0].Content);
                }
            }
            finally { Loc.Current.SetLanguage("en"); window.Close(); }
        });
    }
}
