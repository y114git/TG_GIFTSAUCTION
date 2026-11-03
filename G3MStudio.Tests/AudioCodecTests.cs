using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class AudioCodecTests
{
    private static PcmAudio Tone(int channels = 2, int rate = 22050, double seconds = 1)
    {
        var frames = (int)(rate * seconds);
        var samples = new float[frames * channels];
        for (var i = 0; i < frames; i++)
            for (var c = 0; c < channels; c++) samples[i * channels + c] = (float)(Math.Sin(i * 2 * Math.PI * (440 + c * 110) / rate) * 0.5);
        return new PcmAudio(samples, channels, rate);
    }

    private static double Rms(PcmAudio audio) => Math.Sqrt(audio.Samples.Sum(x => (double)x * x) / audio.Samples.Length);

    [Fact]
    public void AToneSurvivesAnOggRoundTripWithItsLengthChannelsAndLoudness()
    {
        var tone = Tone();
        var ogg = AudioCodec.Encode(tone, "ogg");
        Assert.Equal("ogg", SoundAudio.Format(ogg));
        var back = AudioCodec.Decode(ogg);
        Assert.Equal((2, 22050), (back.Channels, back.SampleRate));
        Assert.InRange(back.Seconds, 0.95, 1.1);
        Assert.InRange(Rms(back) / Rms(tone), 0.85, 1.15);
        Assert.InRange(SoundAudio.Seconds(ogg)!.Value, 0.95, 1.1); // the header reader agrees with the decoder
    }

    [Fact]
    public void AWavRoundTripIsExactToTheSampleAndConversionsGoBothWays()
    {
        var tone = Tone(1);
        var wav = AudioCodec.Encode(tone, "wav");
        var back = AudioCodec.Decode(wav);
        Assert.Equal(tone.Frames, back.Frames);
        Assert.All(tone.Samples.Zip(back.Samples), pair => Assert.InRange(Math.Abs(pair.First - pair.Second), 0, 1.0 / 16000));
        var toOgg = AudioCodec.Convert(wav, "ogg");
        Assert.Equal("ogg", SoundAudio.Format(toOgg));
        Assert.Same(toOgg, AudioCodec.Convert(toOgg, "ogg")); // already that kind: kept as it is
        Assert.Equal("wav", SoundAudio.Format(AudioCodec.Convert(toOgg, "wav")));
        Assert.Throws<InvalidOperationException>(() => AudioCodec.Decode([1, 2, 3]));
    }

    [Fact]
    public void AnyAudioIsCutScaledAndFadedInTheSampleDomain()
    {
        var tone = Tone(2);
        var cut = AudioCodec.Edit(tone, 0.25, 0.75);
        Assert.Equal(0.5, cut.Seconds, 2);
        Assert.InRange(Rms(AudioCodec.Edit(tone, 0, 1, gain: 0.5)) / Rms(tone), 0.49, 0.51);
        var loud = AudioCodec.Edit(tone, 0, 1, gain: 16);
        Assert.All(loud.Samples, x => Assert.InRange(x, -1f, 1f)); // clipped, not wrapped
        var faded = AudioCodec.Edit(tone, 0, 1, fadeIn: 0.5, fadeOut: 0.5);
        Assert.Equal(0, faded.Samples[0]);
        Assert.Equal(0, faded.Samples[^1]);
        Assert.Throws<InvalidOperationException>(() => AudioCodec.Edit(tone, 1, 1));
    }

    [Fact]
    public async Task AnOggSoundOfARealGameDecodesToItsOwnLength()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var sound = data.Sounds.FirstOrDefault(s => s.AudioFile is { Data.Length: > 5000 and < 3000000 } a && SoundAudio.Format(a.Data) == "ogg");
            if (sound is null) continue;
            var decoded = AudioCodec.Decode(sound.AudioFile!.Data);
            Assert.InRange(decoded.Seconds, SoundAudio.Seconds(sound.AudioFile.Data)!.Value - 0.2, SoundAudio.Seconds(sound.AudioFile.Data)!.Value + 0.2);
            var edited = AudioCodec.Encode(AudioCodec.Edit(decoded, 0, Math.Min(0.5, decoded.Seconds)), "ogg");
            Assert.InRange(AudioCodec.Decode(edited).Seconds, 0.3, 0.6);
            return;
        }

        Assert.Skip("No corpus game has an embedded OGG sound of a usable size.");
    }

    [Fact]
    public void AnAudioFileNamedByTheEnvironmentDecodesEditsAndConvertsToBothStoredKinds()
    {
        // G3MSTUDIO_AUDIO_FILE names a real OGG, MP3 or WAV file; the test skips without it.
        var path = Environment.GetEnvironmentVariable("G3MSTUDIO_AUDIO_FILE");
        Assert.SkipWhen(string.IsNullOrEmpty(path) || !File.Exists(path), "G3MSTUDIO_AUDIO_FILE is not set");
        var bytes = File.ReadAllBytes(path);
        var decoded = AudioCodec.Decode(bytes);
        Assert.True(decoded.Seconds > 0.1 && decoded.Channels is 1 or 2, $"{SoundAudio.Format(bytes)} {decoded.Seconds} s {decoded.Channels} ch {decoded.SampleRate} Hz");
        var ogg = AudioCodec.Convert(bytes, "ogg");
        var wav = AudioCodec.Convert(bytes, "wav");
        Assert.InRange(AudioCodec.Decode(ogg).Seconds, decoded.Seconds - 0.2, decoded.Seconds + 0.2);
        Assert.InRange(AudioCodec.Decode(wav).Seconds, decoded.Seconds - 0.01, decoded.Seconds + 0.01);
        var half = AudioCodec.Edit(decoded, 0, decoded.Seconds / 2, fadeOut: 0.1);
        Assert.InRange(half.Seconds, decoded.Seconds / 2 - 0.01, decoded.Seconds / 2 + 0.01);
    }

    /// <summary>
    /// About 1.04 seconds of silent MP3 made by hand: 40 frames of MPEG-1 Layer III, 128 kbit/s, 44.1 kHz, stereo, 417 bytes each. A frame is the header FF FB 90 00 and zero
    /// bytes, which decodes to silence, so no encoder is needed to get a real MP3 stream.
    /// </summary>
    private static byte[] SilentMp3()
    {
        var bytes = new List<byte>();
        for (var frame = 0; frame < 40; frame++)
        {
            bytes.AddRange([0xFF, 0xFB, 0x90, 0x00]);
            bytes.AddRange(new byte[417 - 4]);
        }

        return [.. bytes];
    }

    [Fact]
    public void AnMp3StreamDecodesAndConvertsToTheTwoStoredKinds()
    {
        var mp3 = SilentMp3();
        Assert.Equal("mp3", SoundAudio.Format(mp3));
        var decoded = AudioCodec.Decode(mp3);
        Assert.Equal((2, 44100), (decoded.Channels, decoded.SampleRate));
        Assert.InRange(decoded.Seconds, 0.95, 1.1);
        Assert.All(decoded.Samples, x => Assert.InRange(x, -0.001f, 0.001f)); // silence
        var ogg = AudioCodec.Convert(mp3, "ogg");
        Assert.Equal("ogg", SoundAudio.Format(ogg));
        Assert.InRange(AudioCodec.Decode(ogg).Seconds, 0.95, 1.15);
        var wav = AudioCodec.Convert(mp3, "wav");
        Assert.InRange(WavEdit.Read(wav).Seconds, 0.95, 1.1);
        Assert.Throws<InvalidOperationException>(() => AudioCodec.Decode([0xFF, 0xFB, 1, 2, 3])); // a cut off stream is refused with a message, not an exception of the library
    }
}
