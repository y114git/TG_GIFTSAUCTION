using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Particles;

namespace G3MStudio.Tests;

public sealed class ParticleTests
{
    private static GameMakerParticleSystem SystemOf(params GameMakerParticleSystemEmitter[] emitters)
    {
        var system = new GameMakerParticleSystem { Name = new GameMakerString("ps_test") };
        foreach (var emitter in emitters) system.Emitters.Add(new GameMakerResourceById<GameMakerParticleSystemEmitter, GameMakerChunkPSEM> { Resource = emitter });
        return system;
    }

    [Fact]
    public void ABurstEmitsOnceMovesTheParticlesAndTheyDieAfterTheirLifetime()
    {
        var emitter = new GameMakerParticleSystemEmitter
        {
            Name = new GameMakerString("burst"),
            Mode = GameMakerParticleSystemEmitter.EmitMode.Burst,
            EmitCount = 5,
            LifetimeMin = 10,
            LifetimeMax = 10,
            SpeedMin = 2,
            SpeedMax = 2,
            DirectionMin = 0,
            DirectionMax = 0,
            RegionWidth = 0,
            RegionHeight = 0,
        };
        using var system = SystemOf(emitter);
        var simulation = new ParticleSimulation(system);

        simulation.Step();
        Assert.Equal(5, simulation.Count);
        Assert.All(simulation.Dots(), dot => Assert.Equal(2f, dot.X, 0.01f)); // direction 0 is to the right, 2 pixels a step
        for (var i = 0; i < 20; i++) simulation.Step();
        Assert.Equal(0, simulation.Count);
    }

    [Fact]
    public void AStreamKeepsEmittingAndADisabledEmitterEmitsNothing()
    {
        var stream = new GameMakerParticleSystemEmitter { Name = new GameMakerString("stream"), Mode = GameMakerParticleSystemEmitter.EmitMode.Stream, EmitCount = 2, LifetimeMin = 100, LifetimeMax = 100 };
        var off = new GameMakerParticleSystemEmitter { Name = new GameMakerString("off"), Enabled = false, EmitCount = 50 };
        using var system = SystemOf(stream, off);
        var simulation = new ParticleSimulation(system);
        for (var i = 0; i < 10; i++) simulation.Step();
        Assert.Equal(20, simulation.Count);
    }

    [Fact]
    public void NonFiniteValuesFromAHandMadeFileNeitherHangNorBreakTheLimit()
    {
        var emitter = new GameMakerParticleSystemEmitter
        {
            Name = new GameMakerString("bad"),
            EmitCount = int.MaxValue,
            LifetimeMin = float.NaN,
            LifetimeMax = float.PositiveInfinity,
            SpeedMin = float.NaN,
            SpeedMax = float.NegativeInfinity,
            RegionWidth = float.PositiveInfinity,
            DelayMin = float.NaN,
            DelayMax = float.NaN,
        };
        using var system = SystemOf(emitter);
        var simulation = new ParticleSimulation(system);
        for (var i = 0; i < 50; i++) simulation.Step();
        Assert.InRange(simulation.Count, 0, ParticleSimulation.MaxParticles);
        Assert.All(simulation.Dots(), dot => Assert.True(float.IsFinite(dot.X) && float.IsFinite(dot.Y) && float.IsFinite(dot.Size)));
    }

    [Fact]
    public async Task EveryParticleSystemOfARealGameRunsWithinTheLimit()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            if (data.ParticleSystems is not { Count: > 0 } systems) continue;
            foreach (var system in systems)
            {
                var simulation = new ParticleSimulation(system);
                for (var i = 0; i < 120; i++) simulation.Step();
                Assert.InRange(simulation.Count, 0, ParticleSimulation.MaxParticles);
                Assert.All(simulation.Dots(), dot => Assert.True(float.IsFinite(dot.X) && float.IsFinite(dot.Y)));
            }

            return;
        }

        Assert.Skip("No particle system in the corpus.");
    }
}
