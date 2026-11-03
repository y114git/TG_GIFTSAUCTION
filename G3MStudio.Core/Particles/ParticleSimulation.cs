using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Particles;

/// <summary>
/// A small, approximate simulation of a particle system for the preview: one step is one game frame (60 per second). Emitters stream or burst
/// particles from their region; a particle moves with speed, direction, gravity and the per-step increases, and fades between the start, mid and end colors.
/// shortcut: particles are drawn as disks, so the emitter sprite, texture, orientation, wiggle, spawn-on-death and spawn-on-update are not simulated; add them when the preview needs to match the game.
/// </summary>
public sealed class ParticleSimulation
{
    public const int MaxParticles = 4000;

    private const float Degrees = MathF.PI / 180;
    private const int MaxPerStep = 200;

    private readonly List<Source> _sources = [];
    private readonly List<Particle> _live = [];
    private readonly Random _random;

    private sealed class Source(GameMakerParticleSystemEmitter emitter, float delay)
    {
        public GameMakerParticleSystemEmitter Emitter { get; } = emitter;

        public float Delay { get; set; } = delay;

        public bool Fired { get; set; }

        public float Interval { get; set; }
    }

    private sealed class Particle
    {
        public required GameMakerParticleSystemEmitter Emitter { get; init; }

        public float X, Y, Direction, Speed, Age, Life, SizeX, SizeY, PushX, PushY;
    }

    public ParticleSimulation(GameMakerParticleSystem system, int seed = 1)
    {
        _random = new Random(seed);
        foreach (var emitter in (system.Emitters ?? []).Select(r => r.Resource).OfType<GameMakerParticleSystemEmitter>())
            _sources.Add(new Source(emitter, Frames(Between(emitter.DelayMin, emitter.DelayMax), emitter.DelayUnit)));
    }

    public int Count => _live.Count;

    public long Steps { get; private set; }

    private static float Clean(float value, float min, float max) => float.IsFinite(value) ? Math.Clamp(value, min, max) : min;

    // A deterministic seeded generator on purpose: the preview is a visual effect, and a test can replay it.
#pragma warning disable CA5394
    private float Unit() => (float)_random.NextDouble();
#pragma warning restore CA5394

    private float Between(float low, float high) => low + ((high - low) * Unit());

    private static float Frames(float value, GameMakerParticleSystemEmitter.TimeUnitEnum unit) => Clean(value, 0, 100_000) * (unit == GameMakerParticleSystemEmitter.TimeUnitEnum.Seconds ? 60 : 1);

    public void Step()
    {
        Steps++;
        foreach (var source in _sources.Where(s => s.Emitter.Enabled))
        {
            var emitter = source.Emitter;
            if ((source.Delay -= 1) > 0) continue;
            if (emitter.Mode == GameMakerParticleSystemEmitter.EmitMode.Stream)
            {
                Emit(emitter, emitter.EmitCount);
            }
            else if (!source.Fired)
            {
                Emit(emitter, emitter.EmitCount);
                (source.Fired, source.Interval) = (true, Frames(Between(emitter.IntervalMin, emitter.IntervalMax), emitter.IntervalUnit));
            }
            else if (source.Interval > 0 && (source.Interval -= 1) <= 0)
            {
                Emit(emitter, emitter.EmitCount);
                source.Interval = Frames(Between(emitter.IntervalMin, emitter.IntervalMax), emitter.IntervalUnit);
            }
        }

        foreach (var p in _live)
        {
            var e = p.Emitter;
            p.Speed += Clean(e.SpeedIncrease, -1000, 1000);
            p.Direction += Clean(e.DirectionIncrease, -360, 360);
            var gravity = Clean(e.GravityForce, -1000, 1000);
            p.PushX += MathF.Cos(e.GravityDirection * Degrees) * gravity;
            p.PushY -= MathF.Sin(e.GravityDirection * Degrees) * gravity;
            p.X += (MathF.Cos(p.Direction * Degrees) * p.Speed) + p.PushX;
            p.Y += (-MathF.Sin(p.Direction * Degrees) * p.Speed) + p.PushY;
            p.SizeX = Math.Max(0, p.SizeX + Clean(e.SizeIncreaseX, -100, 100));
            p.SizeY = Math.Max(0, p.SizeY + Clean(e.SizeIncreaseY, -100, 100));
            p.Age++;
        }

        _live.RemoveAll(p => p.Age >= p.Life);
    }

    private void Emit(GameMakerParticleSystemEmitter e, int count)
    {
        for (var i = 0; i < Math.Clamp(count, 0, MaxPerStep) && _live.Count < MaxParticles; i++)
        {
            var (x, y) = Place(e);
            _live.Add(new Particle
            {
                Emitter = e,
                X = x,
                Y = y,
                Direction = Clean(Between(e.DirectionMin, e.DirectionMax), -100_000, 100_000),
                Speed = Clean(Between(e.SpeedMin, e.SpeedMax), -1000, 1000),
                Life = Math.Max(1, Clean(Between(e.LifetimeMin, e.LifetimeMax), 1, 100_000)),
                SizeX = Clean(Between(e.SizeMinX, e.SizeMaxX), 0, 100) * Clean(e.ScaleX, 0, 100),
                SizeY = Clean(Between(e.SizeMinY, e.SizeMaxY), 0, 100) * Clean(e.ScaleY, 0, 100),
            });
        }
    }

    /// <summary>A point in the region of the emitter: uniform in the shape, or bell-shaped for a Gaussian distribution, turned by the rotation of the region.</summary>
    private (float X, float Y) Place(GameMakerParticleSystemEmitter e)
    {
        float Spread() => e.Distribution == GameMakerParticleSystemEmitter.DistributionEnum.Gaussian ? ((Unit() + Unit() + Unit()) / 1.5f) - 1 : Between(-1, 1);
        float u = Spread(), v = Spread();
        switch (e.Shape)
        {
            case GameMakerParticleSystemEmitter.EmitterShape.Ellipse when (u * u) + (v * v) > 1:
                (u, v) = (u * 0.7f, v * 0.7f);
                break;
            case GameMakerParticleSystemEmitter.EmitterShape.Diamond when Math.Abs(u) + Math.Abs(v) > 1:
                (u, v) = (u / 2, v / 2);
                break;
            case GameMakerParticleSystemEmitter.EmitterShape.Line:
                v = 0;
                break;
        }

        var (dx, dy) = (u * Clean(e.RegionWidth, 0, 100_000) / 2, v * Clean(e.RegionHeight, 0, 100_000) / 2);
        var (sin, cos) = MathF.SinCos(Clean(e.Rotation, -100_000, 100_000) * Degrees);
        return (Clean(e.RegionX, -100_000, 100_000) + (dx * cos) - (dy * sin), Clean(e.RegionY, -100_000, 100_000) + (dx * sin) + (dy * cos));
    }

    private static byte Mix(uint from, uint to, float t, int shift) => (byte)Math.Round((((from >> shift) & 0xFF) * (1 - t)) + (((to >> shift) & 0xFF) * t));

    /// <summary>The particles now alive. Emitter colors are ABGR as in GameMaker.</summary>
    public IReadOnlyList<Dot> Dots() => [.. _live.Select(p =>
    {
        var (e, t) = (p.Emitter, Math.Clamp(p.Age / p.Life, 0, 1));
        var (from, to, local) = t < 0.5f ? (e.StartColor, e.MidColor, t * 2) : (e.MidColor, e.EndColor, (t - 0.5f) * 2);
        return new Dot(p.X, p.Y, (p.SizeX + p.SizeY) / 2, Mix(from, to, local, 24), Mix(from, to, local, 0), Mix(from, to, local, 8), Mix(from, to, local, 16));
    })];
}

/// <summary>A particle ready to draw: position relative to the system origin, size factor and ARGB color.</summary>
public readonly record struct Dot(float X, float Y, float Size, byte A, byte R, byte G, byte B);
