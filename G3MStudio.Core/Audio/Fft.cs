namespace G3MStudio.Core.Audio;

/// <summary>A radix-2 fast Fourier transform and the Hann window, for the spectrum of the sound that plays.</summary>
public static class Fft
{
    /// <summary>The Hann window of <paramref name="size"/> samples: 0 at both ends, 1 in the middle.</summary>
    public static float[] Hann(int size)
    {
        var window = new float[size];
        for (var i = 0; i < size; i++) window[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / Math.Max(1, size - 1)));
        return window;
    }

    /// <summary>The transform in place; both arrays are as long as a power of two. Bin k of the result is the frequency k times the sample rate over the length.</summary>
    public static void Transform(float[] real, float[] imaginary)
    {
        var n = real.Length;
        if (n == 0 || (n & (n - 1)) != 0 || imaginary.Length != n) throw new ArgumentException("The length must be a power of two, the same for both arrays.");
        for (int i = 1, j = 0; i < n; i++) // the samples in bit reversed order
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (real[i], real[j], imaginary[i], imaginary[j]) = (real[j], real[i], imaginary[j], imaginary[i]);
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            double stepRe = Math.Cos(angle), stepIm = Math.Sin(angle);
            for (var start = 0; start < n; start += length)
            {
                double re = 1, im = 0;
                for (var k = 0; k < length / 2; k++)
                {
                    int a = start + k, b = a + length / 2;
                    var (tr, ti) = (real[b] * re - imaginary[b] * im, real[b] * im + imaginary[b] * re);
                    (real[b], imaginary[b]) = ((float)(real[a] - tr), (float)(imaginary[a] - ti));
                    (real[a], imaginary[a]) = ((float)(real[a] + tr), (float)(imaginary[a] + ti));
                    (re, im) = (re * stepRe - im * stepIm, re * stepIm + im * stepRe);
                }
            }
        }
    }
}
