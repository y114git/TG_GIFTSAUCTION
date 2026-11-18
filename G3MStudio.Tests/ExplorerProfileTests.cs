using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>Times the Explorer with 20 000 resources, folders, a filter and icons on: building the rows, folding and unfolding, and the filter. Numbers go to the test output.</summary>
public sealed class ExplorerProfileTests(ITestOutputHelper output)
{
    private const int Resources = 20_000;

    // Generous: the headless run is slower than a real window; the budgets catch a step that goes quadratic.
    private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(1500);

    private static double Ms(Stopwatch watch)
    {
        var ms = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        return ms;
    }

    [Fact]
    public async Task TwentyThousandResourcesWithFoldersFilterAndIconsStayInsideTheBudget()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var window = new Window { Width = 360, Height = 600, Content = view };
            window.Show();
            try
            {
                view.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "ThumbToggle").IsChecked = true;
                using var data = ExplorerModelTests.Generate(Resources);
                var watch = Stopwatch.StartNew();
                view.Load("game.win", data);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var load = Ms(watch);

                // 100 folders of 50 objects each.
                var organizer = Organizer.Empty;
                for (var f = 0; f < 100; f++)
                {
                    string? id;
                    (organizer, id) = organizer.AddFolder("folder " + f, null);
                    organizer = organizer.MoveTo(Enumerable.Range(f * 50, 50).Select(i => ResourceTypes.KeyOf("objects", "obj_" + i)), id);
                }

                view.Model.Organizer = organizer;
                view.Model.DefaultFolderView = true;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var folders = Ms(watch);

                view.Model.SetFilter("obj_12");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var filter = Ms(watch);
                view.Model.SetFilter("");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var cleared = Ms(watch);

                var type = view.Model.Rows.First(r => r.Kind == ExplorerRowKind.Type).Key;
                view.Model.Toggle(type);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var fold = Ms(watch);
                view.Model.Toggle(type);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var unfold = Ms(watch);

                output.WriteLine($"{Resources} resources, rows {view.Model.Rows.Count}: load+layout {load:0} ms, 100 folders {folders:0} ms, filter {filter:0} ms, clear {cleared:0} ms, fold {fold:0} ms, unfold {unfold:0} ms");
                foreach (var (what, ms) in new[] { ("load", load), ("folders", folders), ("filter", filter), ("clear", cleared), ("fold", fold), ("unfold", unfold) })
                    G3MStudio.Tests.Budget.AssertUnder(ms, Limit.TotalMilliseconds, what, output); // the shared budget scales with the machine and is only reported on a shared runner
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }
}
