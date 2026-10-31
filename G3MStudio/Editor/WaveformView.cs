using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace G3MStudio.Editor;

/// <summary>
/// The loudness of a 16-bit PCM WAV drawn as one bar per pixel column bucket. Other formats (OGG, MP3) are not decoded, so they get no picture.
/// shortcut: peaks are computed on the UI thread from the bytes already in memory; move to a pool thread if sounds of hundreds of MB show up.
/// </summary>
public sealed class WaveformView : Control
{
    public const int Buckets = 400;

    private readonly float[] _peaks;

    public WaveformView(float[] peaks)
    {
        _peaks = peaks;
        Name = "Waveform";
        Height = 64;
    }

    /// <summary>Peak per bucket (0 to 1) of a PCM16 WAV, or null for any other data.</summary>
    public static float[]? Peaks(byte[] wav, int buckets = Buckets)
    {
        if (wav.Length < 44 || wav[0] != 'R' || wav[8] != 'W') return null;
        int channels = 0, bits = 0;
        for (var at = 12; at + 8 <= wav.Length;)
        {
            var size = BitConverter.ToUInt32(wav, at + 4);
            var id = System.Text.Encoding.ASCII.GetString(wav, at, 4);
            if (id == "fmt " && at + 24 <= wav.Length && BitConverter.ToUInt16(wav, at + 8) == 1)
                (channels, bits) = (BitConverter.ToUInt16(wav, at + 10), BitConverter.ToUInt16(wav, at + 22));
            else if (id == "data")
            {
                if (channels == 0 || bits != 16) return null;
                var count = (int)Math.Min(size, (uint)(wav.Length - at - 8)) / (2 * channels);
                if (count == 0) return null;
                var peaks = new float[buckets];
                for (var frame = 0; frame < count; frame++)
                {
                    var bucket = (int)((long)frame * buckets / count);
                    for (var c = 0; c < channels; c++)
                        peaks[bucket] = Math.Max(peaks[bucket], Math.Abs((int)BitConverter.ToInt16(wav, at + 8 + ((frame * channels) + c) * 2)) / 32768f);
                }

                return peaks;
            }

            at += 8 + (int)Math.Min(size + (size & 1), int.MaxValue - 8);
        }

        return null;
    }

    public override void Render(DrawingContext context)
    {
        var brush = Application.Current is { } app && app.TryGetResource("Text.Accent", app.ActualThemeVariant, out var found) && found is IBrush accent ? accent : Brushes.Gray;
        var (width, middle) = (Bounds.Width, Bounds.Height / 2);
        for (var i = 0; i < _peaks.Length; i++)
        {
            var half = Math.Max(1, _peaks[i] * middle);
            context.FillRectangle(brush, new Rect(i * width / _peaks.Length, middle - half, Math.Max(1, width / _peaks.Length - 0.5), half * 2));
        }
    }
}
