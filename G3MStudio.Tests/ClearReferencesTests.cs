using G3MLib.DataFile;
using G3MLib.DataFile.References;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ClearReferencesTests
{
    [Fact]
    public async Task ASpriteUsedByObjectsIsClearedThenDeletedAndUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var index = ReferenceIndex.Build(data, TestContext.Current.CancellationToken);
        var shapes = string.Join(" | ", data.Sprites.Select(index.FindUsages).Where(u => u.Count > 0).Take(6).Select(u => string.Join(",", u.Select(x => $"{x.Holder.GetType().Name}.{x.Member}@{x.InstructionOffset}"))));
        var sprite = data.Sprites.FirstOrDefault(s => index.FindUsages(s) is { Count: > 0 } uses && uses.All(u => u.InstructionOffset < 0) && uses.Any(u => u.Holder is G3MLib.DataFile.Models.GameMakerGameObject));
        Assert.True(sprite is not null, shapes);
        var users = data.GameObjects.Where(o => ReferenceEquals(o.Sprite, sprite)).ToList();
        Assert.NotEmpty(users);
        var name = sprite.Name.Content!;
        var sprites = data.Sprites.Count;

        var command = new GroupCommand([new ClearReferencesCommand("sprites", name), new ResourceListCommand("sprites", ResourceListCommand.Delete, name)]);
        command.Apply(data);
        Assert.All(users, o => Assert.Null(o.Sprite));
        Assert.Equal(sprites - 1, data.Sprites.Count);

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken); // the file still writes
        command.Revert(data);
        Assert.All(users, o => Assert.Same(sprite, o.Sprite));
        Assert.Equal(sprites, data.Sprites.Count);
    }

    [Fact]
    public async Task AResourceUsedInCodeIsRefusedWithThePlaceNamed()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var index = ReferenceIndex.Build(data, TestContext.Current.CancellationToken);
        var usedInCode = data.Scripts.FirstOrDefault(s => index.FindUsages(s).Any(u => u.InstructionOffset >= 0));
        Assert.SkipWhen(usedInCode is null, "No script of the smallest corpus game is used from code.");
        var error = Assert.ThrowsAny<InvalidOperationException>(() => new ClearReferencesCommand("scripts", usedInCode.Name.Content!).Apply(data));
        Assert.Contains("used in code", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeletingAResourceInUseShowsWhereItIsUsedClearsItAndUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var index = ReferenceIndex.Build(workspace.Data, TestContext.Current.CancellationToken);
                var sprite = workspace.Data.Sprites.First(x => index.FindUsages(x) is { Count: > 0 } uses && uses.All(u => u.InstructionOffset < 0) && uses.Any(u => u.Holder is G3MLib.DataFile.Models.GameMakerGameObject));
                var users = workspace.Data.GameObjects.Where(o => ReferenceEquals(o.Sprite, sprite)).ToList();
                var name = sprite.Name.Content!;
                string? shown = null;
                window.Confirmer = (title, message) =>
                {
                    shown = title + "|" + message;
                    return Task.FromResult(true);
                };
                window.Explorer.Select(ResourceTypes.KeyOf("sprites", name));
                Assert.True(window.Registry.Execute("edit.delete"));
                for (var i = 0; i < 200 && workspace.Data.Sprites.Contains(sprite); i++)
                {
                    await Task.Delay(50);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                Assert.DoesNotContain(sprite, workspace.Data.Sprites);
                Assert.NotNull(shown);
                Assert.Contains(users[0].Name.Content!, shown, StringComparison.Ordinal); // the warning names a place that uses it
                Assert.All(users, o => Assert.Null(o.Sprite));
                workspace.History.Undo();
                Assert.Contains(sprite, workspace.Data.Sprites);
                Assert.All(users, o => Assert.Same(sprite, o.Sprite));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
