using NLayer;
using NVorbis;
using OggVorbisEncoder;

namespace G3MStudio.Core.Workspace;

/// <summary>Decoded audio: interleaved samples between -1 and 1.</summary>
public sealed record PcmAudio(float[] Samples, int Channels, int SampleRate)
{
    public int Frames => Samples.Length / Math.Max(1, Channels);

    public double Seconds => (double)Frames / Math.Max(1, SampleRate);
}

/// <summary>
/// Reads WAV, OGG Vorbis and MP3 (the formats GameMaker takes) into samples, edits them, and writes WAV or OGG Vorbis (the formats a data file stores). Managed code only,
/// so it needs no program installed and works the same on every platform.
/// </summary>
public static class AudioCodec
{
    private const int MaxSamples = 64 * 1024 * 1024; // 256 MB of floats: a sound of about 25 minutes of stereo at 44.1 kHz

    public static PcmAudio Decode(byte[] bytes) => SoundAudio.Format(bytes) switch
    {
        "wav" => DecodeWav(bytes),
        "ogg" => DecodeOgg(bytes),
        "mp3" => DecodeMp3(bytes),
        _ => throw new InvalidOperationException("The audio is not a WAV, OGG or MP3 file."),
    };

    private static PcmAudio DecodeWav(byte[] wav)
    {
        var format = WavEdit.Read(wav);
        var count = format.Frames * format.Channels;
        if (count > MaxSamples) throw new InvalidOperationException("The audio is too long to edit.");
        var samples = new float[count];
        for (var i = 0; i < count; i++)
            samples[i] = format.Bits == 8 ? (wav[format.DataStart + i] - 128) / 128f : BitConverter.ToInt16(wav, format.DataStart + i * 2) / 32768f;
        return new PcmAudio(samples, format.Channels, format.SampleRate);
    }

    private static PcmAudio DecodeOgg(byte[] bytes)
    {
        try
        {
            using var reader = new VorbisReader(new MemoryStream(bytes), true);
            return Read(reader.Channels, reader.SampleRate, (buffer, count) => reader.ReadSamples(buffer, 0, count));
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or NotSupportedException or EndOfStreamException)
        {
            throw new InvalidOperationException("The OGG file could not be read: " + e.Message, e);
        }
    }

    private static PcmAudio DecodeMp3(byte[] bytes)
    {
        try
        {
            using var file = new MpegFile(new MemoryStream(bytes));
            return Read(file.Channels, file.SampleRate, (buffer, count) => file.ReadSamples(buffer, 0, count));
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or NotSupportedException or EndOfStreamException)
        {
            throw new InvalidOperationException("The MP3 file could not be read: " + e.Message, e);
        }
    }

    private static PcmAudio Read(int channels, int rate, Func<float[], int, int> read)
    {
        var all = new List<float>();
        var buffer = new float[Math.Max(1, channels) * 4096];
        int got;
        while ((got = read(buffer, buffer.Length)) > 0)
        {
            all.AddRange(buffer.AsSpan(0, got));
            if (all.Count > MaxSamples) throw new InvalidOperationException("The audio is too long to edit.");
        }

        return new PcmAudio([.. all], channels, rate);
    }

    /// <summary>The part from <paramref name="start"/> to <paramref name="end"/> seconds, times <paramref name="gain"/> (clipped at full scale), with a linear fade in and out.</summary>
    public static PcmAudio Edit(PcmAudio audio, double start, double end, double gain = 1, double fadeIn = 0, double fadeOut = 0)
    {
        var first = (int)Math.Clamp(Math.Round(start * audio.SampleRate), 0, audio.Frames);
        var last = (int)Math.Clamp(Math.Round(end * audio.SampleRate), first, audio.Frames);
        if (last - first < 1) throw new InvalidOperationException("The cut would leave no audio.");
        if (gain is < 0 or > 16) throw new InvalidOperationException("The gain must be between 0 and 1600 percent.");
        var frames = last - first;
        var samples = new float[frames * audio.Channels];
        double fadeInFrames = Math.Max(0, fadeIn) * audio.SampleRate, fadeOutFrames = Math.Max(0, fadeOut) * audio.SampleRate;
        for (var frame = 0; frame < frames; frame++)
        {
            var scale = gain;
            if (fadeInFrames > 0 && frame < fadeInFrames) scale *= frame / fadeInFrames;
            if (fadeOutFrames > 0 && frames - 1 - frame < fadeOutFrames) scale *= (frames - 1 - frame) / fadeOutFrames;
            for (var channel = 0; channel < audio.Channels; channel++)
                samples[frame * audio.Channels + channel] = (float)Math.Clamp(audio.Samples[(first + frame) * audio.Channels + channel] * scale, -1, 1);
        }

        return new PcmAudio(samples, audio.Channels, audio.SampleRate);
    }

    /// <summary>The audio as the stored kind: "wav" (16 bit) or "ogg" (Vorbis).</summary>
    public static byte[] Encode(PcmAudio audio, string kind) => kind switch
    {
        "wav" => EncodeWav(audio),
        "ogg" => EncodeOgg(audio, 0.5f),
        _ => throw new InvalidOperationException("A sound is stored as WAV or OGG."),
    };

    /// <summary>The bytes of any supported audio file as the kind a sound stores (a file that already is that kind is kept as it is).</summary>
    public static byte[] Convert(byte[] bytes, string kind) => SoundAudio.Format(bytes) == kind ? bytes : Encode(Decode(bytes), kind);

    private static byte[] EncodeWav(PcmAudio audio)
    {
        var data = new byte[audio.Samples.Length * 2];
        for (var i = 0; i < audio.Samples.Length; i++)
        {
            var sample = (short)Math.Clamp(Math.Round(audio.Samples[i] * 32767.0), short.MinValue, short.MaxValue);
            data[i * 2] = (byte)sample;
            data[i * 2 + 1] = (byte)(sample >> 8);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)audio.Channels);
        writer.Write(audio.SampleRate);
        writer.Write(audio.SampleRate * audio.Channels * 2);
        writer.Write((short)(audio.Channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    private static byte[] EncodeOgg(PcmAudio audio, float quality)
    {
        var info = VorbisInfo.InitVariableBitRate(audio.Channels, audio.SampleRate, quality);
        // The serial number only has to differ between streams of one file; there is one, so the number does not matter.
        var stream = new OggStream(1);
        using var output = new MemoryStream();
        var comments = new Comments();
        stream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        stream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
        stream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        Flush(stream, output, true);

        var state = ProcessingState.Create(info);
        const int block = 4096;
        var channels = audio.Channels;
        for (var at = 0; at < audio.Frames; at += block)
        {
            var count = Math.Min(block, audio.Frames - at);
            var planar = new float[channels][];
            for (var c = 0; c < channels; c++)
            {
                planar[c] = new float[count];
                for (var i = 0; i < count; i++) planar[c][i] = audio.Samples[(at + i) * channels + c];
            }

            state.WriteData(planar, count);
            while (!stream.Finished && state.PacketOut(out var packet))
            {
                stream.PacketIn(packet);
                Flush(stream, output, false);
            }
        }

        state.WriteEndOfStream();
        while (!stream.Finished && state.PacketOut(out var last))
        {
            stream.PacketIn(last);
            Flush(stream, output, false);
        }

        Flush(stream, output, true);
        return output.ToArray();
    }

    private static void Flush(OggStream stream, MemoryStream output, bool force)
    {
        while (stream.PageOut(out var page, force))
        {
            output.Write(page.Header, 0, page.Header.Length);
            output.Write(page.Body, 0, page.Body.Length);
        }
    }
}
