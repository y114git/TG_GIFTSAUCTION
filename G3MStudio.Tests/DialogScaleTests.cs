using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>A dialog keeps the interface scale after its entrance animation: the animation must not wipe the scale transform of the sizing host.</summary>
public sealed class DialogScaleTests
{
    [Fact]
    public async Task TheEntranceAnimationOfADialogLeavesItsInterfaceScaleInPlace()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            Motion.TimedInHeadless = true;
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Workspaces.Settings.Update(s => s with { UiScale = 95 });
                var dialog = new RenameWindow("spr_a", _ => null);
                _ = dialog.AskAsync(window);
                for (var i = 0; i < 60 && Motion.Active > 0; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10);
                }

                var host = dialog.GetVisualDescendants().OfType<LayoutTransformControl>().First();
                var transform = Assert.IsAssignableFrom<Avalonia.Media.Transform>(host.Child!.RenderTransform);
                Assert.Equal(0.95, transform.Value.M11, 2);
                dialog.Close();
            }
            finally
            {
                Motion.TimedInHeadless = false;
                window.Close();
            }
        });
    }
}
