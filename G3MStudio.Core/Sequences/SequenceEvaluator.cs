using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Sequences;

/// <summary>A sprite of a sequence at one frame: where it is, how it is scaled and turned (degrees, counter-clockwise as in GameMaker).</summary>
public readonly record struct SequenceSprite(GameMakerSprite Sprite, float X, float Y, float ScaleX, float ScaleY, float Rotation, int Image = 0);

/// <summary>
/// What a sequence shows at a frame, for the preview canvas: the sprite of every graphic track whose keyframe covers the frame, moved by the position
/// tracks of its own and of the groups around it, scaled and turned by its scale and rotation tracks. Values between keyframes are linear when the track
/// says so and stepped otherwise.
/// shortcut: blend colors, masks, origin tracks, and instance, text and particle tracks are not drawn; add them when the canvas needs to match the game.
/// </summary>
public static class SequenceEvaluator
{
    private const int MaxDepth = 12;

    public static IReadOnlyList<SequenceSprite> At(GameMakerSequence sequence, float frame)
    {
        var sprites = new List<SequenceSprite>();
        if (!float.IsFinite(frame)) return sprites;
        foreach (var track in sequence.Tracks ?? []) Walk(track, 0, 0, frame, sprites, 0);
        return sprites;
    }

    private static GameMakerSequence.Track? Child(GameMakerSequence.Track track, GameMakerSequence.Track.TrackBuiltinName name, string fallbackName) =>
        (track.Tracks ?? []).FirstOrDefault(c => c.BuiltinName == name || string.Equals(c.Name?.Content, fallbackName, StringComparison.OrdinalIgnoreCase));

    private static void Walk(GameMakerSequence.Track track, float parentX, float parentY, float frame, List<SequenceSprite> sprites, int depth)
    {
        if (depth > MaxDepth) return;
        var position = Child(track, GameMakerSequence.Track.TrackBuiltinName.Position, "position");
        var (x, y) = (parentX + Value(position, 0, frame, 0), parentY + Value(position, 1, frame, 0));
        if (track.Keyframes is GameMakerSequence.GraphicKeyframes graphic && SpriteAt(graphic, frame) is { } sprite)
        {
            var scale = Child(track, GameMakerSequence.Track.TrackBuiltinName.Scale, "scale");
            var rotation = Child(track, GameMakerSequence.Track.TrackBuiltinName.Rotation, "rotation");
            var image = Child(track, GameMakerSequence.Track.TrackBuiltinName.ImageIndex, "image index");
            sprites.Add(new SequenceSprite(sprite, x, y, Value(scale, 0, frame, 1), Value(scale, 1, frame, 1), Value(rotation, 0, frame, 0), ImageIndex(image, frame)));
        }

        // A nested sequence plays inside the track, from the frame of its keyframe.
        if (track.Keyframes is GameMakerSequence.SequenceKeyframes nested)
            foreach (var keyframe in nested.List ?? [])
                if (float.IsFinite(keyframe.Key) && keyframe.Key <= frame && frame < keyframe.Key + Math.Max(keyframe.Length, 0) && keyframe.Channels.FirstOrDefault()?.Value?.Resource?.Resource is { } inner)
                    foreach (var innerTrack in inner.Tracks ?? []) Walk(innerTrack, x, y, frame - keyframe.Key, sprites, depth + 1);

        foreach (var child in track.Tracks ?? [])
            if (child.Keyframes is not GameMakerSequence.RealKeyframes) Walk(child, x, y, frame, sprites, depth + 1);
    }

    private static GameMakerSprite? SpriteAt(GameMakerSequence.GraphicKeyframes graphic, float frame)
    {
        foreach (var keyframe in graphic.List ?? [])
            if (float.IsFinite(keyframe.Key) && keyframe.Key <= frame && frame < keyframe.Key + Math.Max(keyframe.Length, 0)) return keyframe.Channels.FirstOrDefault()?.Value?.Resource?.Resource;
        return null;
    }

    /// <summary>The image index track of a graphic: whole numbers that hold until the next keyframe, whether the track stores floats or integers. 0 when there is none.</summary>
    private static int ImageIndex(GameMakerSequence.Track? track, float frame)
    {
        if (track?.Keyframes is GameMakerSequence.SpriteFramesKeyframes frames)
        {
            var last = (frames.List ?? []).Where(k => float.IsFinite(k.Key) && k.Key <= frame).OrderBy(k => k.Key).LastOrDefault();
            return Math.Max(0, last?.Channels.FirstOrDefault()?.Value?.Value ?? 0);
        }

        var value = Value(track, 0, frame, 0);
        return float.IsFinite(value) ? (int)Math.Clamp(Math.Floor(value), 0, 100_000) : 0;
    }

    /// <summary>The value of a channel of a float track at a frame: the first keyframe before it starts, the last after it ends, linear or stepped between.</summary>
    public static float Value(GameMakerSequence.Track? track, int channel, float frame, float fallback)
    {
        if (track?.Keyframes is not GameMakerSequence.RealKeyframes real || real.List is not { Count: > 0 } list) return fallback;
        var keys = list.Where(k => float.IsFinite(k.Key)).OrderBy(k => k.Key).ToList();
        if (keys.Count == 0) return fallback;
        float At(int i)
        {
            var value = keys[i].Channels.FirstOrDefault(c => c.Channel == channel)?.Value?.Value ?? fallback;
            return float.IsFinite(value) ? value : fallback;
        }

        if (frame <= keys[0].Key) return At(0);
        for (var i = 0; i + 1 < keys.Count; i++)
        {
            if (frame >= keys[i + 1].Key) continue;
            var span = keys[i + 1].Key - keys[i].Key;
            return real.Interpolation == 1 && span > 0 ? At(i) + ((At(i + 1) - At(i)) * ((frame - keys[i].Key) / span)) : At(i);
        }

        return At(keys.Count - 1);
    }
}
