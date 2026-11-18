using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using G3MStudio.Docking;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>Dragging a tab past its neighbours moves it and changes nothing else: no tab fades, flashes, grows or is made again, with animations on or off.</summary>
public sealed class TabReorderTests
{
    private sealed record Look(DocumentTabStripItem Container, double Opacity, Color Fill, double Width, double Height, double Scale, double ContentOpacity, double Left);

    private static Color Fill(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    private static Dictionary<string, Look> Sample(DockControl control) => control.GetVisualDescendants().OfType<DocumentTabStripItem>().Where(t => t.DataContext is Dock.Model.Core.IDockable).ToDictionary(
        t => ((Dock.Model.Core.IDockable)t.DataContext!).Id!,
        t => new Look(t, t.Opacity, Fill(t.Background), t.Bounds.Width, t.Bounds.Height, (t.RenderTransform as ScaleTransform)?.ScaleX ?? 1, ((t.DataContext as Dock.Model.Avalonia.Controls.Document)?.Content as Control)?.Opacity ?? 1, t.Bounds.Left));

    private static Point CentreOf(DocumentTabStripItem tab, Window window) => tab.TranslatePoint(new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2), window)!.Value;

    private static async Task Settle()
    {
        for (var i = 0; i < 6; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Motion.CancelAll();
            await Task.Delay(10);
        }
    }

    [Theory]
    [InlineData("Full")]
    [InlineData("Off")]
    public async Task DraggingATabPastItsNeighboursChangesNothingButPositions(string level)
    {
        await HeadlessApp.Run(async () =>
        {
            Motion.TimedInHeadless = true;
            var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
            var control = new DockControl();
            dock.Attach(control);
            var window = new Window { Width = 1000, Height = 700, Content = control };
            window.Show();
            try
            {
                Motion.SetLevel(level);
                string[] names = ["a", "b", "c", "d"];
                foreach (var name in names) dock.OpenTab("resource", "sprites:spr_" + name, false);
                await Settle();
                dock.OpenTab("resource", "sprites:spr_a", false); // a is in front
                await Settle();
                await Task.Delay(400); // the fill transitions of the tabs that just opened are over
                Dispatcher.UIThread.RunJobs();
                var ids = dock.Documents().Select(d => d.Id).ToList();
                var before = Sample(control);
                Assert.Equal(4, before.Count);
                var steady = before.ToDictionary(p => p.Key, p => p.Value);

                var problems = new List<string>();
                void Check(string step)
                {
                    foreach (var (id, now) in Sample(control))
                    {
                        var was = steady[id];
                        if (now.Opacity != 1) problems.Add($"{step}: {id} opacity {now.Opacity}");
                        if (now.ContentOpacity != 1) problems.Add($"{step}: {id} editor opacity {now.ContentOpacity}");
                        if (now.Scale != 1) problems.Add($"{step}: {id} scale {now.Scale}");
                        if (Math.Abs(now.Height - was.Height) > 0.5 || Math.Abs(now.Width - was.Width) > 0.5) problems.Add($"{step}: {id} size {now.Width}x{now.Height} was {was.Width}x{was.Height}");
                        if (now.Fill != was.Fill && id != ids[0]) problems.Add($"{step}: {id} fill {now.Fill} was {was.Fill}");
                    }

                    var shown = Sample(control);
                    if (shown.Values.Count(l => l.Container.IsSelected) != 1) problems.Add($"{step}: {shown.Values.Count(l => l.Container.IsSelected)} tabs are chosen");
                    if (shown.Count != names.Length) problems.Add($"{step}: {shown.Count} tabs shown");
                    foreach (var (id, now) in shown)
                        if (id != ids[0] && !ReferenceEquals(now.Container, steady[id].Container)) problems.Add($"{step}: {id} got a new tab control"); // the library makes the moved tab's control again; the others keep theirs
                    if (Motion.Active > 0) problems.Add($"{step}: {Motion.Active} animations running");
                }

                var first = Sample(control)[ids[0]].Container;
                var last = Sample(control)[ids[^1]].Container;
                var start = CentreOf(first, window);
                var end = CentreOf(last, window) + new Vector(first.Bounds.Width / 2, 0);
                window.MouseDown(start, MouseButton.Left);
                for (var step = 1; step <= 24; step++)
                {
                    window.MouseMove(start + ((end - start) * step / 24.0), RawInputModifiers.LeftMouseButton);
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(8);
                    Check("move " + step);
                }

                window.MouseUp(end, MouseButton.Left);
                Check("drop");
                window.UpdateLayout();
                Check("drop laid out");
                for (var frame = 1; frame <= 4; frame++)
                {
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Dispatcher.UIThread.RunJobs();
                    Check("drop frame " + frame);
                    await Task.Delay(30);
                }

                Check("drop settling");
                await Settle();
                Check("settled");

                var order = dock.Documents().Select(d => d.Id).ToList();
                Assert.True(order[^1] == ids[0], $"the dragged tab ends last, order {string.Join(",", order)}" + Environment.NewLine + string.Join(Environment.NewLine, problems.Distinct().Take(20)));
                Assert.Equal(0, Motion.Active);
                Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct().Take(30)));
            }
            finally
            {
                window.Close();
                Motion.CancelAll();
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }
        });
    }
}
