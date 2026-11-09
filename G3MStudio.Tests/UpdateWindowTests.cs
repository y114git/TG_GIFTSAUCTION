using G3MStudio.Core.Updates;

namespace G3MStudio.Tests;

public sealed class UpdateWindowTests
{
    [Fact]
    public async Task ANewerReleaseIsOfferedAndInstalledOnlyAfterTheUserAgrees()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var installed = false;
                window.UpdateCheck = _ => Task.FromResult<UpdateOffer?>(null);
                await window.CheckForUpdatesAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("newest version", StringComparison.Ordinal));

                window.UpdateCheck = _ => Task.FromResult<UpdateOffer?>(new UpdateOffer(new SemVer(99, 0, 0, ""), "x.zip", new Uri("https://example.invalid/x.zip"), 1, "00"));
                window.UpdateRunner = _ =>
                {
                    installed = true;
                    return Task.CompletedTask;
                };
                window.Confirmer = (_, _) => Task.FromResult(false);
                await window.CheckForUpdatesAsync();
                Assert.False(installed);

                window.Confirmer = (_, _) => Task.FromResult(true);
                await window.CheckForUpdatesAsync();
                Assert.True(installed);
            }
            finally { window.Close(); }
        });
    }
}
