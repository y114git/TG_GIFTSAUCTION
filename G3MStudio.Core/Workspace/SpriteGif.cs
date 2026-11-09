using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using ImageMagick;

namespace G3MStudio.Core.Workspace;

/// <summary>A sprite as an animated GIF: every frame, the delay from the playback speed, transparent pixels kept.</summary>
public static class SpriteGif
{
    /// <summary>GameMaker's playback speed is frames per second, or frames per game frame at an assumed 30 game frames per second.</summary>
    public static double FramesPerSecond(GameMakerSprite sprite) =>
        Math.Max(1, sprite.GMS2PlaybackSpeed * (sprite.GMS2PlaybackSpeedType == AnimSpeedType.FramesPerGameFrame ? 30 : 1));

    public static byte[] ToGif(GameMakerData data, string sprite)
    {
        var item = data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");
        return Encode([.. Enumerable.Range(0, item.Textures.Count).Select(i => FramePicture.Read(data, sprite, i))], FramesPerSecond(item));
    }

    public static byte[] Encode(IReadOnlyList<PixelBuffer> frames, double framesPerSecond)
    {
        if (frames.Count == 0) throw new InvalidOperationException("The sprite has no frames.");
        // A GIF counts in hundredths of a second; viewers treat a delay under 2 as 10.
        var delay = (uint)Math.Max(2, Math.Round(100 / framesPerSecond));
        using var images = new MagickImageCollection();
        foreach (var frame in frames)
        {
            images.Add(new MagickImage(frame.Data, new PixelReadSettings((uint)frame.Width, (uint)frame.Height, StorageType.Char, PixelMapping.RGBA))
            {
                AnimationDelay = delay,
                GifDisposeMethod = GifDisposeMethod.Background,
            });
        }

        images[0].AnimationIterations = 0;
        using var stream = new MemoryStream();
        images.Write(stream, MagickFormat.Gif);
        return stream.ToArray();
    }
}
