using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using G3MStudio.Docking;

namespace G3MStudio.Tests;

public sealed class DockFloatTests
{
    private static Task Run(Action<StudioDock> test) => HeadlessApp.Run(() =>
    {
        var dock = new StudioDock();
        var control = new DockControl();
        dock.Attach(control);
        var window = new Window { Width = 1000, Height = 700, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try { test(dock); }
        finally
        {
            foreach (var floating in (dock.Layout.Windows ?? []).ToList()) floating.Host?.Exit();
            window.Close();
        }
    });

    [Fact]
    public Task DockWorksForAPanelRestoredFloatingWithoutItsDock() => Run(dock =>
    {
        dock.Factory.FloatDockable(dock.Find<IDockable>("Explorer")!);
        dock.Factory.FloatDockable(dock.Find<IDockable>("Search")!);
        Dispatcher.UIThread.RunJobs();
        var later = new StudioDock();
        later.Restore(dock.Capture(null), _ => true);
        try
        {
            var items = later.Find<IDock>("Main")!.VisibleDockables!;
            if (items.FirstOrDefault(d => d.Id == "LeftDock") is { } left) items.Remove(left);
            Assert.DoesNotContain(items, d => d.Id == "LeftDock");
            later.DockHome(later.Find<IDockable>("Explorer")!);
            Assert.Contains(items, d => d.Id == "LeftDock");
            Assert.Equal("LeftDock", later.Find<IDockable>("Explorer")!.Owner!.Id);
        }
        finally
        {
            foreach (var floating in (later.Layout.Windows ?? []).ToList()) floating.Host?.Exit();
        }
    });

    [Fact]
    public Task AFloatingWindowIsHandedToTheMainWindowForItsLinkPicturesAndHandler() => Run(dock =>
    {
        var floated = new List<Window>();
        dock.FloatCreated = floated.Add;
        dock.Factory.FloatDockable(dock.Find<IDockable>("Inspector")!);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(Assert.Single(dock.Layout.Windows!).Host, Assert.Single(floated));
    });

    [Fact]
    public Task AFloatingPanelIsShownAgainWhenTheSavedLayoutIsRestored() => Run(dock =>
    {
        dock.Factory.FloatDockable(dock.Find<IDockable>("Inspector")!);
        Dispatcher.UIThread.RunJobs();
        var state = dock.Capture(null);
        dock.Reset();
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(dock.Layout.Windows ?? []);
        dock.Restore(state, _ => true);
        Dispatcher.UIThread.RunJobs();
        var host = Assert.IsAssignableFrom<Window>(Assert.Single(dock.Layout.Windows!).Host);
        Assert.True(host.IsVisible);
    });

    [Fact]
    public Task ClosingTheMainWindowLeavesTheFloatingPanelInTheLayoutItSaves() => HeadlessApp.Run(() =>
    {
        var dock = new StudioDock();
        var control = new DockControl();
        dock.Attach(control);
        var window = new Window { Width = 1000, Height = 700, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        dock.Factory.FloatDockable(dock.Find<IDockable>("Inspector")!);
        Dispatcher.UIThread.RunJobs();
        var saved = "";
        window.Closing += (_, _) => saved = dock.Save();
        window.Close();
        Dispatcher.UIThread.RunJobs();
        var later = new StudioDock();
        later.Restore(saved);
        try
        {
            Assert.Single(later.Layout.Windows!);
            Assert.NotNull(later.Find<IDockable>("Inspector"));
        }
        finally { foreach (var floating in (later.Layout.Windows ?? []).ToList()) floating.Host?.Exit(); }
    });

    [Fact]
    public Task ResetClosesTheFloatingWindows() => Run(dock =>
    {
        dock.Factory.FloatDockable(dock.Find<IDockable>("Inspector")!);
        Dispatcher.UIThread.RunJobs();
        var host = Assert.IsAssignableFrom<Window>(dock.Layout.Windows!.Single().Host);
        Assert.True(host.IsVisible);
        dock.Reset();
        Dispatcher.UIThread.RunJobs();
        Assert.False(host.IsVisible);
    });

    [Fact]
    public Task AFloatingWindowClosesWhenItsLastPanelLeaves() => Run(dock =>
    {
        dock.Factory.FloatDockable(dock.Find<IDockable>("Inspector")!);
        Dispatcher.UIThread.RunJobs();
        var host = Assert.IsAssignableFrom<Window>(dock.Layout.Windows!.Single().Host);
        dock.Factory.CloseDockable(dock.Find<IDockable>("Inspector")!);
        Dispatcher.UIThread.RunJobs();
        Assert.False(host.IsVisible);
    });

    [Fact]
    public Task ClosingAFloatingPanelDocksItAtItsOldPlace() => Run(dock =>
    {
        var inspector = dock.Find<IDockable>("Inspector")!;
        dock.Factory.FloatDockable(inspector);
        Dispatcher.UIThread.RunJobs();
        dock.Factory.CloseDockable(inspector);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("RightDock", Assert.IsAssignableFrom<IDock>(inspector.Owner).Id);
        Assert.Empty(dock.Layout.Windows ?? []);
    });

    [Fact]
    public Task AFloatingWindowUsesTheFontTokensOfTheMainWindow() => Run(dock =>
    {
        dock.Factory.FloatDockable(dock.Find<IDockable>("Inspector")!);
        Dispatcher.UIThread.RunJobs();
        var host = Assert.IsAssignableFrom<Window>(dock.Layout.Windows!.Single().Host);
        var app = Avalonia.Application.Current!;
        Assert.True(app.TryGetResource("Font.Weight", app.ActualThemeVariant, out var weight));
        Assert.Equal(weight, host.FontWeight);
        Assert.Equal(Avalonia.Media.FontWeight.Bold, host.FontWeight);
        Assert.True(app.TryGetResource("FontSize.Body", app.ActualThemeVariant, out var size));
        Assert.Equal(size, host.FontSize);
        Assert.True(app.TryGetResource("Font.Body", app.ActualThemeVariant, out var family));
        Assert.Equal(family, host.FontFamily);
    });
}
