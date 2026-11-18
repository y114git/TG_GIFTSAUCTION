using Avalonia;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Tests;

/// <summary>Room editing faults found by reading: a flip of a turned instance, a typed decimal, a room without an instance layer, a huge fill, and a copy between room tabs.</summary>
public sealed class RoomHuntFixTests
{
#pragma warning disable CA2000 // model objects owned by the data model of the test
    private static GameMakerRoom AddRoom(GameMakerData data, string name, bool instanceLayer)
    {
        var room = new GameMakerRoom { Name = data.Strings.MakeString(name, out _), Width = 320, Height = 240, Layers = [] };
        if (instanceLayer)
            room.Layers.Add(new Layer { LayerName = data.Strings.MakeString("Instances", out _), LayerType = LayerType.Instances, LayerId = 1, ParentRoom = room, Data = new Layer.LayerInstancesData { Instances = [] } });
        else
            room.Layers.Add(new Layer { LayerName = data.Strings.MakeString("Back", out _), LayerType = LayerType.Background, LayerId = 2, ParentRoom = room, Data = new Layer.LayerBackgroundData() });
        data.Rooms.Add(room);
        return room;
    }

    private static GameObject AddInstance(GameMakerData data, GameMakerRoom room, float rotation)
    {
        var sprite = new GameMakerSprite { Name = data.Strings.MakeString("spr", out _), Width = 16, Height = 8 };
        data.GameObjects[0].Sprite = sprite;
        var instance = new GameObject { InstanceID = 100001, X = 100, Y = 100, ScaleX = 1, ScaleY = 1, Rotation = rotation, ObjectDefinition = data.GameObjects[0] };
        room.Layers[0].InstancesData.Instances.Add(instance);
        room.GameObjects.Add(instance);
        return instance;
    }

    private static async Task<Workspace> OpenAsync(TempDir dir)
    {
        var userData = new UserData(dir.File("user"));
        return await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(TestData.WriteSample(dir, objects: 1), cancellationToken: TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(0f, 116, 100)]
    [InlineData(90f, 100, 84)] // turned a quarter counterclockwise, the sprite's width runs up the screen
    [InlineData(180f, 84, 100)]
    public void FlippingATurnedInstanceShiftsAlongItsOwnAxisAndUndoes(float rotation, int x, int y)
    {
        using var data = TestData.Create();
        var room = AddRoom(data, "rm", true);
        var instance = AddInstance(data, room, rotation);
        var flip = new RoomInstanceCommand("rm", RoomInstanceCommand.FlipX, instance.InstanceID, 0, 0);
        flip.Apply(data);
        Assert.Equal((x, y, -1f), (instance.X, instance.Y, instance.ScaleX));
        flip.Revert(data);
        Assert.Equal((100, 100, 1f), (instance.X, instance.Y, instance.ScaleX));

        var flipY = new RoomInstanceCommand("rm", RoomInstanceCommand.FlipY, instance.InstanceID, 0, 0);
        flipY.Apply(data);
        Assert.Equal(-1f, instance.ScaleY);
        flipY.Revert(data);
        Assert.Equal((100, 100, 1f), (instance.X, instance.Y, instance.ScaleY));
    }

    [Fact]
    public async Task ATypedDecimalIsRoundedAndAHugeNumberIsRefusedWithoutAnException()
    {
        using var data = TestData.Create();
        var room = AddRoom(data, "rm", true);
        var instance = AddInstance(data, room, 0);
        var set = new RoomInstanceCommand("rm", RoomInstanceCommand.Set, instance.InstanceID, 0, 0, "x") { Value = "10.6" };
        set.Apply(data);
        Assert.Equal(11, instance.X);
        set.Revert(data);
        Assert.Equal(100, instance.X);
        Assert.Throws<InvalidDataException>(() => new RoomInstanceCommand("rm", RoomInstanceCommand.Set, instance.InstanceID, 0, 0, "y") { Value = "3000000000" }.Apply(data));

        using var dir = new TempDir();
        using var workspace = await OpenAsync(dir);
        var inside = AddRoom(workspace.Data, "rm_i", true);
        var typed = AddInstance(workspace.Data, inside, 0);
        await HeadlessApp.Run(() =>
        {
            var inspector = new InstanceInspector(workspace, "rm_i");
            inspector.Show([typed]);
            inspector.Set("x", "10.6");
            Assert.Equal(11, typed.X);
            inspector.Set("x", "3000000000");
            Assert.Equal(11, typed.X);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AnInstanceIsNotAddedToARoomWithoutAnInstanceLayer()
    {
        using var dir = new TempDir();
        using var workspace = await OpenAsync(dir);
        var room = AddRoom(workspace.Data, "rm_bare", false);
        Assert.False(RoomInstanceCommand.CanAddInstance(room));
        var name = workspace.Data.GameObjects[0].Name.Content!;
        Assert.Throws<InvalidOperationException>(() => new RoomInstanceCommand("rm_bare", RoomInstanceCommand.Add, 0, 1, 2, name).Apply(workspace.Data));
        Assert.Empty(room.GameObjects);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var preview = new RoomPreview(room, textures, workspace);
            await preview.Ready;
            var source = new GameObject { ObjectDefinition = workspace.Data.GameObjects[0], ScaleX = 1, ScaleY = 1 };
            preview.PaintCopies(source, [new Point(4, 4)]); // refused with a hint, no exception
            Assert.Empty(room.GameObjects);
        });
        Assert.True(RoomInstanceCommand.CanAddInstance(new GameMakerRoom { Layers = null! }));
    }

    [Fact]
    public void AFillOverTheCapIsRefusedAndTheJournalTextIsBuiltOnDemand()
    {
        var rows = Enumerable.Range(0, 2100).Select(_ => new uint[2100]).ToArray(); // 4.41 million empty cells
        var cells = RoomTilesCommand.FloodFill(rows, 0, 0, 5, out var tooLarge);
        Assert.True(tooLarge);
        Assert.Empty(cells);
        var small = RoomTilesCommand.FloodFill([.. rows.Take(10).Select(r => r[..10])], 0, 0, 5, out tooLarge);
        Assert.False(tooLarge);
        Assert.Equal(100, small.Count);

        Assert.Throws<ArgumentException>(() => new RoomTilesCommand("rm", 1, new (int, int, uint)[RoomTilesCommand.MaxCells + 1]));
        var command = new RoomTilesCommand("rm", 1, small);
        Assert.StartsWith("0,0,5;", command.Args["cells"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopiedInstancesPasteIntoAnotherRoomOfTheSameFile()
    {
        using var dir = new TempDir();
        using var workspace = await OpenAsync(dir);
        var first = AddRoom(workspace.Data, "rm_a", true);
        var second = AddRoom(workspace.Data, "rm_b", true);
        var instance = AddInstance(workspace.Data, first, 0);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var a = new RoomPreview(first, textures, workspace);
            var b = new RoomPreview(second, textures, workspace);
            await a.Ready;
            await b.Ready;
            a.Copy([instance]);
            b.Paste(new Point(40, 30));
            var pasted = Assert.Single(second.GameObjects);
            Assert.Equal((40, 30), (pasted.X, pasted.Y));
        });
    }
#pragma warning restore CA2000
}
