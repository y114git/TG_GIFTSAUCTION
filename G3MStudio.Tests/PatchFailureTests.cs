using System.Reflection;
using G3MStudio.Core.Notifications;

namespace G3MStudio.Tests;

public sealed class PatchFailureTests
{
    [Fact]
    public async Task APatchThatFailsWithAnyErrorTellsTheUserInsteadOfFaultingSilently()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var run = typeof(MainWindow).GetMethod("RunPatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Func<CancellationToken, Task<(bool, string?)>> failing = _ => throw new KeyNotFoundException("no such page");
                await (Task)run.Invoke(window, ["textures.repacking", failing, null])!;
                Assert.Contains(window.Notifications.Active, n => n.Level == NotificationLevel.Error && n.Text.Contains("no such page", StringComparison.Ordinal));
            }
            finally { window.Close(); }
        });
    }
}
