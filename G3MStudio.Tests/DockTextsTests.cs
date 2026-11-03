using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Docking;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class DockTextsTests
{
    [Fact]
    public async Task TheDockingMenusFollowTheLanguage()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Docking.OpenTab("resource", "sprites:nothing", false);
                await Task.Delay(200);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Loc.Current.SetLanguage("ru");
                Assert.True(Application.Current!.TryGetResource("DocumentTabStripItemCloseAllTabsString", Application.Current.ActualThemeVariant, out var text));
                Assert.Equal("Закрыть все вкладки", text);
                var strip = window.GetVisualDescendants().OfType<Dock.Avalonia.Controls.DocumentTabStripItem>().FirstOrDefault();
                if (strip?.ContextMenu is { } menu)
                {
                    var headers = menu.Items.OfType<MenuItem>().Select(i => i.Header?.ToString() ?? "").ToList();
                    Assert.Contains("Закрыть все вкладки", headers);
                }

                Loc.Current.SetLanguage("en");
                Application.Current.TryGetResource("DocumentTabStripItemCloseAllTabsString", Application.Current.ActualThemeVariant, out text);
                Assert.Equal("Close all tabs", text);
                Assert.Equal(24, DockTexts.Keys.Length);
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                window.Close();
            }
        });
    }
}
