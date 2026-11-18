using Avalonia;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Panel titles and the tab strips of the docks are never cut: at every interface scale and language the title shows whole, and no tab is drawn half.</summary>
public sealed class PanelChromeTests
{
    internal static async Task Settle(Window? window = null)
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
            window?.UpdateLayout();
        }
    }

    internal static async Task<MainWindow> Shown(TempDir dir, string path, string language, int uiScale, int width, int height)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        await window.OpenAsync(path);
        window.Width = width;
        window.Height = height;
        window.Show();
        Loc.Current.SetLanguage(language);
        window.Workspaces.Settings.Update(x => x with { UiScale = uiScale });
        await Settle(window);
        return window;
    }

    private static readonly string[] Languages = ["en", "ru", "es", "ja", "zh_cn"];
    private static readonly (int Scale, int Width, int Height)[] Sizes = [(100, 1920, 1080), (125, 1536, 864), (150, 1280, 720), (200, 960, 540), (200, 800, 450)];

    private static readonly string[] Panels = ["history", "search", "inspector"];

    public static TheoryData<string, int, int, int> Screens()
    {
        var data = new TheoryData<string, int, int, int>();
        foreach (var language in Languages)
            foreach (var (scale, width, height) in Sizes) data.Add(language, scale, width, height);
        return data;
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public async Task EveryPanelTitleShowsWhole(string language, int uiScale, int width, int height)
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = await Shown(dir, path, language, uiScale, width, height);
            try
            {
                foreach (var panel in Panels) window.Registry.Execute("view.panel." + panel);
                await Settle(window);
                var cut = new List<string>();
                foreach (var title in window.DockHost.GetVisualDescendants().OfType<ToolChromeControl>().Select(c => c.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "PART_Title")))
                {
                    if (!title.IsEffectivelyVisible || string.IsNullOrEmpty(title.Text)) continue;
                    var wanted = new FormattedText(title.Text, CultureInfo.CurrentCulture, title.FlowDirection, new Typeface(title.FontFamily, title.FontStyle, title.FontWeight), title.FontSize, null).Width;
                    if (title.Bounds.Width + 0.5 < wanted) cut.Add($"'{title.Text}' gets {title.Bounds.Width:0} of {wanted:0}");
                }

                Assert.True(cut.Count == 0, $"{language} {uiScale}% {width}x{height}: {string.Join("; ", cut)}");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AHeaderOrStripThatLoadedIsNotWiredAgain()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        await HeadlessApp.Run(async () =>
        {
            var window = await Shown(dir, path, "en", 100, 1280, 720);
            try
            {
                window.Registry.Execute("view.panel.history");
                window.Docking.OpenTab("resource", "objects:obj_0", false);
                await Settle(window);
                var host = window.DockHost;
                // the dock wired its headers and strips when they first loaded; a second Loaded must find them wired
                var chromes = host.GetVisualDescendants().OfType<ToolChromeControl>().ToList();
                var strips = host.GetVisualDescendants().OfType<TemplatedControl>().Where(c => c is DocumentTabStrip or ToolTabStrip).ToList();
                Assert.NotEmpty(chromes);
                Assert.NotEmpty(strips);
                foreach (var chrome in chromes) Assert.False(G3MStudio.Docking.ToolHeaderFit.Watch(chrome));
                foreach (var strip in strips) Assert.False(G3MStudio.Docking.TabStripFit.Watch(strip));
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData("es", 200, 960, 540)]
    [InlineData("ru", 200, 800, 450)]
    [InlineData("ru", 150, 1280, 720)]
    public async Task ATabCutByTheStripEdgeShowsNoCloseButtonAndTheChosenTabIsWhole(string language, int uiScale, int width, int height)
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 12);
        await HeadlessApp.Run(async () =>
        {
            var window = await Shown(dir, path, language, uiScale, width, height);
            try
            {
                window.Registry.Execute("view.panel.search");
                for (var n = 0; n < 11; n++) window.Docking.OpenTab("resource", "objects:obj_" + n, false);
                await Settle(window);
                var problems = new List<string>();
                var strips = window.DockHost.GetVisualDescendants().OfType<Control>().Where(c => c is DocumentTabStrip or ToolTabStrip).ToList();
                Assert.NotEmpty(strips);
                foreach (var strip in strips)
                {
                    var view = strip.GetVisualDescendants().OfType<ScrollViewer>().First().Presenter!;
                    foreach (var item in strip.GetVisualDescendants().OfType<ContentControl>().Where(c => c is DocumentTabStripItem or ToolTabStripItem))
                    {
                        var left = item.TranslatePoint(default, view)!.Value.X;
                        var whole = left >= -0.5 && left + item.Bounds.Width <= view.Bounds.Width + 0.5;
                        var close = item.GetVisualDescendants().OfType<Button>().FirstOrDefault();
                        if (!whole && close is { Opacity: > 0 }) problems.Add($"{strip.GetType().Name} tab at {left:0} width {item.Bounds.Width:0} of {view.Bounds.Width:0} keeps its close button");
                        if (!whole && item.Classes.Contains(":selected")) problems.Add($"{strip.GetType().Name} chosen tab at {left:0} width {item.Bounds.Width:0} of {view.Bounds.Width:0} is cut");
                    }
                }

                Assert.True(problems.Count == 0, $"{language} {uiScale}%: {string.Join("; ", problems)}");
            }
            finally { window.Close(); }
        });
    }
}
