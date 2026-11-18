using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>A WAV deeper than 16 bit is taken down to 16 bit PCM: rounded, clipped, with the channels and rate kept.</summary>
public sealed class WavDepthTests
{
    private static byte[] Wav(int tag, int bits, int channels, int rate, byte[] data, bool extensible = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + (extensible ? 24 : 0) + data.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(extensible ? 40 : 16);
        writer.Write((short)(extensible ? 0xFFFE : tag));
        writer.Write((short)channels);
        writer.Write(rate);
        writer.Write(rate * channels * bits / 8);
        writer.Write((short)(channels * bits / 8));
        writer.Write((short)bits);
        if (extensible)
        {
            writer.Write((short)22);
            writer.Write((short)bits);
            writer.Write(3);
            writer.Write((short)tag); // the first two bytes of the sub format GUID
            writer.Write(new byte[14]);
        }

        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    private static byte[] Pcm24(params int[] samples) => [.. samples.SelectMany(s => new[] { (byte)s, (byte)(s >> 8), (byte)(s >> 16) })];

    private static short[] Samples(byte[] wav)
    {
        var format = WavEdit.Read(wav);
        return [.. Enumerable.Range(0, format.Frames * format.Channels).Select(i => BitConverter.ToInt16(wav, format.DataStart + i * 2))];
    }

    [Fact]
    public void A24BitWavBecomes16BitWithRoundingClippingAndTheSameFormat()
    {
        // 0x000080 is half a step: rounds up; 0x00007F rounds down; the top value must not wrap; negatives round the same way.
        var wav = Wav(1, 24, 2, 44100, Pcm24(0, 0x80, 0x7F, 0x180, 0x7FFFFF, -0x800000, -0x80, -0x81));
        var converted = WavEdit.To16Bit(wav);
        var format = WavEdit.Read(converted);
        Assert.Equal((2, 44100, 16), (format.Channels, format.SampleRate, format.Bits));
        Assert.Equal<short>([0, 1, 0, 2, 32767, -32768, 0, -1], Samples(converted));
    }

    [Fact]
    public void AnExtensible24BitAndA32BitFloatWavAreConvertedAndOtherWavsAreKept()
    {
        var extensible = Wav(1, 24, 1, 8000, Pcm24(0x400000), extensible: true);
        Assert.Equal<short>([16384], Samples(WavEdit.To16Bit(extensible)));
        var floats = Wav(3, 32, 1, 8000, [.. new[] { 0.5f, -1f, 2f }.SelectMany(BitConverter.GetBytes)]);
        Assert.Equal<short>([16384, -32767, 32767], Samples(WavEdit.To16Bit(floats)));
        var plain = Wav(1, 16, 1, 8000, [1, 0, 2, 0]);
        Assert.Same(plain, WavEdit.To16Bit(plain));
        Assert.True(WavEdit.IsDeeperThan16(floats));
        Assert.False(WavEdit.IsDeeperThan16(plain));
        Assert.False(WavEdit.IsDeeperThan16([1, 2, 3]));
    }

    [Fact]
    public void TheCodecDecodesAndConvertsADeepWavSoItCanBeEdited()
    {
        var wav = Wav(1, 24, 1, 22050, Pcm24(0x400000, -0x400000));
        var audio = AudioCodec.Decode(wav);
        Assert.Equal((1, 22050, 2), (audio.Channels, audio.SampleRate, audio.Samples.Length));
        Assert.Equal(0.5f, audio.Samples[0], 3);
        Assert.Equal(16, WavEdit.Read(AudioCodec.Convert(wav, "wav")).Bits); // a WAV stored as WAV is no longer kept as it is when deeper than 16 bit
        Assert.Equal("ogg", SoundAudio.Format(AudioCodec.Convert(wav, "ogg")));
    }
}
