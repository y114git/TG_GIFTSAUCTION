using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the data model owns what is added to it
public sealed class SequenceKeyframeTests
{
    private const string Path = "Tracks[0].Keyframes.List";

    private static GameMakerSequence Build(GameMakerData data)
    {
        var frame = new GameMakerSequence.Keyframe<GameMakerSequence.RealData> { Key = 0, Length = 8 };
        frame.Channels.Add(new GameMakerSequence.Keyframe<GameMakerSequence.RealData>.KeyframeChannel { Channel = 0, Value = new GameMakerSequence.RealData { Value = 3 } });
        var track = new GameMakerSequence.Track { Name = data.Strings.MakeString("t"), Tracks = [], Keyframes = new GameMakerSequence.RealKeyframes { List = [frame] } };
        var sequence = new GameMakerSequence { Name = data.Strings.MakeString("seq_a"), Tracks = [track] };
        data.Sequences.Add(sequence);
        return sequence;
    }

    private static List<(float Key, float Length)> Keys(GameMakerSequence sequence) =>
        [.. ((GameMakerSequence.RealKeyframes)sequence.Tracks[0].Keyframes).List.Select(k => (k.Key, k.Length))];

    [Fact]
    public void SplitAddAndDeleteChangeTheListAndUndoRestoresIt()
    {
        using var data = TestData.Create(0);
        var sequence = Build(data);
        var history = new EditHistory(data);
        history.Execute(new SequenceKeyframeCommand("seq_a", Path, SequenceKeyframeCommand.Split, 0, 5));
        Assert.Equal([(0f, 5f), (5f, 3f)], Keys(sequence));
        var list = ((GameMakerSequence.RealKeyframes)sequence.Tracks[0].Keyframes).List;
        Assert.Equal(3f, list[1].Channels[0].Value.Value);
        history.Undo();
        Assert.Equal([(0f, 8f)], Keys(sequence));

        history.Execute(new SequenceKeyframeCommand("seq_a", Path, SequenceKeyframeCommand.Add, -1, 12));
        Assert.Equal([(0f, 8f), (12f, 1f)], Keys(sequence));
        history.Execute(new SequenceKeyframeCommand("seq_a", Path, SequenceKeyframeCommand.Delete, 0, 0));
        Assert.Equal([(12f, 1f)], Keys(sequence));
        history.Undo();
        history.Undo();
        Assert.Equal([(0f, 8f)], Keys(sequence));
    }

    [Fact]
    public void BadRequestsAreRefusedAndReplayRebuildsTheCommand()
    {
        using var data = TestData.Create(0);
        Build(data);
        Assert.Throws<InvalidOperationException>(() => new SequenceKeyframeCommand("seq_a", Path, SequenceKeyframeCommand.Split, 0, 0).Apply(data));
        Assert.Throws<InvalidOperationException>(() => new SequenceKeyframeCommand("seq_a", Path, SequenceKeyframeCommand.Add, -1, 3).Apply(data));
        Assert.Throws<InvalidOperationException>(() => new SequenceKeyframeCommand("seq_a", "Moments", SequenceKeyframeCommand.Delete, 0, 0).Apply(data));
        var replay = EditCommand.Create(SequenceKeyframeCommand.Name, new SequenceKeyframeCommand("seq_a", Path, SequenceKeyframeCommand.Add, -1, 20).Args);
        replay.Apply(data);
        replay.Revert(data);
    }

    [Fact]
    public void SnapPrefersATargetInReachAndElseRounds()
    {
        Assert.Equal(10f, SequenceSnap.Snap(9.6f, [10f, 4f], 1f));
        Assert.Equal(7f, SequenceSnap.Snap(6.8f, [20f], 1f));
    }
}
