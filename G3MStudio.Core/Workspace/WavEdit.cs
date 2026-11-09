namespace G3MStudio.Core.Workspace;

/// <summary>The format of a WAV's samples.</summary>
public readonly record struct WavFormat(int Channels, int SampleRate, int Bits, int DataStart, int DataLength)
{
    public int FrameBytes => Channels * Bits / 8;

    public int Frames => DataLength / Math.Max(1, FrameBytes);

    public double Seconds => (double)Frames / Math.Max(1, SampleRate);
}

/// <summary>Cuts, scales and fades PCM WAV audio (8 and 16 bit) and writes a new WAV. Other chunks of the file are dropped.</summary>
public static class WavEdit
{
    public static WavFormat Read(byte[] wav)
    {
        if (wav.Length < 12 || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F' || wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E') throw new InvalidOperationException("The audio is not a WAV file.");
        int channels = 0, rate = 0, bits = 0, tag = 0;
        for (var at = 12; at + 8 <= wav.Length;)
        {
            var id = System.Text.Encoding.ASCII.GetString(wav, at, 4);
            var size = BitConverter.ToUInt32(wav, at + 4);
            if (id == "fmt " && at + 24 <= wav.Length)
            {
                tag = BitConverter.ToUInt16(wav, at + 8);
                channels = BitConverter.ToUInt16(wav, at + 10);
                rate = (int)BitConverter.ToUInt32(wav, at + 12);
                bits = BitConverter.ToUInt16(wav, at + 22);
            }
            else if (id == "data")
            {
                var length = (int)Math.Min(size, (uint)(wav.Length - at - 8));
                if (tag != 1 || channels < 1 || rate < 1 || bits is not (8 or 16)) throw new InvalidOperationException("Only 8 and 16 bit PCM WAV audio can be edited.");
                return new WavFormat(channels, rate, bits, at + 8, length);
            }

            at += 8 + (int)Math.Min(size + (size & 1), int.MaxValue - 8);
        }

        throw new InvalidOperationException("The WAV file has no audio data.");
    }

    /// <summary>
    /// The part from <paramref name="start"/> to <paramref name="end"/> seconds, times <paramref name="gain"/> (1 is unchanged, clipped at full scale),
    /// with a linear fade in and out of the given lengths in seconds.
    /// </summary>
    public static byte[] Edit(byte[] wav, double start, double end, double gain = 1, double fadeIn = 0, double fadeOut = 0)
    {
        var format = Read(wav);
        var first = (int)Math.Clamp(Math.Round(start * format.SampleRate), 0, format.Frames);
        var last = (int)Math.Clamp(Math.Round(end * format.SampleRate), first, format.Frames);
        if (last - first < 1) throw new InvalidOperationException("The cut would leave no audio.");
        if (gain is < 0 or > 16) throw new InvalidOperationException("The gain must be between 0 and 1600 percent.");
        var frames = last - first;
        var data = new byte[frames * format.FrameBytes];
        double fadeInFrames = Math.Max(0, fadeIn) * format.SampleRate, fadeOutFrames = Math.Max(0, fadeOut) * format.SampleRate;
        for (var frame = 0; frame < frames; frame++)
        {
            var scale = gain;
            if (fadeInFrames > 0 && frame < fadeInFrames) scale *= frame / fadeInFrames;
            if (fadeOutFrames > 0 && frames - 1 - frame < fadeOutFrames) scale *= (frames - 1 - frame) / fadeOutFrames;
            for (var channel = 0; channel < format.Channels; channel++)
            {
                var from = format.DataStart + (first + frame) * format.FrameBytes + channel * format.Bits / 8;
                var to = frame * format.FrameBytes + channel * format.Bits / 8;
                if (format.Bits == 8)
                {
                    data[to] = (byte)Math.Clamp(Math.Round((wav[from] - 128) * scale) + 128, 0, 255);
                }
                else
                {
                    var sample = (short)Math.Clamp(Math.Round(BitConverter.ToInt16(wav, from) * scale), short.MinValue, short.MaxValue);
                    data[to] = (byte)sample;
                    data[to + 1] = (byte)(sample >> 8);
                }
            }
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length + (data.Length & 1));
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)format.Channels);
        writer.Write(format.SampleRate);
        writer.Write(format.SampleRate * format.FrameBytes);
        writer.Write((short)format.FrameBytes);
        writer.Write((short)format.Bits);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        if ((data.Length & 1) == 1) writer.Write((byte)0);
        return stream.ToArray();
    }

    /// <summary>A silent 16 bit mono WAV of the given length, the template of a new sound.</summary>
    public static byte[] Silence(double seconds, int rate = 22050)
    {
        var data = new byte[(int)Math.Max(1, Math.Round(seconds * rate)) * 2];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }
}
