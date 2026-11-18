using Avalonia.Controls;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>The dialog sizing hook keeps the store of the last main window; a leaked window of an earlier run must not scale the windows of a later one.</summary>
public sealed class DialogSizingIsolationTests
{
    public static object? Leaked { get; set; }

    [Fact]
    public async Task AWindowOfALaterRunIsNotScaledByTheSettingsOfALeakedMainWindow()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            window.Workspaces.Settings.Update(s => s with { UiScale = 175 });
            Leaked = window;
        });
        await HeadlessApp.Run(() =>
        {
            var window = new Window { Content = new Border() };
            window.Show();
            try { Assert.Empty(window.GetVisualDescendants().OfType<LayoutTransformControl>()); }
            finally { window.Close(); }
        });
        Leaked = null;
    }
}
