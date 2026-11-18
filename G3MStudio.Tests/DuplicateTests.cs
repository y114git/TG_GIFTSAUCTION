using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class DuplicateTests
{
    [Fact]
    public void AnUnusualCopyNameNeedsPermissionAndAnUnsafeOneIsAlwaysRefused()
    {
        ResourceDuplicate.CheckCopyName("obj_copy", false);
        Assert.Throws<UnusualNameException>(() => ResourceDuplicate.CheckCopyName("my copy", false));
        ResourceDuplicate.CheckCopyName("my copy", true);
        Assert.Throws<InvalidOperationException>(() => ResourceDuplicate.CheckCopyName("a/b", true));
    }

    [Fact]
    public async Task ATakenCopyNameIsRefusedBeforeTheCopyBetweenFilesStarts()
    {
        using var data = await GameMakerIO.ReadAsync(Corpus.Files(Corpus.Folder).First(), cancellationToken: TestContext.Current.CancellationToken);
        var taken = data.GameObjects[0].Name.Content!;
        Assert.Throws<InvalidOperationException>(() => ResourceDuplicate.CheckCopyFree(data, "objects", taken));
        ResourceDuplicate.CheckCopyFree(data, "objects", "zz_free_name");
    }

    [Fact]
    public async Task AnObjectIsCopiedWithItsPropertiesEventsAndCodeAndTheCopyUndoes()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var source = data.GameObjects.First(o => o.Events.Sum(e => e.Count) is > 1 and < 5 && o.Events.SelectMany(e => e).All(e => e.Actions.Count == 1 && e.Actions[0].CodeId is not null));
        var name = source.Name.Content!;
        var objects = data.GameObjects.Count;
        var code = data.Code.Count;
        var command = ResourceDuplicate.Build(data, "objects", name, "zz_copy");
        command.Apply(data);
        var copy = data.GameObjects.Single(o => o.Name.Content == "zz_copy");
        Assert.Equal(objects + 1, data.GameObjects.Count);
        Assert.Equal(source.Depth, copy.Depth);
        Assert.Equal(source.Persistent, copy.Persistent);
        Assert.Equal(source.Visible, copy.Visible);
        Assert.Same(source.Sprite, copy.Sprite);
        Assert.Equal(source.Events.Select(e => e.Count), copy.Events.Select(e => e.Count));
        Assert.Equal(code + source.Events.Sum(e => e.Count), data.Code.Count);
        var kind = Enumerable.Range(0, source.Events.Count).First(k => source.Events[k].Count > 0);
        var first = source.Events[kind][0];
        Assert.Equal(CodeText.Decompile(data, first.Actions[0].CodeId!.Name.Content!), CodeText.Decompile(data, ObjectEventCommand.CodeName("zz_copy", (uint)kind, first.EventSubtype)));

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(source.Events.Select(e => e.Count), again.GameObjects.Single(o => o.Name.Content == "zz_copy").Events.Select(e => e.Count));

        command.Revert(data);
        Assert.Equal(objects, data.GameObjects.Count);
        Assert.Equal(code, data.Code.Count);
        Assert.DoesNotContain(data.GameObjects, o => o.Name.Content == "zz_copy");
    }

    [Fact]
    public async Task AScriptIsCopiedWithItsCode()
    {
        var folder = Corpus.Folder;
        GameMakerData? found = null;
        G3MLib.DataFile.Models.GameMakerScript? source = null;
        foreach (var file in Corpus.Files(folder))
        {
            var candidate = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            source = candidate.Scripts.FirstOrDefault(s => s.Code is { ParentEntry: null } && s.Code.Name.Content is { Length: > 0 } c && CodeText.Decompile(candidate, c) is { Length: > 10 and < 600 } text && !text.Contains("function", StringComparison.Ordinal));
            if (source is not null)
            {
                found = candidate;
                break;
            }

            candidate.Dispose();
        }

        Assert.False(found is null || source is null, "no corpus file has a short plain script");
        using var data = found;
        var scripts = data!.Scripts.Count;
        var command = ResourceDuplicate.Build(data, "scripts", source!.Name.Content!, "zz_script_copy");
        command.Apply(data);
        Assert.Equal(scripts + 1, data.Scripts.Count);
        Assert.Equal(CodeText.Decompile(data, source!.Code!.Name.Content!), CodeText.Decompile(data, "gml_Script_zz_script_copy"));
        command.Revert(data);
        Assert.Equal(scripts, data.Scripts.Count);
    }

    [Fact]
    public async Task DuplicateFromTheMenuAsksForANameRefusesATakenOneAndOpensTheCopy()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var source = workspace.Data.GameObjects.First(o => o.Events.Sum(e => e.Count) is > 0 and < 4 && o.Events.SelectMany(e => e).All(e => e.Actions.Count == 1 && e.Actions[0].CodeId is not null)).Name.Content!;
                window.Explorer.Select(ResourceTypes.KeyOf("objects", source));
                Assert.True(window.Registry.Execute("edit.duplicate"));
                var dialog = window.OwnedWindows.OfType<RenameWindow>().Single();
                var box = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).OfType<Avalonia.Controls.TextBox>().Single(t => t.Name == "NewName");
                var ok = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).OfType<Avalonia.Controls.Button>().Single(b => b.Name == "Rename");
                box.Text = source; // taken
                ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Single(window.OwnedWindows.OfType<RenameWindow>()); // still open with the reason
                box.Text = "obj_zz_copy";
                ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Contains(workspace.Data.GameObjects, o => o.Name.Content == "obj_zz_copy");
                Assert.Contains(window.Docking.Tabs.Tabs, t => t.Key == ResourceTypes.KeyOf("objects", "obj_zz_copy"));
                workspace.History.Undo();
                Assert.DoesNotContain(workspace.Data.GameObjects, o => o.Name.Content == "obj_zz_copy");
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
            }
        });
    }
}
