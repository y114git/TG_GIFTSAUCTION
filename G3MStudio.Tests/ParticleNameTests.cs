using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ParticleNameTests
{
    [Fact]
    public void APastedParticleSystemInstanceGetsARandomNameOfEightHexDigits()
    {
        using var room = new GameMakerRoom();
        var first = RoomElements.NewParticleName(room);
        Assert.Matches("^particle_[0-9a-f]{8}$", first);
        Assert.NotEqual(first, RoomElements.NewParticleName(room));
    }
}
