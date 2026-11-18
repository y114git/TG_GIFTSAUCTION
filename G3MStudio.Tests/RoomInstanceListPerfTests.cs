using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile.Models;
using G3MStudio.Editor;

#pragma warning disable CA2000 // windows and models made for a check only

namespace G3MStudio.Tests;

/// <summary>The list of a room's instances with thousands of rows (picture, checkbox, text) builds and scrolls inside a budget and only realizes the rows in view.</summary>
public sealed class RoomInstanceListPerfTests(ITestOutputHelper output)
{
    private const int Instances = 5000;

    // Generous: the headless run is slower than a real window; the point is to catch a list that builds every row.
    private static readonly TimeSpan BuildBudget = TimeSpan.FromMilliseconds(1500), ScrollBudget = TimeSpan.FromMilliseconds(5000);

    [Fact]
    public async Task ThousandsOfInstancesBuildFastAndOnlyTheRowsInViewAreMade()
    {
        await HeadlessApp.Run(() =>
        {
            var objects = Enumerable.Range(0, 20).Select(i => new GameMakerGameObject { Name = new GameMakerString("obj_" + i) }).ToList();
            var layer = new GameMakerRoom.Layer { LayerName = new GameMakerString("l"), LayerType = GameMakerRoom.LayerType.Instances, Data = new GameMakerRoom.Layer.LayerInstancesData { Instances = [] } };
            for (var i = 0; i < Instances; i++) layer.InstancesData.Instances.Add(new GameMakerRoom.GameObject { InstanceID = (uint)(100000 + i), X = i, Y = i * 2, ObjectDefinition = objects[i % objects.Count] });
            var list = new RoomElementList(new GameMakerRoom { Layers = [layer] });
            var window = new Window { Content = list, Width = 500, Height = 400 };
            window.Show();
            try
            {
                var watch = Stopwatch.StartNew();
                list.Show(layer);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var build = watch.Elapsed;
                var realized = list.GetVisualDescendants().OfType<ListBoxItem>().Count();
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                watch.Restart();
                for (var step = 0; step < 100; step++)
                {
                    scroll.Offset = new Vector(0, step * 600);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                var scrolled = watch.Elapsed;
                output.WriteLine($"{Instances} instances: build {build.TotalMilliseconds:0} ms, 100 scroll steps {scrolled.TotalMilliseconds:0} ms, rows realized {realized}");
                Assert.True(realized < 200, $"{realized} rows were made");
                Budget.AssertUnder(build.TotalMilliseconds, BuildBudget.TotalMilliseconds, "build of the instance list", output);
                Budget.AssertUnder(scrolled.TotalMilliseconds, ScrollBudget.TotalMilliseconds, "100 scroll steps of the instance list", output);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RowsShowTheirOwnElementAfterScrollingAndFiltering()
    {
        await HeadlessApp.Run(() =>
        {
            var layer = new GameMakerRoom.Layer { LayerName = new GameMakerString("l"), LayerType = GameMakerRoom.LayerType.Instances, Data = new GameMakerRoom.Layer.LayerInstancesData { Instances = [] } };
            for (var i = 0; i < 300; i++) layer.InstancesData.Instances.Add(new GameMakerRoom.GameObject { InstanceID = (uint)i, X = i, ObjectDefinition = new GameMakerGameObject { Name = new GameMakerString("obj_" + i) } });
            var nicknames = new G3MStudio.Core.Workspace.Nicknames();
            var list = new RoomElementList(new GameMakerRoom { Layers = [layer] }, nicknames);
            var window = new Window { Content = list, Width = 500, Height = 400 };
            window.Show();
            try
            {
                list.Show(layer);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                nicknames.Set("objects:obj_0", "hero");
                list.Relabel();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Contains(list.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("hero", StringComparison.Ordinal) == true);
                AssertRowsMatch(list, nicknames);
                list.GetVisualDescendants().OfType<ScrollViewer>().First().Offset = new Vector(0, 3000);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AssertRowsMatch(list, nicknames);
                list.Filter = "obj_29";
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AssertRowsMatch(list, nicknames);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    // A reused row must not keep the text of the element it showed before.
    private static void AssertRowsMatch(RoomElementList list, G3MStudio.Core.Workspace.Nicknames nicknames)
    {
        var rows = list.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsVisible && i.DataContext is GameMakerRoom.GameObject).ToList();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
            Assert.Equal(RoomElementList.Describe(row.DataContext!, nicknames), row.GetVisualDescendants().OfType<TextBlock>().Last().Text);
    }
}
