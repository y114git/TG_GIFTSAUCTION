using G3MStudio.Core.Audio;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>The sound generator: its length, range, silent ends, repeatability, presets, shapes and extreme parameters.</summary>
public sealed class SoundSynthTests
{
    private static float Peak(PcmAudio audio) => audio.Samples.Max(Math.Abs);

    public static TheoryData<SynthPreset> Presets => [.. Enum.GetValues<SynthPreset>()];

    public static TheoryData<SynthShape> Shapes => [.. Enum.GetValues<SynthShape>()];

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.02)]
    [InlineData(2.0)]
    public void TheSampleCountIsTheDurationAtFortyFourThousandOneHundredHertzMono(double seconds)
    {
        var audio = SoundSynth.Generate(new SynthSettings { Duration = seconds });
        Assert.Equal((int)Math.Round(seconds * 44100), audio.Samples.Length);
        Assert.Equal((1, 44100), (audio.Channels, audio.SampleRate));
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void EveryPresetIsAudibleWithinRangeAndStartsAndEndsAtZero(SynthPreset preset)
    {
        var audio = SoundSynth.Generate(SoundSynth.Preset(preset));
        Assert.True(Peak(audio) > 0.1f, $"{preset} is silent");
        Assert.All(audio.Samples, s => Assert.InRange(s, -1f, 1f));
        Assert.Equal(0f, audio.Samples[0]); // the envelope starts at silence
        Assert.True(Math.Abs(audio.Samples[^1]) < 0.01f, $"{preset} ends at {audio.Samples[^1]}");
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryWaveShapeIsAudibleAndEndsWithoutAClick(SynthShape shape)
    {
        var audio = SoundSynth.Generate(new SynthSettings { Shape = shape, Duty = 0.2, Frequency = 330, LowPass = 0, Attack = 0.05, Sustain = 0.1, Decay = 0.1, Duration = 0.25 });
        Assert.True(Peak(audio) > 0.3f, $"{shape} is silent");
        Assert.Equal(0f, audio.Samples[0]);
        Assert.True(Math.Abs(audio.Samples[^1]) < 0.01f);
    }

    [Fact]
    public void AnEnvelopeThatIsTooLongIsShortenedAndNoEndClicksEvenWithoutAnAttackOrDecay()
    {
        var audio = SoundSynth.Generate(new SynthSettings { Shape = SynthShape.Square, Attack = 0, Decay = 0, Sustain = 5, Duration = 0.3 });
        Assert.Equal(0f, audio.Samples[0]);
        Assert.True(Math.Abs(audio.Samples[^1]) < 0.01f); // the shortest attack and decay still apply
        Assert.True(Peak(audio) > 0.5f);
        var wide = SoundSynth.Generate(new SynthSettings { Attack = 4, Sustain = 4, Decay = 4, Duration = 0.5 });
        Assert.True(Math.Abs(wide.Samples[^1]) < 0.01f); // all three are shortened together
    }

    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(10, 10, 0)]
    [InlineData(0.002, 10, 10)]
    [InlineData(0, 0, 10)]
    public void AVeryShortSoundStillFadesInAndOutWhateverTheEnvelope(double attack, double sustain, double decay)
    {
        var audio = SoundSynth.Generate(new SynthSettings { Shape = SynthShape.Square, Volume = 1, Attack = attack, Sustain = sustain, Decay = decay, Duration = SoundSynth.MinDuration });
        Assert.True(Math.Abs(audio.Samples[^1]) < 0.001f, $"last sample {audio.Samples[^1]}");
        Assert.True(Math.Abs(audio.Samples[0]) < 0.001f);
        Assert.True(Peak(audio) > 0.5f);
    }

    [Fact]
    public void TheSameSettingsAndSeedGiveTheSameBytesAndAnotherSeedChangesNoise()
    {
        var noise = new SynthSettings { Shape = SynthShape.Noise, Seed = 5, Duration = 0.2 };
        Assert.Equal(SoundSynth.ToWav(noise), SoundSynth.ToWav(noise));
        Assert.NotEqual(SoundSynth.ToWav(noise), SoundSynth.ToWav(noise with { Seed = 6 }));
        Assert.Equal(SoundSynth.ToWav(SoundSynth.Random(42)), SoundSynth.ToWav(SoundSynth.Random(42)));
        Assert.Equal(SoundSynth.Random(42), SoundSynth.Random(42)); // the settings too
        Assert.NotEqual(SoundSynth.Random(42), SoundSynth.Random(43));
        Assert.Equal(SoundSynth.ToWav(new SynthSettings { Shape = SynthShape.Sine, Seed = 1 }), SoundSynth.ToWav(new SynthSettings { Shape = SynthShape.Sine, Seed = 99 })); // a seed only matters for noise
    }

    [Fact]
    public void TheWavIsAMono16BitPcmFileThatReadsBackToTheSameSamples()
    {
        var wav = SoundSynth.ToWav(SoundSynth.Preset(SynthPreset.Coin));
        Assert.Equal("wav", SoundAudio.Format(wav));
        var format = WavEdit.Read(wav);
        Assert.Equal((1, 44100, 16), (format.Channels, format.SampleRate, format.Bits));
        var back = AudioCodec.Decode(wav);
        var original = SoundSynth.Generate(SoundSynth.Preset(SynthPreset.Coin));
        Assert.Equal(original.Samples.Length, back.Samples.Length);
        Assert.Equal(0, original.Samples.Zip(back.Samples, (a, b) => Math.Abs(a - b)).Max(), 3);
        Assert.Equal(original.Seconds, SoundAudio.Seconds(wav)!.Value, 3);
    }

    [Fact]
    public void ARandomSoundIsAudibleAndInRangeForManySeeds()
    {
        for (var seed = -50; seed < 250; seed++)
        {
            var audio = SoundSynth.Generate(SoundSynth.Random(seed));
            Assert.True(Peak(audio) > 0.02f, $"seed {seed} is silent");
            Assert.All(audio.Samples, s => Assert.InRange(s, -1f, 1f));
            Assert.True(Math.Abs(audio.Samples[^1]) < 0.01f, $"seed {seed} clicks at the end");
        }
    }

    [Fact]
    public void ExtremeAndNonNumberParametersNeverProduceNaNOrOutOfRangeSamples()
    {
        double[] bad = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1e300, 1e300, 0, -1, 1e-300];
        foreach (var value in bad)
        {
            var settings = new SynthSettings { Duty = value, Frequency = value, Slide = value, VibratoDepth = value, VibratoSpeed = value, Attack = value, Sustain = value, Decay = value, Duration = value, Volume = value, LowPass = value };
            foreach (var shape in Enum.GetValues<SynthShape>())
            {
                var audio = SoundSynth.Generate(settings with { Shape = shape });
                Assert.InRange(audio.Samples.Length, (int)(SoundSynth.MinDuration * 44100), (int)(SoundSynth.MaxDuration * 44100));
                Assert.All(audio.Samples, s => Assert.True(float.IsFinite(s) && s is >= -1 and <= 1, $"{shape} with {value}: {s}"));
            }
        }

        Assert.All(SoundSynth.Generate(new SynthSettings { Shape = (SynthShape)99 }).Samples, s => Assert.True(float.IsFinite(s))); // a shape that is not one
        Assert.True(Peak(SoundSynth.Generate(new SynthSettings { Slide = 8, Frequency = 20000, VibratoDepth = 12, VibratoSpeed = 40, Duration = 1 })) > 0); // the top of every range together
        Assert.True(SoundSynth.Limited(new SynthSettings { LowPass = 10 }).LowPass == 0); // a cut off that would mute the sound is off
    }

    [Fact]
    public void VolumeScalesTheLevelAndALowPassTakesOutTheHighFrequencies()
    {
        var loud = Peak(SoundSynth.Generate(new SynthSettings { Shape = SynthShape.Sine, Volume = 1, Sustain = 0.3 }));
        var quiet = Peak(SoundSynth.Generate(new SynthSettings { Shape = SynthShape.Sine, Volume = 0.25, Sustain = 0.3 }));
        Assert.Equal(0.25, quiet / loud, 2);
        Assert.Equal(0f, Peak(SoundSynth.Generate(new SynthSettings { Volume = 0 })));

        var bright = new SynthSettings { Shape = SynthShape.Sine, Frequency = 8000, Volume = 1, Sustain = 0.3 };
        Assert.True(Peak(SoundSynth.Generate(bright with { LowPass = 400 })) < Peak(SoundSynth.Generate(bright)) / 5);
    }

    [Fact]
    public void ASlideMovesThePitchTheWayItsSignSays()
    {
        static int Crossings(PcmAudio a, int from, int to) => Enumerable.Range(from + 1, to - from - 1).Count(i => a.Samples[i - 1] < 0 != a.Samples[i] < 0);
        var rise = SoundSynth.Generate(new SynthSettings { Shape = SynthShape.Sine, Frequency = 300, Slide = 2, Attack = 0.002, Sustain = 0.6, Decay = 0.01, Duration = 0.6 });
        var fall = SoundSynth.Generate(new SynthSettings { Shape = SynthShape.Sine, Frequency = 300, Slide = -2, Attack = 0.002, Sustain = 0.6, Decay = 0.01, Duration = 0.6 });
        Assert.True(Crossings(rise, 20000, 26000) > Crossings(rise, 1000, 7000) * 1.5); // later in a rising sound there are more zero crossings per sample
        Assert.True(Crossings(fall, 20000, 26000) < Crossings(fall, 1000, 7000) * 0.7);
    }
}
