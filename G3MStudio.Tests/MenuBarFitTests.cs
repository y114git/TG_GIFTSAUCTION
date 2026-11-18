using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Core.Commands;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class MenuBarFitTests
{
    /// <summary>At the window minimum, in every language and with any context menu showing, no top-level menu is cut off: it is in the bar or in the named "..." menu.</summary>
    [Fact]
    public async Task EveryTopLevelMenuIsReachableAtTheMinimumWidthInEveryLanguage()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = window.MinWidth;
                Dispatcher.UIThread.RunJobs();
                foreach (var language in Loc.Current.Languages.Keys.ToList())
                {
                    Loc.Current.SetLanguage(language);
                    foreach (var context in AppCommands.MenuContexts.Values.Prepend(""))
                    {
                        foreach (var other in AppCommands.MenuContexts.Values) window.Registry.SetContext(other, other == context);
                        Dispatcher.UIThread.RunJobs();
                        Dispatcher.UIThread.RunJobs();
                        var inBar = window.MainMenu.Items.OfType<MenuItem>().Where(m => m.Name != "MenuOverflow").ToList();
                        var more = window.MainMenu.Items.OfType<MenuItem>().SingleOrDefault(m => m.Name == "MenuOverflow");
                        var where = $"{language} {context}";
                        Assert.True(window.MainMenu.Bounds.Width > 0, where);
                        var shown = inBar.Where(m => m.IsVisible).Sum(m => m.Bounds.Width) + (more?.Bounds.Width ?? 0);
                        Assert.True(shown <= window.MainMenu.Bounds.Width + 0.5, $"{where}: {shown} > {window.MainMenu.Bounds.Width}");
                        var parked = more?.Items.OfType<MenuItem>().ToList() ?? [];
                        Assert.Equal(window.Bar.Items.Count, inBar.Count + parked.Count);
                        if (parked.Count > 0) Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(more!)), where);
                    }
                }

                // Every context menu showing at once is wider than the Spanish bar: the extra menus are parked.
                Loc.Current.SetLanguage("es");
                foreach (var each in AppCommands.MenuContexts.Values) window.Registry.SetContext(each, true);
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                Assert.NotEmpty(window.MainMenu.Items.OfType<MenuItem>().Single(m => m.Name == "MenuOverflow").Items);

                // Wide again: nothing is parked.
                window.Width = 1600;
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                Assert.DoesNotContain(window.MainMenu.Items.OfType<MenuItem>(), m => m.Name == "MenuOverflow");
            }
            finally { window.Close(); }
        });
    }
}
