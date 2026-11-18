using Avalonia.Controls;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
/// <summary>Sounds whose audio is outside the data file: found beside it, read from an audio group file or a streamed file, and played by the same player. The files are generated, no game data.</summary>
public sealed class ExternalAudioTests
{
    private static readonly byte[] Streamed = AudioCodec.Encode(new PcmAudio([.. Enumerable.Repeat(0.25f, 8000)], 1, 8000), "wav");

    // An audiogroupN.dat made by hand: FORM, one AUDO chunk, a pointer table and the entries (size, bytes, padded to four).
    private static void WriteGroup(string path, params byte[][] entries)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var chunk = 4 + (4 * entries.Length) + entries.Sum(e => 4 + ((e.Length + 3) & ~3));
        writer.Write("FORM"u8);
        writer.Write(8 + chunk);
        writer.Write("AUDO"u8);
        writer.Write(chunk);
        writer.Write(entries.Length);
        var at = 16 + 4 + (4 * entries.Length);
        foreach (var entry in entries)
        {
            writer.Write(at);
            at += 4 + ((entry.Length + 3) & ~3);
        }

        foreach (var entry in entries)
        {
            writer.Write(entry.Length);
            writer.Write(entry);
            writer.Write(new byte[((entry.Length + 3) & ~3) - entry.Length]);
        }

        File.WriteAllBytes(path, stream.ToArray());
    }

    private sealed record Fixture(WorkspaceService Service, Workspace Workspace, string Folder, GameMakerSound Group, GameMakerSound Stream, GameMakerSound Inside);

    private static async Task<Fixture> Make(TempDir dir, bool groupFile = true, bool streamFile = true)
    {
        using (var data = TestData.Create(0))
        {
            data.AudioGroups.Add(new GameMakerAudioGroup { Name = data.Strings.MakeString("audiogroup_default"), Path = data.Strings.MakeString("") }); // group 0 is the data file itself
            var group = new GameMakerAudioGroup { Name = data.Strings.MakeString("ag_music"), Path = data.Strings.MakeString("audiogroup1.dat") };
            data.AudioGroups.Add(group);
            var id = data.AudioGroups.Count - 1;
            var inside = new GameMakerEmbeddedAudio { Data = Streamed };
            data.EmbeddedAudio.Add(inside);
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("snd_group"), AudioGroup = group, AudioID = 0, AudioLength = 1 });
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("snd_stream"), File = data.Strings.MakeString("mus_stream.wav"), AudioID = -1, Flags = GameMakerSound.AudioEntryFlags.Regular, AudioLength = 1 });
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("snd_inside"), AudioFile = inside, AudioID = 0, AudioLength = 1 });
            TestData.Write(dir.File("data.win"), data);
            Assert.True(id > 0, "the group must not be the default one");
        }

        if (groupFile) WriteGroup(dir.File("audiogroup1.dat"), Streamed);
        if (streamFile) await File.WriteAllBytesAsync(dir.File("mus_stream.wav"), Streamed, TestContext.Current.CancellationToken);
        var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
        await service.OpenAsync(dir.File("data.win"), cancellationToken: TestContext.Current.CancellationToken);
        var workspace = Assert.IsType<Workspace>(service.Current);
        GameMakerSound Sound(string name) => workspace.Data.Sounds.Single(s => s.Name.Content == name);
        return new Fixture(service, workspace, dir.Path, Sound("snd_group"), Sound("snd_stream"), Sound("snd_inside"));
    }

    [Fact]
    public async Task AnAudioGroupEntryAndAStreamedFileAreFoundBesideTheDataFileAndRead()
    {
        using var dir = new TempDir();
        var f = await Make(dir);
        var external = f.Workspace.ExternalAudio;

        var group = external.Locate(f.Group)!;
        Assert.Equal((ExternalKind.Group, true), (group.Kind, group.Exists));
        Assert.Equal(dir.File("audiogroup1.dat"), group.Path);
        Assert.Equal(Streamed, external.Load(f.Group)); // the entry, not the whole file

        var stream = external.Locate(f.Stream)!;
        Assert.Equal((ExternalKind.Streamed, true), (stream.Kind, stream.Exists));
        Assert.Equal(dir.File("mus_stream.wav"), stream.Path);
        Assert.Equal(Streamed, external.Load(f.Stream));

        Assert.Null(external.Locate(f.Inside)); // its audio is in the data file
        Assert.Null(external.Load(f.Inside));
    }

    [Fact]
    public async Task AMissingFileKeepsTheExpectedPathAndLoadsNothing()
    {
        using var dir = new TempDir();
        var f = await Make(dir, groupFile: false, streamFile: false);
        var external = f.Workspace.ExternalAudio;
        Assert.Equal((dir.File("audiogroup1.dat"), false), (external.Locate(f.Group)!.Path, external.Locate(f.Group)!.Exists));
        Assert.Equal((dir.File("mus_stream.wav"), false), (external.Locate(f.Stream)!.Path, external.Locate(f.Stream)!.Exists));
        Assert.Null(external.Load(f.Group));
        Assert.Null(external.Load(f.Stream));
    }

    [Fact]
    public async Task ANameThatLeavesTheFolderOfTheDataFileIsNotLookedUpAndAnEndingIsAddedWhenTheNameHasNone()
    {
        using var dir = new TempDir();
        var f = await Make(dir);
        var external = f.Workspace.ExternalAudio;
        f.Stream.File = new GameMakerString("../outside.ogg");
        Assert.Null(external.Locate(f.Stream));
        f.Stream.File = new GameMakerString("mus_bare");
        File.WriteAllBytes(dir.File("mus_bare.ogg"), [(byte)'O', (byte)'g', (byte)'g', (byte)'S']);
        Assert.Equal(dir.File("mus_bare.ogg"), external.Locate(f.Stream)!.Path);
    }

    [Fact]
    public async Task TheGroupFileIsClosedWhenTheCacheIsLetGo()
    {
        using var dir = new TempDir();
        var f = await Make(dir);
        Assert.NotNull(f.Workspace.ExternalAudio.Load(f.Group));
        f.Workspace.ExternalAudio.Dispose();
        File.WriteAllBytes(dir.File("audiogroup1.dat"), [1]);
        Assert.Null(f.Workspace.ExternalAudio.Load(f.Group)); // and what is in the file now is not an audio group file: nothing, no exception
    }

    [Fact]
    public async Task TheSoundTabPlaysAGroupEntryAndAStreamedFileAndANoteNamesAMissingOne()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var output = AudioPlaybackTests.UseNullDevice();
            var f = await Make(dir);
            try
            {
                foreach (var sound in new[] { f.Group, f.Stream })
                {
                    var view = new SoundPlayerView(sound, _ => throw new InvalidOperationException("the system player must not open"), () => new AudioOptions(), f.Workspace.ExternalAudio);
                    var window = new Window { Width = 600, Height = 500, Content = view };
                    window.Show();
                    try
                    {
                        var play = AudioPlaybackTests.Find<Button>(view, "PlaySound");
                        Assert.True(play.IsEnabled);
                        Assert.Contains(f.Folder, view.Status, StringComparison.Ordinal);
                        await view.Transport!.PlayPauseAsync(); // read and decoded on a pool thread
                        Assert.True(output.IsPlaying);
                        Assert.Contains("0:01.0", view.Transport.TimeText, StringComparison.Ordinal);
                        view.StopPlayback();
                    }
                    finally
                    {
                        window.Close();
                    }
                }

                File.Delete(dir.File("mus_stream.wav"));
                var missing = new SoundPlayerView(f.Stream, _ => true, () => new AudioOptions(), f.Workspace.ExternalAudio);
                Assert.Null(missing.Transport);
                Assert.False(AudioPlaybackTests.Find<Button>(missing, "PlaySound").IsEnabled);
                Assert.Equal(Loc.Current.Format("sounds.outside", ("file", dir.File("mus_stream.wav"))), missing.Status); // the note keeps the expected path
                var without = new SoundPlayerView(f.Group, _ => true); // no reader given: as before
                Assert.Null(without.Transport);
            }
            finally
            {
                AudioDevices.Shutdown();
            }
        });
    }
}
