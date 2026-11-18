namespace G3MStudio.Core.Workspace;

/// <summary>
/// Turns a glyph coverage bitmap into the signed distance field GameMaker keeps in an SDF font sheet. The encoding was read from sheets that the GameMaker IDE made
/// (Cover Assault, spread 16): the color is white and the alpha holds the distance, 128 at the glyph edge, rising inside the glyph and falling outside until it reaches
/// 0 at the spread distance, about 128 / spread alpha levels per pixel.
/// shortcut: the distance is measured from whole pixels (the coverage is cut at one half), so an edge can be off by up to half a pixel; upgrade when a sub-pixel distance is worth the code.
/// </summary>
public static class SdfEncoder
{
    private const float Far = 1e20f;

    /// <summary>The alpha of each pixel: <paramref name="coverage"/> is width * height values of 0 to 255, <paramref name="spread"/> the spread in pixels (at least 1).</summary>
    public static byte[] Encode(byte[] coverage, int width, int height, int spread)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(spread, 1);
        if (coverage.Length != width * height) throw new ArgumentException("The bitmap does not match its size.", nameof(coverage));
        var toInside = new float[coverage.Length];
        var toOutside = new float[coverage.Length];
        var at = 0;
        foreach (var value in coverage)
        {
            var inside = value >= 128;
            toInside[at] = inside ? 0 : Far;
            toOutside[at++] = inside ? Far : 0;
        }

        Transform(toInside, width, height);
        Transform(toOutside, width, height);
        var result = new byte[coverage.Length];
        at = 0;
        foreach (var value in coverage)
        {
            // Positive inside: the distance to the nearest outside pixel, minus half a pixel because the edge lies between pixel centers; negative outside likewise.
            var signed = value >= 128 ? MathF.Sqrt(toOutside[at]) - 0.5f : -(MathF.Sqrt(toInside[at]) - 0.5f);
            result[at++] = (byte)Math.Clamp((int)MathF.Round(128 + 128 * signed / spread), 0, 255);
        }

        return result;
    }

    // The squared Euclidean distance to the nearest zero, in two passes of the one dimensional lower envelope (Felzenszwalb and Huttenlocher).
    private static void Transform(float[] grid, int width, int height)
    {
        var column = new float[height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++) column[y] = grid[y * width + x];
            Line(column);
            for (var y = 0; y < height; y++) grid[y * width + x] = column[y];
        }

        var row = new float[width];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(grid, y * width, row, 0, width);
            Line(row);
            Array.Copy(row, 0, grid, y * width, width);
        }
    }

    private static void Line(float[] f)
    {
        var n = f.Length;
        var v = new int[n];
        var z = new float[n + 1];
        var k = 0;
        z[0] = -Far;
        z[1] = Far;
        for (var q = 1; q < n; q++)
        {
            float s;
            while ((s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2f * q - 2f * v[k])) <= z[k] && k > 0) k--;
            if (s <= z[k])
            {
                v[k] = q;
                z[k] = -Far;
                z[k + 1] = Far;
                continue;
            }

            k++;
            v[k] = q;
            z[k] = s;
            z[k + 1] = Far;
        }

        var source = (float[])f.Clone();
        k = 0;
        for (var q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            f[q] = (q - v[k]) * (q - v[k]) + source[v[k]];
        }
    }
}
