using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class AudioExportTests
{
    [Fact]
    public async Task EverySoundWithAudioIsWrittenUnderItsOwnNameAndExtension_AndOneWithoutIsSkipped()
    {
        using var dir = new TempDir();
        byte[] wav = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 2, 3, 4], ogg = [(byte)'O', (byte)'g', (byte)'g', (byte)'S', 9];
#pragma warning disable CA2000 // the data model owns what is added to it
        using (var data = TestData.Create(0))
        {
            var (a, b) = (new GameMakerEmbeddedAudio { Data = wav }, new GameMakerEmbeddedAudio { Data = ogg });
            data.EmbeddedAudio.Add(a);
            data.EmbeddedAudio.Add(b);
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("snd_a", out _), AudioID = 0, AudioFile = a });
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("snd_b", out _), AudioID = 1, AudioFile = b });
            data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("snd_none", out _), AudioID = -1 });
            TestData.Write(dir.File("data.win"), data);
        }
#pragma warning restore CA2000

        var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
        await service.OpenAsync(dir.File("data.win"), cancellationToken: TestContext.Current.CancellationToken);
        var workspace = Assert.IsType<Workspace>(service.Current);
        var steps = new List<int>();
        var results = await AudioExport.ExportAllAsync(workspace, dir.File("out"), new SyncProgress<BatchProgress>(p => steps.Add(p.Done)), TestContext.Current.CancellationToken);

        Assert.Equal(wav, await File.ReadAllBytesAsync(dir.File("out/snd_a.wav"), TestContext.Current.CancellationToken));
        Assert.Equal(ogg, await File.ReadAllBytesAsync(dir.File("out/snd_b.ogg"), TestContext.Current.CancellationToken));
        Assert.Equal(2, Directory.GetFiles(dir.File("out")).Length);
        Assert.Equal(1, results.Count(r => r.Kind == ResultKind.Skipped));
        Assert.Equal(steps.Order(), steps);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AudioExport.ExportAllAsync(workspace, dir.File("out2"), null, new CancellationToken(true)));
    }
}
