using Avalonia.Input;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the room and the layer belong to the data, which is disposed
public sealed class TileKeysTests
{
    [Fact]
    public async Task KeysChooseTheTileToolAndTurnTheFlagsOfTheChosenTileLayer()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var data = workspace.Data;
        var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_k", out _) };
        data.Backgrounds.Add(set);
        var room = new GameMakerRoom { Name = data.Strings.MakeString("rm_k", out _), Layers = [] };
        var layer = new Layer { LayerName = data.Strings.MakeString("tiles", out _), LayerType = LayerType.Tiles, LayerId = 1, ParentRoom = room };
        layer.Data = new Layer.LayerTilesData { ParentLayer = layer, Background = set, TilesX = 2, TilesY = 2, TileData = [[0u, 0u], [0u, 0u]] };
        room.Layers.Add(layer);
        data.Rooms.Add(room);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var preview = new RoomPreview(room, textures, workspace);
            await preview.Ready;
            void Press(Key key) => preview.View.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

            Press(Key.G); // without a tile layer G is the grid
            Assert.NotEqual(RoomTool.Fill, preview.View.Tool);
            preview.ChooseTileLayer(layer);
            Press(Key.G);
            Assert.Equal(RoomTool.Fill, preview.View.Tool);
            Press(Key.B);
            Assert.Equal(RoomTool.Paint, preview.View.Tool);
            Press(Key.E);
            Assert.Equal(RoomTool.Erase, preview.View.Tool);
            Press(Key.R);
            Assert.Equal(RoomTool.Rectangle, preview.View.Tool);
            Press(Key.P);
            Assert.Equal(RoomTool.Pick, preview.View.Tool);

            Press(Key.X);
            Press(Key.Y);
            Press(Key.Z);
            Assert.Equal(7, preview.TileFlags);
            Press(Key.X);
            Assert.Equal(6, preview.TileFlags);
        });
    }
}
