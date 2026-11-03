using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Tests;

public sealed class TileUsageTests
{
    [Fact]
    public async Task ATileCellIsFoundInItsRoomAndLayerWhileTheEmptyTileAndOtherSetsAreNot()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var data = workspace.Data;
        var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_a", out _) };
        data.Backgrounds.Add(set);
        var room = new GameMakerRoom { Name = data.Strings.MakeString("rm_a", out _), Layers = [] };
        var layer = new Layer { LayerName = data.Strings.MakeString("tiles", out _), LayerType = LayerType.Tiles, LayerId = 1, ParentRoom = room };
        layer.Data = new Layer.LayerTilesData { ParentLayer = layer, Background = set, TilesX = 3, TilesY = 1, TileData = [[0u, 5u, 0x30000000u | 9u]] }; // an empty cell, tile 5, tile 9 with flip flags
        room.Layers.Add(layer);
        data.Rooms.Add(room);

        var token = TestContext.Current.CancellationToken;
        Assert.Contains(await UsageSearch.FindTileAsync(workspace, "ts_a", 5, token), h => h.OwnerType == "rooms" && h.OwnerName == "rm_a" && h.Where == "tiles");
        Assert.Single(await UsageSearch.FindTileAsync(workspace, "ts_a", 9, token));
        Assert.Empty(await UsageSearch.FindTileAsync(workspace, "ts_a", 0, token));
        Assert.Empty(await UsageSearch.FindTileAsync(workspace, "ts_a", 6, token));
        Assert.Empty(await UsageSearch.FindTileAsync(workspace, "no_such_set", 5, token));
    }
}
