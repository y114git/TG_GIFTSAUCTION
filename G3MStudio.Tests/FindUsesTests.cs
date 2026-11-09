using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class FindUsesTests
{
    [Fact]
    public async Task AnObjectListsItsSpriteAndTheListOpensInTheUsagesPanel()
    {
        var corpus = Corpus.Folder;
        string? file = null;
        foreach (var candidate in Corpus.Files(corpus))
        {
            using var probe = await G3MLib.DataFile.GameMakerIO.ReadAsync(candidate, cancellationToken: TestContext.Current.CancellationToken);
            if (probe.GameObjects.Any(o => o.Sprite is not null)) { file = candidate; break; }
        }

        Assert.False(file is null, "no game in the corpus has an object with a sprite");
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = 1100;
                window.Height = 700;
                window.Show();
                await window.OpenAsync(file!);
                var workspace = window.Workspaces.Current!;
                var obj = workspace.Data.GameObjects.First(o => o.Sprite is not null);
                var name = obj.Name.Content!;
                var hits = await UsageSearch.FindUsesAsync(workspace, "objects", name, TestContext.Current.CancellationToken);
                Assert.Contains(hits, h => h.OwnerType == "sprites" && h.OwnerName == obj.Sprite!.Name.Content);
                Assert.DoesNotContain(hits, h => h.OwnerName == name);

                window.Explorer.Select(ResourceTypes.KeyOf("objects", name));
                window.FindUses();
                var list = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.Name == "UsagesList");
                for (var i = 0; i < 100 && list?.ItemsSource is null; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                    list = window.GetVisualDescendants().OfType<ListBox>().FirstOrDefault(l => l.Name == "UsagesList");
                }

                Assert.NotNull(list?.ItemsSource);
                if (Environment.GetEnvironmentVariable("G3MSTUDIO_SHOTS") is { Length: > 0 } shots) ScreenshotTests.Shoot(window, Path.Combine(shots, "24-find-uses.png"));
            }
            finally { window.Close(); }
        });
    }
}
