using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class GroupListTests
{
    [Fact]
    public async Task NewAndRenamedGroupsSurviveAWriteAndUndoPutsThemBack()
    {
        using var data = await GameMakerIO.ReadAsync(Corpus.Files(Corpus.Folder).First(), cancellationToken: TestContext.Current.CancellationToken);
        var textures = data.TextureGroupInfo!.Count;
        var audios = data.AudioGroups!.Count;
        var newTexture = new GroupListCommand(false, GroupListCommand.New, "zz_tex");
        var newAudio = new GroupListCommand(true, GroupListCommand.New, "zz_aud");
        newTexture.Apply(data);
        newAudio.Apply(data);
        var rename = new GroupListCommand(false, GroupListCommand.Rename, "zz_tex", "zz_tex2");
        rename.Apply(data);
        Assert.Throws<InvalidOperationException>(() => new GroupListCommand(false, GroupListCommand.New, "zz_tex2").Apply(data));
        Assert.Throws<UnusualNameException>(() => new GroupListCommand(true, GroupListCommand.New, "bad name").Apply(data));
        Assert.Throws<InvalidOperationException>(() => new GroupListCommand(true, GroupListCommand.New, "a/b", force: true).Apply(data));
        var forced = new GroupListCommand(true, GroupListCommand.New, "bad name", force: true);
        forced.Apply(data);
        Assert.Contains(data.AudioGroups!, g => g.Name.Content == "bad name");
        Assert.Throws<InvalidOperationException>(() => new GroupListCommand(true, GroupListCommand.New, "bad name", force: true).Apply(data));
        Assert.True(EditCommand.Create(GroupListCommand.Name, forced.Args) is GroupListCommand { Args: var back } && back.ContainsKey("force"));
        forced.Revert(data);

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var again = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains(again.TextureGroupInfo!, g => g.Name.Content == "zz_tex2");
        Assert.Contains(again.AudioGroups!, g => g.Name.Content == "zz_aud");

        rename.Revert(data);
        Assert.Contains(data.TextureGroupInfo!, g => g.Name.Content == "zz_tex");
        newAudio.Revert(data);
        newTexture.Revert(data);
        Assert.Equal(textures, data.TextureGroupInfo!.Count);
        Assert.Equal(audios, data.AudioGroups!.Count);
    }
}
