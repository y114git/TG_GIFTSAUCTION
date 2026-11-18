using System.Text.Json;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>The recording buffer (gain, clipping, limit, level), the fake microphone the recorder tests use, and the remembered microphone of the audio options.</summary>
public sealed class RecordingTakeTests
{
    private static short[] Constant(short value, int frames) => [.. Enumerable.Repeat(value, frames)];

    [Fact]
    public void BlocksAreAppendedInOrderAndAudioHasThemAsMono44100()
    {
        var take = new RecordingTake();
        take.Append([1000, -2000, 3000]);
        take.Append([4000]);
        Assert.Equal(4, take.Frames);
        var audio = take.ToAudio();
        Assert.Equal((1, 44100, 4), (audio.Channels, audio.SampleRate, audio.Frames));
        Assert.Equal([1000 / 32768f, -2000 / 32768f, 3000 / 32768f, 4000 / 32768f], audio.Samples);
        Assert.Equal(4000 / 32768f, take.Peak, 5); // the loudest sample of the last block
        Assert.Equal(2, take.Last(2).Frames);
        Assert.Equal(4, take.Last(100).Frames); // fewer than asked for when the take is shorter
        Assert.Equal(0, new RecordingTake().Last(10).Frames);
    }

    [Fact]
    public void GainScalesAndNeverWrapsAndClippingIsRememberedUntilReset()
    {
        var take = new RecordingTake { Gain = 2 };
        take.Append([10000]);
        Assert.False(take.Clipped);
        Assert.Equal(20000 / 32768f, take.ToAudio().Samples[0], 5);
        take.Append([20000, -20000]); // 40000 would wrap a 16 bit sample
        Assert.True(take.Clipped);
        Assert.Equal([32767 / 32768f, -1f], take.ToAudio().Samples[1..], new FloatComparer()); // held at full scale, not wrapped
        take.ResetClipping();
        Assert.False(take.Clipped);
        take.Gain = 0.5;
        take.Append([32767]); // quieter: nothing reaches full scale
        Assert.False(take.Clipped);
        take.Gain = 4;
        take.Append([-8192]); // exactly the negative full scale after the gain
        Assert.True(take.Clipped);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(-3, 0)]
    [InlineData(99, RecordingTake.MaxGain)]
    public void AGainOutOfRangeOrNotANumberIsBroughtBackAndNothingBecomesNaN(double gain, double expected)
    {
        var take = new RecordingTake { Gain = gain };
        Assert.Equal(expected, take.Gain);
        take.Append(Constant(12345, 100));
        Assert.All(take.ToAudio().Samples, s => Assert.True(float.IsFinite(s)));
        Assert.True(float.IsFinite(take.Peak));
    }

    [Fact]
    public void TheLimitStopsTheTakeAndTheBlockThatCrossesItIsCutThere()
    {
        var take = new RecordingTake { LimitFrames = 1000 };
        Assert.Equal(600, take.Append(Constant(5, 600)));
        Assert.False(take.Full);
        Assert.Equal(400, take.Append(Constant(5, 700))); // 300 do not fit
        Assert.True(take.Full);
        Assert.Equal(0, take.Append(Constant(5, 10)));
        Assert.Equal(1000, take.Frames);
        take.Clear();
        Assert.Equal((0, false, false), (take.Frames, take.Full, take.Clipped));
        Assert.Equal(10, take.Append(Constant(1, 10))); // and it takes blocks again
    }

    [Fact]
    public void ThereIsNoTimeLimitOnlyThePlaceOfAWavCanHold()
    {
        var take = new RecordingTake();
        Assert.Equal(RecordingTake.MaxFrames, take.LimitFrames);
        Assert.True(RecordingTake.MaxFrames > 3 * 3600 * RecordingTake.SampleRate); // hours, not minutes
        Assert.True((long)RecordingTake.MaxFrames * sizeof(short) < int.MaxValue); // the data of the WAV keeps its size in an int
    }

    [Fact]
    public void ASampleFollowsASampleAcrossTheChunksAndNothingIsCopiedWhenTheTakeGrows()
    {
        var take = new RecordingTake();
        var all = Enumerable.Range(0, RecordingTake.ChunkFrames * 2 + 5000).Select(i => (short)(i % 20000)).ToArray();
        for (var at = 0; at < all.Length; at += 44100) take.Append(all.AsSpan(at, Math.Min(44100, all.Length - at))); // blocks that cut the chunks anywhere
        Assert.Equal(all.Length, take.Frames);
        Assert.Equal(all.Length * 2L, take.Bytes); // two bytes a frame and nothing more
        var audio = take.ToAudio();
        Assert.Equal(all.Length, audio.Frames);
        Assert.Equal(all.Select(s => s / 32768f), audio.Samples);
        var tail = take.Last(RecordingTake.ChunkFrames + 100); // across a chunk border
        Assert.Equal(all[^(RecordingTake.ChunkFrames + 100)..].Select(s => s / 32768f), tail.Samples);
        Assert.Equal(all.Length, take.Frames);
    }

    [Fact]
    public void LettingGoWhileCopyingGivesTheSameClipAndEmptiesTheTake()
    {
        var take = new RecordingTake();
        take.Append(Constant(3000, RecordingTake.ChunkFrames + 10));
        var clip = take.ToAudio(release: true);
        Assert.Equal(RecordingTake.ChunkFrames + 10, clip.Frames);
        Assert.All(clip.Samples, s => Assert.Equal(3000 / 32768f, s, 5));
        Assert.Equal((RecordingTake.ChunkFrames + 10, 0L, 0), (take.Frames, take.Bytes, take.Last(10).Frames)); // the counts stay, the samples are gone
        take.Clear();
        take.Append(Constant(1, 10));
        Assert.Equal(10, take.ToAudio().Frames);
    }

    [Fact]
    public void TheLevelOfTheLastMomentsIsWhatTheMeterReads()
    {
        var take = new RecordingTake();
        take.Append(Constant(0, 4410));
        take.Append(Constant(16384, 2205)); // 50 ms at half scale
        var tail = take.Last(4096);
        var levels = AudioAnalysis.Levels(tail, tail.Seconds - 0.025);
        Assert.Equal(0.5f, levels[0].Peak, 3);
        Assert.Equal(0.5f, levels[0].Rms, 3);
        var silent = new RecordingTake();
        silent.Append(Constant(0, 5000));
        Assert.Equal(0, AudioAnalysis.Levels(silent.Last(4096), 0.05)[0].Peak);
        Assert.Equal(0, AudioAnalysis.Levels(new RecordingTake().ToAudio(), 0)[0].Peak); // an empty take has no level and no error
    }

    [Fact]
    public void ThePartOfARegionIsWhatTheEditorKeeps()
    {
        var take = new RecordingTake();
        for (var i = 0; i < 4; i++) take.Append(Constant((short)((i + 1) * 1000), 11025)); // four quarters of a second
        var kept = AudioCodec.Edit(take.ToAudio(), 0.25, 0.75);
        Assert.Equal(22050, kept.Frames);
        Assert.Equal(2000 / 32768f, kept.Samples[0], 5);
        Assert.Equal(3000 / 32768f, kept.Samples[^1], 5);
        var wav = AudioCodec.Encode(kept, "wav");
        Assert.Equal((1, 44100, 16, 22050), (WavEdit.Read(wav).Channels, WavEdit.Read(wav).SampleRate, WavEdit.Read(wav).Bits, WavEdit.Read(wav).Frames));
    }

    [Fact]
    public void TheFakeMicrophoneDeliversOnlyWhileStartedAndFailsOnRequest()
    {
        using var mic = new FakeAudioCapture();
        mic.Feed(1, 2, 3); // not started: dropped
        var buffer = new short[10];
        Assert.Equal(0, mic.Read(buffer));
        mic.Start();
        mic.Feed(1, 2, 3, 4, 5);
        Assert.Equal(3, mic.Read(buffer.AsSpan(0, 3)));
        Assert.Equal(2, mic.Read(buffer));
        mic.Tone(0.1, 0.5);
        Assert.Equal(4410, mic.Read(new short[8000]));
        mic.Stop();
        Assert.False(mic.IsCapturing);
        mic.Fail(CaptureFailure.Lost);
        Assert.Equal(CaptureFailure.Lost, Assert.Throws<CaptureException>(() => mic.Read(buffer)).Failure);
        mic.Dispose();
        Assert.True(mic.Disposed);
        Assert.Throws<ObjectDisposedException>(mic.Start);
    }

    [Fact]
    public void AClosedMicrophoneIsLostAndNotSilent()
    {
        var mic = new FakeAudioCapture();
        mic.Dispose();
        Assert.Equal(CaptureFailure.Lost, Assert.Throws<CaptureException>(() => mic.Read(new short[10])).Failure);
    }

    [Fact]
    public void ReleasingTheSamplesKeepsTheCountsAndClearMakesTheTakeUsableAgain()
    {
        var take = new RecordingTake();
        take.Append(Constant(1000, 5000));
        take.Release();
        Assert.Equal((5000, 0), (take.Frames, take.Last(100).Frames)); // the clip made of it is the take from here on
        Assert.Equal(0, take.ToAudio().Frames);
        take.Clear();
        take.Append(Constant(2000, 10));
        Assert.Equal(2000 / 32768f, take.ToAudio().Samples[9], 5);
    }

    [Fact]
    public void AnOpenThatTheDriverRefusedNamesTheRealCause()
    {
        Assert.Equal(CaptureFailure.AccessDenied, OpenAlCaptureSource.OpenFailure(new(["Mic"], "Mic", CaptureFailure.AccessDenied))); // not "busy"
        Assert.Equal(CaptureFailure.AccessDenied, OpenAlCaptureSource.OpenFailure(new([], "", CaptureFailure.AccessDenied)));
        Assert.Equal(CaptureFailure.NoDevice, OpenAlCaptureSource.OpenFailure(new([], "", CaptureFailure.NoDevice)));
        Assert.Equal(CaptureFailure.Busy, OpenAlCaptureSource.OpenFailure(new(["Mic"], "Mic")));
    }

    [Fact]
    public void TheFakeSourceListsAndOpensAndRefusesAsTold()
    {
        var source = new FakeCaptureSource { Default = "B" };
        source.Names.AddRange(["A", "B"]);
        var list = source.List();
        Assert.Equal(["A", "B"], list.Names);
        Assert.Equal(("B", null), (list.Default, list.Problem));
        var device = Assert.IsType<FakeAudioCapture>(source.Open("A"));
        Assert.Same(device, source.Device);
        source.OpenFails = CaptureFailure.AccessDenied;
        Assert.Equal(CaptureFailure.AccessDenied, Assert.Throws<CaptureException>(() => source.Open("B")).Failure);
        Assert.Equal(["A"], source.Opened);
    }

    [Fact]
    public void AnOldSettingsFileWithoutTheMicrophoneGetsTheDefaultAndANullNameIsRepaired()
    {
        var old = JsonSerializer.Deserialize<AudioOptions>("""{"Backend":"openal","Device":"Speakers","Volume":50}""")!;
        Assert.Equal("", old.CaptureDevice);
        Assert.Equal("Speakers", old.Device); // the rest is untouched
        var saved = JsonSerializer.Deserialize<AudioOptions>(JsonSerializer.Serialize(old with { CaptureDevice = "USB Mic" }))!;
        Assert.Equal("USB Mic", saved.CaptureDevice);
        Assert.Equal("", JsonSerializer.Deserialize<AudioOptions>("""{"CaptureDevice":null}""")!.Clamped().CaptureDevice);
    }

    private sealed class FloatComparer : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => Math.Abs(x - y) < 1e-4;

        public int GetHashCode(float value) => 0;
    }
}
