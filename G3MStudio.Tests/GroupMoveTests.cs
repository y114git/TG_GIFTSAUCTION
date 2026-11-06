using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class GroupMoveTests
{
    [Fact]
    public void SoundsMoveToAnotherAudioGroupAndUndoRestores()
    {
        using var data = TestData.Create(0);
        var (a, b) = (new GameMakerAudioGroup { Name = data.Strings.MakeString("ag_a") }, new GameMakerAudioGroup { Name = data.Strings.MakeString("ag_b") });
        data.AudioGroups.Add(a);
        data.AudioGroups.Add(b);
        var sound = new GameMakerSound { Name = data.Strings.MakeString("snd_x"), AudioGroup = a };
        data.Sounds.Add(sound);
        var history = new EditHistory(data);
        history.Execute(new AudioGroupMoveCommand(["snd_x"], "ag_b"));
        Assert.Same(b, sound.AudioGroup);
        history.Undo();
        Assert.Same(a, sound.AudioGroup);
        var replay = EditCommand.Create(AudioGroupMoveCommand.Name, new AudioGroupMoveCommand(["snd_x"], "ag_b").Args);
        replay.Apply(data);
        Assert.Same(b, sound.AudioGroup);
        Assert.Throws<InvalidOperationException>(() => new AudioGroupMoveCommand(["snd_x"], "nope").Apply(data));
    }

    [Fact]
    public void SpritesMoveBetweenTextureGroupsAndUndoRestoresBothLists()
    {
        using var data = TestData.Create(0);
        TestData.AddPage(data);
        var sprite = SpriteTemplate.Create(data, "spr_g");
        var groups = data.TextureGroupInfo;
        Assert.SkipUnless(groups is not null, "the generated file has no texture groups");
        var other = new GameMakerTextureGroupInfo { Name = data.Strings.MakeString("tg_other") };
        groups.Add(other);
        var history = new EditHistory(data);
        history.Execute(new TextureGroupMoveCommand("sprites", ["spr_g"], "tg_other"));
        Assert.Contains(other.Sprites, e => TextureGroupMoveCommand.NameOf(e) == "spr_g");
        Assert.Equal(1, groups.Sum(g => g.Sprites.Count(e => TextureGroupMoveCommand.NameOf(e) == "spr_g")));
        history.Undo();
        Assert.DoesNotContain(other.Sprites, e => TextureGroupMoveCommand.NameOf(e) == "spr_g");
        Assert.NotNull(sprite);
    }
}
