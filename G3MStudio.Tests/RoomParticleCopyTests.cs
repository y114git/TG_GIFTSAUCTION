using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Tests;

public sealed class RoomParticleCopyTests
{
    [Fact]
    public async Task ACopiedParticleElementKeepsItsColorScaleAndRotationAndANewOneIsOpaque()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
        var data = workspace.Data;
#pragma warning disable CA2000 // the data owns the system
        var system = new GameMakerParticleSystem { Name = data.Strings.MakeString("ps_copy", out _) };
#pragma warning restore CA2000
        data.ParticleSystems.Add(system);
        var room = new GameMakerRoom { Name = data.Strings.MakeString("rm_copy", out _), Layers = [] };
        var layer = new Layer { LayerName = data.Strings.MakeString("fx", out _), LayerType = LayerType.Assets, LayerId = 7, ParentRoom = room, Data = new Layer.LayerAssetsData { ParticleSystems = [] } };
        room.Layers.Add(layer);
        data.Rooms.Add(room);
        var list = layer.AssetsData.ParticleSystems;

        var plain = new RoomElementCommand(new ElementRef("rm_copy", 7, RoomElements.Particle, "plain"), RoomElementCommand.Add, 1, 2, "ps_copy");
        plain.Apply(data);
        Assert.Equal((0xFFFFFFFFu, 1f, 1f, 0f), (list[0].Color, list[0].ScaleX, list[0].ScaleY, list[0].Rotation));
        Assert.False(plain.Args.ContainsKey("color"));

        var copy = new RoomElementCommand(new ElementRef("rm_copy", 7, RoomElements.Particle, "copy"), RoomElementCommand.Add, 3, 4, "ps_copy", new ParticleLook(0x80102030, 2, 3, 45));
        copy.Apply(data);
        Assert.Equal((0x80102030u, 2f, 3f, 45f), (list[1].Color, list[1].ScaleX, list[1].ScaleY, list[1].Rotation));
        var again = EditCommand.Create(copy.Kind, copy.Args);
        copy.Revert(data);
        again.Apply(data); // the journal form carries the look
        Assert.Equal((0x80102030u, 2f, 3f, 45f), (list[1].Color, list[1].ScaleX, list[1].ScaleY, list[1].Rotation));
    }
}
