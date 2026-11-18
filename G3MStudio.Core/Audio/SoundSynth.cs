using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Audio;

public enum SynthShape { Sine, Square, Saw, Triangle, Noise }

public enum SynthPreset { Jump, Coin, Hit, Laser, Explosion, Blip, PowerUp }

/// <summary>The parameters of one generated sound. A value out of range, NaN or infinite is brought back into range by <see cref="SoundSynth.Generate"/>, so no input can make it fail.</summary>
public sealed record SynthSettings
{
    public SynthShape Shape { get; init; } = SynthShape.Square;

    /// <summary>Share of a period the square wave is high, 0.05 to 0.95.</summary>
    public double Duty { get; init; } = 0.5;

    /// <summary>The frequency at the start, in hertz.</summary>
    public double Frequency { get; init; } = 440;

    /// <summary>Octaves per second the frequency rises (negative: falls).</summary>
    public double Slide { get; init; }

    /// <summary>How far the vibrato bends the pitch up and down, in semitones.</summary>
    public double VibratoDepth { get; init; }

    /// <summary>Vibrato cycles per second.</summary>
    public double VibratoSpeed { get; init; } = 6;

    /// <summary>Seconds the level rises from silence to full.</summary>
    public double Attack { get; init; } = 0.01;

    /// <summary>Seconds the level stays at full.</summary>
    public double Sustain { get; init; } = 0.1;

    /// <summary>Seconds the level falls back to silence.</summary>
    public double Decay { get; init; } = 0.2;

    /// <summary>Length of the sound in seconds; the sustain, then the attack and decay, are shortened to fit when the envelope is longer, and silence follows when it is shorter.</summary>
    public double Duration { get; init; } = 0.4;

    /// <summary>0 to 1.</summary>
    public double Volume { get; init; } = 0.8;

    /// <summary>Cut off above this frequency (hertz); 0 is off.</summary>
    public double LowPass { get; init; }

    /// <summary>Starts the noise; the same seed and settings always give the same sound.</summary>
    public int Seed { get; init; } = 1;
}

/// <summary>
/// A small sound generator for effects: one oscillator (sine, square with a duty, saw, triangle or noise) with a frequency slide and vibrato, an attack, sustain and decay envelope, a volume and an
/// optional low-pass filter, as 16 bit mono at 44100 Hz. Pure and deterministic: the same settings give the same bytes. The envelope starts and ends at zero, so a sound never clicks.
/// </summary>
public static class SoundSynth
{
    public const int SampleRate = 44100;

    public const double MinFrequency = 20, MaxFrequency = 20000, MinDuration = 0.02, MaxDuration = 10, MaxSlide = 8, MaxVibrato = 12, MaxVibratoSpeed = 40, MaxEnvelope = 10;

    private const double MinAttack = 0.002, MinDecay = 0.004; // seconds: shorter ends would click

    /// <summary>Renders the settings to samples between -1 and 1; out of range values are limited first, so this never throws.</summary>
    public static PcmAudio Generate(SynthSettings settings)
    {
        var s = Limited(settings);
        var frames = (int)Math.Round(s.Duration * SampleRate);
        var (attack, sustain, decay) = (Math.Max(s.Attack, MinAttack), s.Sustain, Math.Max(s.Decay, MinDecay));
        if (attack + sustain + decay > s.Duration)
        {
            // An envelope longer than the sound gives up its sustain first; then the attack and decay share the sound in their own proportion, each keeping its click guard, so the end still fades.
            sustain = Math.Max(0, sustain - (attack + sustain + decay - s.Duration));
            var room = s.Duration - sustain;
            if (attack + decay > room)
            {
                attack = Math.Clamp(room * attack / (attack + decay), MinAttack, Math.Max(MinAttack, room - MinDecay));
                decay = room - attack;
            }
        }

        var cutoff = s.LowPass > 0 ? 1 - Math.Exp(-2 * Math.PI * s.LowPass / SampleRate) : 1;
        var random = new SplitMix((ulong)(s.Seed & 0xFFFFFFFFL));
        var samples = new float[frames];
        double phase = 0, held = 0, filtered = 0;
        for (var i = 0; i < frames; i++)
        {
            var t = (double)i / SampleRate;
            var pitch = s.Frequency * Math.Pow(2, s.Slide * t + s.VibratoDepth / 12 * Math.Sin(2 * Math.PI * s.VibratoSpeed * t));
            phase += Math.Clamp(pitch, MinFrequency, MaxFrequency) / SampleRate;
            var wrapped = phase >= 1;
            phase -= Math.Floor(phase);
            if (wrapped || i == 0) held = random.Next() * 2 - 1;
            var wave = s.Shape switch
            {
                SynthShape.Sine => Math.Sin(2 * Math.PI * phase),
                SynthShape.Square => phase < s.Duty ? 1 : -1,
                SynthShape.Saw => 2 * phase - 1,
                SynthShape.Triangle => 4 * Math.Abs(phase - 0.5) - 1,
                _ => held, // noise: a new random level every period, so the frequency sets how rough it is
            };
            filtered += cutoff * (wave - filtered);
            samples[i] = (float)Math.Clamp(filtered * Envelope(t, attack, sustain, decay) * s.Volume, -1, 1);
        }

        return new PcmAudio(samples, 1, SampleRate);
    }

    /// <summary>16 bit PCM, mono, 44100 Hz: a form a sound of a data file keeps as it is and the player reads without a codec.</summary>
    public static byte[] ToWav(SynthSettings settings) => AudioCodec.Encode(Generate(settings), "wav");

    /// <summary>The settings of a preset; the seed only changes the noise.</summary>
    public static SynthSettings Preset(SynthPreset preset, int seed = 1)
    {
        var settings = preset switch
        {
            SynthPreset.Jump => new SynthSettings { Shape = SynthShape.Square, Duty = 0.35, Frequency = 260, Slide = 1.6, Attack = 0.005, Sustain = 0.05, Decay = 0.18, Duration = 0.3, LowPass = 6000 },
            SynthPreset.Coin => new SynthSettings { Shape = SynthShape.Square, Duty = 0.5, Frequency = 988, Slide = 0.6, Attack = 0.002, Sustain = 0.08, Decay = 0.22, Duration = 0.35, LowPass = 9000 },
            SynthPreset.Hit => new SynthSettings { Shape = SynthShape.Noise, Frequency = 600, Slide = -1, Attack = 0.002, Sustain = 0.02, Decay = 0.12, Duration = 0.16, LowPass = 3500 },
            SynthPreset.Laser => new SynthSettings { Shape = SynthShape.Saw, Frequency = 1500, Slide = -3, Attack = 0.002, Sustain = 0.06, Decay = 0.16, Duration = 0.25, LowPass = 8000 },
            SynthPreset.Explosion => new SynthSettings { Shape = SynthShape.Noise, Frequency = 220, Slide = -0.6, Attack = 0.01, Sustain = 0.15, Decay = 0.7, Duration = 0.9, LowPass = 1200, Volume = 1 },
            SynthPreset.Blip => new SynthSettings { Shape = SynthShape.Square, Duty = 0.5, Frequency = 520, Attack = 0.002, Sustain = 0.04, Decay = 0.04, Duration = 0.09, LowPass = 7000 },
            _ => new SynthSettings { Shape = SynthShape.Square, Duty = 0.25, Frequency = 300, Slide = 2, VibratoDepth = 0.6, VibratoSpeed = 14, Attack = 0.01, Sustain = 0.2, Decay = 0.25, Duration = 0.5, LowPass = 8000 },
        };
        return settings with { Seed = seed };
    }

    /// <summary>A sound drawn from the seed: the same seed always gives the same settings, and every one is audible.</summary>
    public static SynthSettings Random(int seed)
    {
        var random = new SplitMix(0x9E3779B97F4A7C15UL ^ (ulong)(seed & 0xFFFFFFFFL));
        double Between(double low, double high) => low + (high - low) * random.Next();
        var shape = (SynthShape)(int)(random.Next() * 5);
        return new SynthSettings
        {
            Shape = shape,
            Duty = Between(0.1, 0.9),
            Frequency = 120 * Math.Pow(2, Between(0, 4)), // 120 to 1920 Hz, evenly in pitch
            Slide = Between(-2, 2),
            VibratoDepth = random.Next() < 0.4 ? Between(0.2, 3) : 0,
            VibratoSpeed = Between(3, 20),
            Attack = Between(0.002, 0.05),
            Sustain = Between(0.02, 0.25),
            Decay = Between(0.05, 0.5),
            Duration = Between(0.15, 0.9),
            Volume = Between(0.6, 1),
            LowPass = random.Next() < 0.5 ? Between(800, 8000) : 0,
            Seed = seed,
        };
    }

    // The envelope is 0 at the start and the end and 1 for the sustain; the decay is squared, which sounds like a natural fade. Silence follows an envelope shorter than the sound.
    private static double Envelope(double t, double attack, double sustain, double decay)
    {
        if (t < attack) return t / attack;
        if (t < attack + sustain) return 1;
        var left = 1 - (t - attack - sustain) / decay;
        return left > 0 ? left * left : 0;
    }

    /// <summary>The settings with every value in range (NaN takes the default), so the generator has no case that fails.</summary>
    public static SynthSettings Limited(SynthSettings s) => s with
    {
        Shape = Enum.IsDefined(s.Shape) ? s.Shape : SynthShape.Square,
        Duty = Clean(s.Duty, 0.05, 0.95, 0.5),
        Frequency = Clean(s.Frequency, MinFrequency, MaxFrequency, 440),
        Slide = Clean(s.Slide, -MaxSlide, MaxSlide, 0),
        VibratoDepth = Clean(s.VibratoDepth, 0, MaxVibrato, 0),
        VibratoSpeed = Clean(s.VibratoSpeed, 0, MaxVibratoSpeed, 6),
        Attack = Clean(s.Attack, 0, MaxEnvelope, 0.01),
        Sustain = Clean(s.Sustain, 0, MaxEnvelope, 0.1),
        Decay = Clean(s.Decay, 0, MaxEnvelope, 0.2),
        Duration = Clean(s.Duration, MinDuration, MaxDuration, 0.4),
        Volume = Clean(s.Volume, 0, 1, 0.8),
        LowPass = Cutoff(s.LowPass),
    };

    // Below 50 Hz a low-pass would mute the sound, so that counts as off.
    private static double Cutoff(double hertz) => Clean(hertz, 0, MaxFrequency, 0) is var cutoff && cutoff < 50 ? 0 : cutoff;

    private static double Clean(double value, double low, double high, double fallback) => double.IsNaN(value) ? fallback : Math.Clamp(value, low, high);

    // A small seeded generator: .NET's own Random does not promise the same numbers in every version, and a saved seed must always give the same bytes.
    private sealed class SplitMix(ulong state)
    {
        public double Next()
        {
            var z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (double)((z ^ (z >> 31)) >> 11) / (1UL << 53);
        }
    }
}
