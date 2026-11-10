using Avalonia.VisualTree;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ResourceListTests
{
    [Fact]
    public async Task NewResourcesSurviveAWriteAndARead_AndDeletesUndo()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var counts = ResourceTypes.Counts(data);
        var added = new List<ResourceListCommand>();
        foreach (var type in ResourceListCommand.Creatable)
        {
            var command = EditCommand.Create(ResourceListCommand.Name, new ResourceListCommand(type, ResourceListCommand.New, "zz_new_" + type).Args);
            command.Apply(data);
            added.Add((ResourceListCommand)command);
        }

        foreach (var type in ResourceListCommand.Creatable) Assert.Equal(counts[type] + 1, ResourceTypes.Counts(data)[type]);
        Assert.ThrowsAny<InvalidOperationException>(() => new ResourceListCommand("objects", ResourceListCommand.New, "zz_new_objects").Apply(data));
        Assert.ThrowsAny<InvalidOperationException>(() => new ResourceListCommand("objects", ResourceListCommand.New, "9bad name").Apply(data));

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        foreach (var type in ResourceListCommand.Creatable) Assert.NotNull(ResourceTypes.Find(again, ResourceTypes.KeyOf(type, "zz_new_" + type)));
        Assert.NotNull(again.Scripts.First(s => s.Name.Content == "zz_new_scripts").Code);

        var delete = new ResourceListCommand("scripts", ResourceListCommand.Delete, "zz_new_scripts");
        var codeCount = data.Code.Count;
        delete.Apply(data);
        Assert.Equal(counts["scripts"], ResourceTypes.Counts(data)["scripts"]);
        Assert.Equal(codeCount - 1, data.Code.Count);
        delete.Revert(data);
        Assert.Equal(counts["scripts"] + 1, ResourceTypes.Counts(data)["scripts"]);
        Assert.Equal(codeCount, data.Code.Count);

        // A collision event names its partner by position: deleting an object moves the events after it up by one, and one that aims at it blocks the delete.
        var events = data.GameObjects.SelectMany(o => o.Events[(int)G3MLib.DataFile.Models.EventType.Collision]).ToList();
        var partner = events.FirstOrDefault()?.EventSubtype;
        if (partner is { } p && p < data.GameObjects.Count)
            Assert.ThrowsAny<InvalidOperationException>(() => new ResourceListCommand("objects", ResourceListCommand.Delete, data.GameObjects[(int)p].Name.Content!).Apply(data));
        var removable = Enumerable.Range(0, data.GameObjects.Count).FirstOrDefault(i => events.All(e => e.EventSubtype != i) && events.Any(e => e.EventSubtype > i), -1);
        if (removable >= 0)
        {
            var later = events.Where(e => e.EventSubtype > removable).ToList();
            var was = later.Select(e => e.EventSubtype).ToList();
            var gone = new ResourceListCommand("objects", ResourceListCommand.Delete, data.GameObjects[removable].Name.Content!);
            gone.Apply(data);
            Assert.Equal(was.Select(x => x - 1), later.Select(e => e.EventSubtype));
            gone.Revert(data);
            Assert.Equal(was, later.Select(e => e.EventSubtype));
        }

        for (var i = added.Count - 1; i >= 0; i--) added[i].Revert(data);
        Assert.Equal(counts["objects"], ResourceTypes.Counts(data)["objects"]);
        Assert.Equal(counts["scripts"], ResourceTypes.Counts(data)["scripts"]);
    }

    [Fact]
    public async Task NewObjectFromTheMenuOpensItAndDeleteAsksAndUndoes()
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
                var before = workspace.Data.GameObjects.Count;
                Assert.True(window.Registry.Execute("file.new.objects"));
                var dialog = window.OwnedWindows.OfType<RenameWindow>().Single();
                var name = dialog.GetVisualDescendants().OfType<Avalonia.Controls.TextBox>().Single(t => t.Name == "NewName");
                name.Text = "9 bad";
                name.Text = "obj_made_here";
                dialog.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(b => b.Name == "Rename").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal(before + 1, workspace.Data.GameObjects.Count);
                Assert.Contains(window.Docking.Tabs.Tabs, t => t.Key == ResourceTypes.KeyOf("objects", "obj_made_here"));

                window.Confirmer = (_, _) => Task.FromResult(true);
                window.Explorer.Select(ResourceTypes.KeyOf("objects", "obj_made_here"));
                Assert.True(window.Registry.Execute("edit.delete"));
                for (var i = 0; i < 100 && workspace.Data.GameObjects.Count != before; i++)
                {
                    await Task.Delay(50);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                Assert.Equal(before, workspace.Data.GameObjects.Count);
                Assert.DoesNotContain(window.Docking.Tabs.Tabs, t => t.Key == ResourceTypes.KeyOf("objects", "obj_made_here"));
                workspace.History.Undo();
                Assert.Equal(before + 1, workspace.Data.GameObjects.Count);
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public async Task NewFontSequenceCurveParticleSystemAndExtensionOpenInTheirTabs()
    {
        var file = Corpus.Files(Corpus.Folder).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                foreach (var type in ResourceListCommand.Creatable.Skip(8))
                {
                    Assert.Null(window.CreateResource(workspace, type, "zz_new_" + type));
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Assert.Contains(window.Docking.Tabs.Tabs, t => t.Key == ResourceTypes.KeyOf(type, "zz_new_" + type));
                }

                workspace.Undo();
                Assert.Null(ResourceTypes.Find(workspace.Data, ResourceTypes.KeyOf("extensions", "zz_new_extensions")));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task NewSoundAndNewRoomComeFromTheirTemplatesAndUndo()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var audio = data.EmbeddedAudio.Count;
        var sound = ResourceTemplates.New(data, "sounds", "snd_zz_new");
        sound.Apply(data);
        var made = data.Sounds.Single(x => x.Name.Content == "snd_zz_new");
        Assert.Equal(audio + 1, data.EmbeddedAudio.Count);
        Assert.Equal("wav", SoundAudio.Format(made.AudioFile!.Data));
        Assert.Equal(1.0, WavEdit.Read(made.AudioFile.Data).Seconds, 2);

        var layered = data.Rooms.Any(r => r.Layers?.Count > 0);
        var room = ResourceTemplates.New(data, "rooms", "rm_zz_new");
        room.Apply(data);
        var newRoom = data.Rooms.Single(r => r.Name.Content == "rm_zz_new");
        Assert.Equal((1024u, 768u), (newRoom.Width, newRoom.Height));
        if (layered) Assert.Single(newRoom.Layers!, l => l.LayerName.Content == "Instances");

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(again.Rooms.Single(r => r.Name.Content == "rm_zz_new"));
        Assert.Equal(audio + 1, again.EmbeddedAudio.Count);

        room.Revert(data);
        sound.Revert(data);
        Assert.Equal(audio, data.EmbeddedAudio.Count);
        Assert.DoesNotContain(data.Rooms, r => r.Name.Content == "rm_zz_new");
    }
}
