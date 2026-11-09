using G3MStudio.Core.Workspace;
using ImageMagick;

namespace G3MStudio.Tests;

public sealed class SpriteGifTests
{
    [Fact]
    public void AllFramesBecomeGifFramesWithTheDelayOfThePlaybackSpeedAndTransparencyKept()
    {
        var frames = Enumerable.Range(0, 3).Select(i =>
        {
            var frame = new PixelBuffer(5, 4);
            frame.Data[0] = (byte)(60 * (i + 1)); // R of the first pixel, alpha stays 0
            for (var p = 4; p < frame.Data.Length; p += 4) frame.Data[p + 3] = 255;
            return frame;
        }).ToList();
        var gif = SpriteGif.Encode(frames, 10);

        using var read = new MagickImageCollection(gif);
        Assert.Equal(3, read.Count);
        Assert.All(read, f => Assert.Equal((5u, 4u), (f.Width, f.Height)));
        Assert.All(read, f => Assert.Equal(10u, f.AnimationDelay));
        Assert.Equal(0, read[0].GetPixels().GetPixel(0, 0).ToColor()!.A);
    }

    [Fact]
    public void ASpriteWithoutFramesIsRefused() => Assert.Throws<InvalidOperationException>(() => SpriteGif.Encode([], 10));
}
