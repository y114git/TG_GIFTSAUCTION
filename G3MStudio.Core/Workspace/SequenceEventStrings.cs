using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>The strings of a moment or broadcast message keyframe of a sequence, and a new keyframe with its text.</summary>
internal static class SequenceEventStrings
{
    // The one channel a moment or message keyframe has.
    public static GameMakerSimpleListString? Of(object keyframe) => keyframe switch
    {
        GameMakerSequence.Keyframe<GameMakerSequence.Moment> m => m.Channels.FirstOrDefault()?.Value.Events,
        GameMakerSequence.Keyframe<GameMakerSequence.BroadcastMessage> b => b.Channels.FirstOrDefault()?.Value.Messages,
        _ => null,
    };

    public static string[] Lines(string value) => value.Length == 0 ? [] : value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    public static void Replace(GameMakerSimpleListString strings, GameMakerData data, string[] lines)
    {
        strings.Clear();
        foreach (var line in lines) strings.Add(data.Strings.MakeString(line, out _));
    }

    /// <summary>A keyframe of one second-long length at <paramref name="frame"/> with one channel holding the lines of <paramref name="text"/>.</summary>
    public static object Make(bool moment, GameMakerData data, string text, float frame) => moment
        ? Build<GameMakerSequence.Moment>(frame, v => v.Events = Fill(data, text))
        : Build<GameMakerSequence.BroadcastMessage>(frame, v => v.Messages = Fill(data, text));

    private static GameMakerSimpleListString Fill(GameMakerData data, string text)
    {
        var strings = new GameMakerSimpleListString();
        Replace(strings, data, Lines(text));
        return strings;
    }

    private static GameMakerSequence.Keyframe<T> Build<T>(float frame, Action<T> fill) where T : GameMakerObject, new()
    {
        var key = new GameMakerSequence.Keyframe<T> { Key = frame, Length = 1 };
        var channel = new GameMakerSequence.Keyframe<T>.KeyframeChannel { Channel = 0 };
        fill(channel.Value);
        key.Channels.Add(channel);
        return key;
    }
}
