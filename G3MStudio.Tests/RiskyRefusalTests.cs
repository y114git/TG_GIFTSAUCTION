using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using ImageMagick;

namespace G3MStudio.Tests;

/// <summary>Each refusal the owner can overrule: refused first, accepted with the force flag, the file still writes and reads, and undo puts everything back.</summary>
public sealed class RiskyRefusalTests
{
    private static async Task AssertFileRoundTripsAsync(GameMakerData data)
    {
        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(again);
    }

    private static GameMakerData WithSprite()
    {
        var data = TestData.Create(1, true);
        TestData.AddPage(data);
        new EditHistory(data).Execute(ResourceTemplates.New(data, "sprites", "spr_a"));
        return data;
    }

    [Fact]
    public async Task TheLastFrameOfASpriteIsRefusedThenRemovedOnRequestAndComesBack()
    {
        using var data = WithSprite();
        var sprite = data.Sprites.First(s => s.Name.Content == "spr_a");
        while (sprite.Textures.Count > 1) new FrameListCommand("spr_a", FrameListCommand.Remove, 0).Apply(data);
        Assert.Throws<RiskyRefusalException>(() => new FrameListCommand("spr_a", FrameListCommand.Remove, 0).Apply(data));

        var forced = (FrameListCommand)EditCommand.Create(FrameListCommand.Name, new FrameListCommand("spr_a", FrameListCommand.Remove, 0, true).Args); // the journal keeps the choice
        forced.Apply(data);
        Assert.Empty(sprite.Textures);
        await AssertFileRoundTripsAsync(data);
        forced.Revert(data);
        Assert.Single(sprite.Textures);
    }

    [Fact]
    public async Task TheLastPointOfAPathIsRefusedThenRemovedOnRequestAndComesBack()
    {
        using var data = TestData.Create(0);
        new EditHistory(data).Execute(ResourceTemplates.New(data, "paths", "pth_a"));
        var path = data.Paths.First(p => p.Name.Content == "pth_a");
        if (path.Points.Count == 0) new PathPointsCommand("pth_a", PathPointsCommand.Add, -1, 1, 2).Apply(data);
        while (path.Points.Count > 1) new PathPointsCommand("pth_a", PathPointsCommand.Delete, 0).Apply(data);
        Assert.Throws<RiskyRefusalException>(() => new PathPointsCommand("pth_a", PathPointsCommand.Delete, 0).Apply(data));

        var forced = (PathPointsCommand)EditCommand.Create(PathPointsCommand.Name, new PathPointsCommand("pth_a", PathPointsCommand.Delete, 0, force: true).Args);
        forced.Apply(data);
        Assert.Empty(path.Points);
        await AssertFileRoundTripsAsync(data);
        forced.Revert(data);
        Assert.Single(path.Points);
    }

    [Fact]
    public async Task AudioOverTheLimitOrOfAnotherKindIsRefusedThenPutInOnRequestAndComesBack()
    {
        using var data = TestData.Create(0);
        new EditHistory(data).Execute(ResourceTemplates.New(data, "sounds", "snd_a"));
        var sound = data.Sounds.First(s => s.Name.Content == "snd_a");
        var before = sound.AudioFile!.Data;
        var large = new byte[ReplaceAudioCommand.MaxBytes + 1];
        "RIFF"u8.CopyTo(large);
        Assert.Throws<RiskyRefusalException>(() => new ReplaceAudioCommand("snd_a", large).Apply(data));
        var forcedLarge = new ReplaceAudioCommand("snd_a", large, force: true);
        forcedLarge.Apply(data);
        Assert.Equal(large.Length, sound.AudioFile!.Data.Length);
        forcedLarge.Revert(data);
        Assert.Equal(before, sound.AudioFile.Data);

        byte[] ogg = [.. "OggS"u8, 0, 0, 0, 0, 0, 0];
        Assert.Throws<RiskyRefusalException>(() => new ReplaceAudioCommand("snd_a", ogg).Apply(data));
        var forcedKind = (ReplaceAudioCommand)EditCommand.Create(ReplaceAudioCommand.Name, new ReplaceAudioCommand("snd_a", ogg, force: true).Args);
        forcedKind.Apply(data);
        Assert.Equal(ogg, sound.AudioFile.Data);
        await AssertFileRoundTripsAsync(data);
        forcedKind.Revert(data);
        Assert.Equal(before, sound.AudioFile.Data);
        Assert.Throws<InvalidOperationException>(() => new ReplaceAudioCommand("snd_a", [], force: true).Apply(data)); // empty stays refused
    }

    [Fact]
    public async Task APictureOfAnotherSizeIsRefusedThenReplacesThePageOnRequestAndComesBack()
    {
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        var name = data.EmbeddedTextures[0].Name.Content!;
        var original = ReplacePageCommand.ReadPng(data, name);
        using var small = new MagickImage(MagickColors.Red, 64, 64);
        var picture = small.ToByteArray(MagickFormat.Png);
        Assert.Throws<RiskyRefusalException>(() => new ReplacePageCommand(name, picture).Apply(data));

        var forced = (ReplacePageCommand)EditCommand.Create(ReplacePageCommand.Name, new ReplacePageCommand(name, picture, true).Args);
        forced.Apply(data);
        using (var now = new MagickImage(ReplacePageCommand.ReadPng(data, name))) Assert.Equal(64u, now.Width);
        await AssertFileRoundTripsAsync(data);
        forced.Revert(data);
        Assert.Equal(original.Length, ReplacePageCommand.ReadPng(data, name).Length);
    }

    [Fact]
    public async Task ADeleteAimedAtByCollisionEventsIsRefusedThenRemovesThoseEventsOnRequestAndComesBack()
    {
        using var data = TestData.Create(2, true);
        new ObjectEventCommand("obj_0", ObjectEventCommand.Add, (uint)EventType.Collision, 1).Apply(data); // obj_1 is entry 1
        var events = data.GameObjects[0].Events[(int)EventType.Collision];
        var code = data.Code.Count;
        Assert.Throws<RiskyRefusalException>(() => new ResourceListCommand("objects", ResourceListCommand.Delete, "obj_1").Apply(data));
        Assert.Single(events);

        var forced = (ResourceListCommand)EditCommand.Create(ResourceListCommand.Name, new ResourceListCommand("objects", ResourceListCommand.Delete, "obj_1", true).Args);
        forced.Apply(data);
        Assert.Empty(events);
        Assert.Equal(code - 1, data.Code.Count);
        Assert.Single(data.GameObjects);
        await AssertFileRoundTripsAsync(data);
        forced.Revert(data);
        Assert.Single(events);
        Assert.Equal(1u, events[0].EventSubtype);
        Assert.Equal(code, data.Code.Count);
        Assert.Equal(2, data.GameObjects.Count);
    }

    [Fact]
    public void ANameThatExistsIsRefusedThenUsedTwiceOnRequestAndUndoes()
    {
        using var data = TestData.Create(0);
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "paths", "pth_a"));
        var refusal = Assert.Throws<UnusualNameException>(() => ResourceTemplates.New(data, "paths", "pth_a").Apply(data));
        Assert.True(refusal.Taken);
        history.Execute(ResourceTemplates.New(data, "paths", "pth_a", true));
        Assert.Equal(2, data.Paths.Count(p => p.Name.Content == "pth_a"));
        history.Undo();
        Assert.Single(data.Paths, p => p.Name.Content == "pth_a");
    }

    [Fact]
    public void AFileOfAnUnknownTypeIsRefusedThenReadAsAScriptOnRequestAndABinaryFileStaysRefused()
    {
        using var dir = new TempDir();
        using var data = TestData.Create(0, true);
        var text = dir.File("notes.txt");
        File.WriteAllText(text, "show_debug_message(1);");
        Assert.Throws<RiskyRefusalException>(() => FileImport.Build(data, text, out _, out _));

        var history = new EditHistory(data);
        history.Execute(FileImport.Build(data, text, out var type, out var name, asText: true));
        Assert.Equal("scripts", type);
        Assert.Contains(data.Scripts, s => s.Name.Content == name);
        history.Undo();
        Assert.DoesNotContain(data.Scripts, s => s.Name.Content == name);

        var binary = dir.File("blob.bin");
        File.WriteAllBytes(binary, [1, 0, 2, 0]);
        Assert.Throws<InvalidDataException>(() => FileImport.Build(data, binary, out _, out _, asText: true));
    }

    [Fact]
    public async Task ABackgroundEditWaitsWhileTheWorkspaceSavesInsteadOfBeingRefused()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
        workspace.HoldEdits();
        var running = workspace.EditInBackground(ResourceTemplates.New(workspace.Data, "paths", "pth_wait"));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.False(running.IsCompleted);
        workspace.ReleaseEdits();
        await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Contains(workspace.Data.Paths, p => p.Name.Content == "pth_wait");
    }

    [Fact]
    public async Task UnsavedChangesCanBeKeptThroughABulkChangeOnRequestButItCannotBeUndone()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
        workspace.Edit(ResourceTemplates.New(workspace.Data, "paths", "pth_unsaved"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ApplyBulkAsync(() => { }));
        await workspace.ApplyBulkAsync(() => { }, allowUnsaved: true);
        Assert.Equal(1, workspace.History.BulkChanges);
        Assert.Contains(workspace.Data.Paths, p => p.Name.Content == "pth_unsaved");
        Assert.False(workspace.History.CanUndo);
    }

    [Fact]
    public async Task TheWindowAsksThenGoesOnOrStopsAccordingToTheAnswerAndTheTwoSettings()
    {
        using var dir = new TempDir();
        using (var data = WithSprite()) TestData.Write(dir.File("data.win"), data);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(dir.File("data.win"));
                var workspace = window.Workspaces.Current!;
                var sprite = workspace.Data.Sprites.First(s => s.Name.Content == "spr_a");
                while (sprite.Textures.Count > 1) workspace.Edit(new FrameListCommand("spr_a", FrameListCommand.Remove, 0));
                var asked = 0;
                string? shown = null;
                window.Confirmer = (title, message) =>
                {
                    asked++;
                    shown = title + "|" + message;
                    return Task.FromResult(false);
                };
                bool Remove() => window.EditOrForceAsync(workspace, force => new FrameListCommand("spr_a", FrameListCommand.Remove, 0, force), "spr_a").GetAwaiter().GetResult();

                Assert.False(Remove()); // a no: nothing changes
                Assert.Equal(1, asked);
                Assert.Contains("spr_a", shown, StringComparison.Ordinal);
                Assert.Single(sprite.Textures);

                window.Workspaces.Settings.Update(x => x with { AskRiskyActions = false });
                Assert.False(Remove()); // set to refuse: no question, a plain error
                Assert.Equal(1, asked);
                Assert.Contains(window.Notifications.Active, n => n.Level == NotificationLevel.Error);

                window.Workspaces.Settings.Update(x => x with { AskRiskyActions = true, AllowUnsafeSilently = true });
                Assert.True(Remove()); // allowed silently: no question
                Assert.Equal(1, asked);
                Assert.Empty(sprite.Textures);
                workspace.History.Undo();
                Assert.Single(sprite.Textures);

                window.Workspaces.Settings.Update(x => x with { AllowUnsafeSilently = false });
                window.Confirmer = (_, _) => Task.FromResult(true);
                Assert.True(Remove()); // a yes
                Assert.Empty(sprite.Textures);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheNewResourceDialogWarnsAboutAnUnusualNameOnceThenCreatesItAndStopsAtAFileNameThatCannotBeUsed()
    {
        using var dir = new TempDir();
        TestData.WriteSample(dir, objects: 1);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(dir.File("data.win"));
                var workspace = window.Workspaces.Current!;
                var first = window.CreateResource(workspace, "paths", "my path");
                Assert.NotNull(first); // the warning stays in the dialog
                Assert.DoesNotContain(workspace.Data.Paths, p => p.Name.Content == "my path");
                Assert.Null(window.CreateResource(workspace, "paths", "my path")); // the same name again goes through
                Assert.Contains(workspace.Data.Paths, p => p.Name.Content == "my path");
                Assert.NotNull(window.CreateResource(workspace, "paths", "a/b"));
                Assert.NotNull(window.CreateResource(workspace, "paths", "a/b")); // not a file name: refused again, never accepted
                Assert.DoesNotContain(workspace.Data.Paths, p => p.Name.Content == "a/b");
                workspace.History.Undo();
                Assert.DoesNotContain(workspace.Data.Paths, p => p.Name.Content == "my path");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task DeletingAResourceUsedInCodeAsksThenDeletesOnYesAndKeepsItOnNo()
    {
        using var dir = new TempDir();
        using (var data = TestData.Create(2, true))
        {
            new EditHistory(data).Execute(new EditCodeCommand("gml_Script_probe", "return scr_1();"));
            TestData.Write(dir.File("data.win"), data);
        }

        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(dir.File("data.win"));
                var workspace = window.Workspaces.Current!;
                string? shown = null;
                window.Confirmer = (title, message) =>
                {
                    shown = title + "|" + message;
                    return Task.FromResult(false);
                };
                window.Explorer.Select(ResourceTypes.KeyOf("scripts", "scr_1"));
                Assert.True(window.Registry.Execute("edit.delete"));
                await Wait(() => shown is not null);
                Assert.Contains(workspace.Data.Scripts, s => s.Name.Content == "scr_1"); // a no keeps it
                Assert.Contains("scr_1", shown, StringComparison.Ordinal);

                window.Confirmer = (_, _) => Task.FromResult(true);
                Assert.True(window.Registry.Execute("edit.delete"));
                await Wait(() => workspace.Data.Scripts.All(s => s.Name.Content != "scr_1"));
                Assert.DoesNotContain(workspace.Data.Scripts, s => s.Name.Content == "scr_1");
                workspace.History.Undo();
                Assert.Contains(workspace.Data.Scripts, s => s.Name.Content == "scr_1");
            }
            finally { window.Close(); }
        });
    }

    private static async Task Wait(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            await Task.Delay(50);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }
}
