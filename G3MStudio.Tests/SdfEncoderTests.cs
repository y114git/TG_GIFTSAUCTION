using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class SdfEncoderTests
{
    private static byte[] Square(int size, int from, int to)
    {
        var bitmap = new byte[size * size];
        for (var y = from; y < to; y++)
            for (var x = from; x < to; x++) bitmap[y * size + x] = 255;
        return bitmap;
    }

    [Fact]
    public void TheDistanceIs128AtTheEdgeRisesInsideAndFallsToZeroAtTheSpread()
    {
        const int size = 64, spread = 8;
        var sdf = SdfEncoder.Encode(Square(size, 24, 40), size, size, spread);
        byte At(int x, int y) => sdf[y * size + x];
        Assert.InRange(At(24, 32), 128 - 128 / spread, 128 + 128 / spread); // the first inside pixel is next to the edge
        Assert.InRange(At(23, 32), 128 - 128 / spread, 128 + 128 / spread); // and so is the first outside one
        Assert.True(At(32, 32) > At(26, 32)); // deeper inside is higher
        Assert.True(At(20, 32) < At(23, 32)); // farther outside is lower
        Assert.Equal(0, At(10, 32)); // beyond the spread
        Assert.Equal(0, At(0, 0));
        // About 128 / spread levels per pixel on the way out, as in the sheets the IDE makes.
        Assert.InRange(At(23, 32) - At(21, 32), 2 * 128 / spread - 3, 2 * 128 / spread + 3);
    }

    [Fact]
    public void AnEmptyOrFullBitmapStaysAtTheExtremes()
    {
        Assert.All(SdfEncoder.Encode(new byte[16 * 16], 16, 16, 4), v => Assert.Equal(0, v));
        Assert.All(SdfEncoder.Encode([.. Enumerable.Repeat((byte)255, 16 * 16)], 16, 16, 4), v => Assert.Equal(255, v));
        Assert.Throws<ArgumentException>(() => SdfEncoder.Encode(new byte[5], 4, 4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => SdfEncoder.Encode(new byte[16], 4, 4, 0));
    }
}
