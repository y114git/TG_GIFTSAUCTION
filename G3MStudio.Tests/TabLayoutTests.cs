using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>
/// Rule R5 for the main window: with the window squeezed, every control of an open tab, a dock panel and a settings section either lies inside the area that
/// clips it or has a scroll viewer that can bring it into view. Walks the first entry of each resource type of each fixture.
/// </summary>
public sealed class TabLayoutTests
{
    private static readonly Type[] Targets = [typeof(Button), typeof(TextBox), typeof(ComboBox), typeof(NumericUpDown), typeof(CheckBox), typeof(RadioButton), typeof(ToggleButton), typeof(Slider)];

    private static readonly string[] Types = ["general", "sprites", "tileSets", "sounds", "paths", "scripts", "shaders", "fonts", "timelines", "objects", "rooms", "extensions", "sequences", "animationCurves", "particleSystems", "texturePageItems", "embeddedTextures", "uiLayers"];

    [Theory]
    [InlineData(1100, 700)]
    [InlineData(800, 520)]
    public async Task EveryTabAndPanelIsReachableInASmallWindow(int width, int height)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = width;
                window.Height = height;
                foreach (var file in Corpus.Files(Corpus.Fixtures))
                {
                    await window.OpenAsync(file);
                    var data = window.Workspaces.Current!.Data;
                    var tag = $"{Path.GetFileName(Path.GetDirectoryName(file))} {width}x{height}";
                    foreach (var type in Types)
                    {
                        if (ResourceTypes.All.First(t => t.Key == type).Items(data)?.FirstOrDefault() is not { } item) continue;
                        window.Docking.OpenTab("resource", ResourceTypes.KeyOf(type, ResourceTypes.NameOf(item)), false);
                        await Settle();
                        problems.AddRange(Audit(window).Select(p => $"{tag} {type}: {p}"));
                    }

                    foreach (var panel in (string[])["history", "git", "problems", "output", "usages", "reference", "search", "inspector"])
                    {
                        window.Registry.Execute("view.panel." + panel);
                        await Settle();
                        problems.AddRange(Audit(window).Select(p => $"{tag} panel {panel}: {p}"));
                    }
                }
            }
            finally { window.Close(); }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    [Fact]
    public async Task EverySettingsSectionIsReachableInASmallWindow()
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = 800;
                window.Height = 520;
                window.Registry.Execute("settings.open");
                await Settle();
                var nav = window.GetVisualDescendants().OfType<ListBox>().First(l => l.Name == "SettingsNav");
                for (var i = 0; i < nav.ItemCount; i++)
                {
                    nav.SelectedIndex = i;
                    await Settle();
                    problems.AddRange(Audit(window).Select(p => $"settings section {i}: {p}"));
                }
            }
            finally { window.Close(); }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(40);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Rect Where(Control control, Visual root) => control.TransformToVisual(root) is { } matrix ? new Rect(default, control.Bounds.Size).TransformToAABB(matrix) : default;

    /// <summary>Controls that are shown, cut by the window or an ancestor that clips, and that no scroll viewer between them and that ancestor can scroll to.</summary>
    private static IEnumerable<string> Audit(Window window)
    {
        var client = new Rect(default, window.Bounds.Size).Inflate(1);
        foreach (var visual in window.GetVisualDescendants().OfType<Control>())
        {
            if (!Targets.Any(t => t.IsInstanceOfType(visual)) || !visual.IsEffectivelyVisible || visual.Bounds.Width < 1 || visual.Bounds.Height < 1) continue;
            var rect = Where(visual, window);
            var inside = client;
            var scrollers = new List<ScrollViewer>();
            foreach (var ancestor in visual.GetVisualAncestors().OfType<Control>())
            {
                if (ancestor is ScrollViewer scroller) scrollers.Add(scroller);
                if (ancestor is ScrollViewer || ancestor.ClipToBounds && ancestor is not Window) inside = inside.Intersect(Where(ancestor, window).Inflate(1));
            }

            if (inside.Contains(rect) || scrollers.Any(s => CanScroll(s, rect, Where(s, window)))) continue;
            yield return $"{visual.GetType().Name} '{visual.Name}' at {rect} is outside {inside} and nothing scrolls to it [{string.Join("; ", scrollers.Select(x => $"{x.GetType().Name} {x.Name} ext {x.Extent} view {x.Viewport} v={x.VerticalScrollBarVisibility}"))}]";
        }
    }

    private static bool CanScroll(ScrollViewer scroller, Rect rect, Rect viewport)
    {
        var verticalCut = rect.Top < viewport.Top - 1 || rect.Bottom > viewport.Bottom + 1;
        var horizontalCut = rect.Left < viewport.Left - 1 || rect.Right > viewport.Right + 1;
        return (!verticalCut || scroller.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.Extent.Height > scroller.Viewport.Height + 1)
            && (!horizontalCut || scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.Extent.Width > scroller.Viewport.Width + 1);
    }
}
