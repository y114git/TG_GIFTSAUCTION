using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RoomEditTests
{
    [Fact]
    public async Task MovingDeletingAndAddingInstancesUndoAndSurviveAWrite()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => RoomInstanceCommand.Lists(r).Any(l => l.Count > 0));
        var name = room.Name.Content ?? string.Empty;
        var first = RoomInstanceCommand.Lists(room).SelectMany(l => l).First();
        var (id, x, y) = (first.InstanceID, first.X, first.Y);
        var total = room.GameObjects.Count;

        var move = new RoomInstanceCommand(name, RoomInstanceCommand.Move, id, x + 5, y + 7);
        move.Apply(data);
        Assert.Equal((x + 5, y + 7), (first.X, first.Y));
        move.Revert(data);
        Assert.Equal((x, y), (first.X, first.Y));

        var delete = new RoomInstanceCommand(name, RoomInstanceCommand.Delete, id);
        delete.Apply(data);
        Assert.Equal(total - 1, room.GameObjects.Count);
        delete.Revert(data);
        Assert.Equal(total, room.GameObjects.Count);

        var lastObj = data.GeneralInfo.LastObj;
        var add = new RoomInstanceCommand(name, RoomInstanceCommand.Add, 0, 12, 34, data.GameObjects[0].Name.Content ?? string.Empty);
        add.Apply(data);
        var newId = uint.Parse(add.Args["id"], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(newId > lastObj);
        Assert.Equal(newId, data.GeneralInfo.LastObj);

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var added = back.Rooms.First(r => r.Name.Content == name);
        Assert.Contains(RoomInstanceCommand.Lists(added).SelectMany(l => l), i => i.InstanceID == newId && i.X == 12 && i.Y == 34);

        add.Revert(data);
        Assert.Equal(lastObj, data.GeneralInfo.LastObj);
        Assert.Equal(total, room.GameObjects.Count);
        Assert.Equal(name, EditCommand.Create(delete.Kind, delete.Args).Args["room"]);
    }

    [Fact]
    public async Task MovingAnInstanceInTheCreationOrderUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.GameObjects.Count > 2);
        var before = room.GameObjects.Select(i => i.InstanceID).ToList();
        var move = new RoomCreationOrderCommand(room.Name.Content ?? string.Empty, before[0], room.GameObjects.Count - 1);
        move.Apply(data);
        Assert.Equal([.. before.Skip(1), before[0]], room.GameObjects.Select(i => i.InstanceID));
        move.Revert(data);
        Assert.Equal(before, room.GameObjects.Select(i => i.InstanceID));
        Assert.Equal(move.Args["index"], EditCommand.Create(move.Kind, move.Args).Args["index"]);
    }

    [Fact]
    public async Task PathPointsAddMoveAndDeleteUndo()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            if (data.Paths.FirstOrDefault(p => p.Points.Count > 1) is not { } path) continue;
            var name = path.Name.Content ?? string.Empty;
            var count = path.Points.Count;
            var (x, y) = (path.Points[0].X, path.Points[0].Y);

            var add = new PathPointsCommand(name, PathPointsCommand.Add, -1, 7, 9);
            add.Apply(data);
            Assert.Equal((count + 1, 7f, 9f), (path.Points.Count, path.Points[^1].X, path.Points[^1].Y));
            add.Revert(data);
            Assert.Equal(count, path.Points.Count);

            var move = new PathPointsCommand(name, PathPointsCommand.Move, 0, x + 3, y + 4);
            move.Apply(data);
            Assert.Equal((x + 3, y + 4), (path.Points[0].X, path.Points[0].Y));
            move.Revert(data);
            Assert.Equal((x, y), (path.Points[0].X, path.Points[0].Y));

            var delete = EditCommand.Create(PathPointsCommand.Name, new PathPointsCommand(name, PathPointsCommand.Delete, 0).Args);
            delete.Apply(data);
            Assert.Equal(count - 1, path.Points.Count);
            delete.Revert(data);
            Assert.Equal((count, x, y), (path.Points.Count, path.Points[0].X, path.Points[0].Y));

            var speed0 = path.Points[0].Speed;
            var speed = new PathPointsCommand(name, PathPointsCommand.Speed, 0, 0.5f);
            speed.Apply(data);
            Assert.Equal(0.5f, path.Points[0].Speed);
            speed.Revert(data);
            Assert.Equal(speed0, path.Points[0].Speed);

            var shift = EditCommand.Create(PathPointsCommand.Name, new PathPointsCommand(name, PathPointsCommand.Shift, 0, 10, -5).Args);
            shift.Apply(data);
            Assert.Equal((x + 10, y - 5), (path.Points[0].X, path.Points[0].Y));
            shift.Revert(data);
            Assert.Equal((x, y), (path.Points[0].X, path.Points[0].Y));
            return;
        }

        Assert.Skip("No path with two points in the corpus.");
    }

    [Fact]
    public async Task ARoomTabRestoresItsZoomAndPosition()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var state = new ViewState("", [], null, new Dictionary<string, double[]> { ["rooms:r"] = [2, 10, 20] });
        var back = System.Text.Json.JsonSerializer.Deserialize<ViewState>(System.Text.Json.JsonSerializer.Serialize(state));
        Assert.Equal([2, 10, 20], back!.Rooms!["rooms:r"]);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var preview = new Editor.RoomPreview(data.Rooms[0], textures);
            await preview.RestoreViewAsync([2, 10, 20]);
            Assert.Equal([2, 10, 20], preview.ViewState);
        });
    }

    [Fact]
    public async Task FittingATileLayerToTheRoomResizesItAndUndoes()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var room = data.Rooms.FirstOrDefault(r => r.Layers?.Any(l => l.TilesData is { Background: not null, TileData.Length: > 0 }) == true);
            if (room is null) continue;
            var layer = room.Layers.First(l => l.TilesData is { Background: not null, TileData.Length: > 0 });
            var before = layer.TilesData.TileData;
            var (x, y) = (layer.TilesData.TilesX, layer.TilesData.TilesY);
            var oldWidth = room.Width;
            room.Width += layer.TilesData.Background.GMS2TileWidth * 2;
            var fit = new RoomLayerCommand(room.Name.Content ?? string.Empty, RoomLayerCommand.FitTiles, layer.LayerId);
            fit.Apply(data);
            Assert.Equal(x + 2, layer.TilesData.TilesX);
            Assert.Equal(before[0][0], layer.TilesData.TileData[0][0]);
            fit.Revert(data);
            Assert.Equal((x, y), (layer.TilesData.TilesX, layer.TilesData.TilesY));
            Assert.Same(before, layer.TilesData.TileData);
            room.Width = oldWidth;
            return;
        }

        Assert.Skip("No tile layer in the corpus.");
    }

    [Fact]
    public async Task ABackgroundLayerIsEditedThroughItsPathInTheRoom()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories))
        {
            using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            var room = data.Rooms.FirstOrDefault(r => r.Layers?.Any(l => l.BackgroundData is not null) == true);
            if (room is null) continue;
            var index = room.Layers.ToList().FindIndex(l => l.BackgroundData is not null);
            var prefix = $"Layers[{index}].BackgroundData";
            var shown = PropertyModel.ReadRaw(room, prefix);
            var row = PropertyModel.Describe(shown!).First(r => r.Kind == PropertyKind.Bool && !r.ReadOnly);
            var path = prefix + "." + row.Path;
            var flipped = row.Value == "True" ? "False" : "True";
            var set = new SetPropertyCommand("rooms", room.Name.Content ?? string.Empty, path, flipped);
            set.Apply(data);
            Assert.Equal(flipped, PropertyModel.Read(room, path));
            set.Revert(data);
            Assert.Equal(row.Value, PropertyModel.Read(room, path));
            return;
        }

        Assert.Skip("No background layer in the corpus.");
    }

    [Fact]
    public async Task ClickingWithThePlaceToolAddsTheObjectSelectedInTheExplorer()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var room = workspace.Data.Rooms[0];
            var preview = new Editor.RoomPreview(room, textures, workspace) { PlaceSource = () => "objects:obj_1" };
            var window = new Avalonia.Controls.Window { Content = preview, Width = 800, Height = 700 };
            window.Show();
            await preview.Ready;
            var before = room.GameObjects.Count;
            preview.View.Tool = Rooms.RoomTool.Place;
            window.UpdateLayout();
            var middle = Avalonia.VisualExtensions.TranslatePoint(preview.View, new Avalonia.Point(40, 40), window) ?? default;
            Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, middle, Avalonia.Input.MouseButton.Left);
            Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, middle, Avalonia.Input.MouseButton.Left);
            Assert.Equal(before + 1, room.GameObjects.Count);
            Assert.Equal("obj_1", room.GameObjects[^1].ObjectDefinition?.Name.Content);
            workspace.History.Undo();
            Assert.Equal(before, room.GameObjects.Count);
        });
    }

    [Fact]
    public async Task TheRoomToolbarButtonsAndTheKeysShareOneState()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 2);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var preview = new Editor.RoomPreview(workspace.Data.Rooms[0], textures, workspace);
            var window = new Avalonia.Controls.Window { Content = preview, Width = 900, Height = 700 };
            window.Show();
            await preview.Ready;
            T Named<T>(string name) where T : Avalonia.Controls.Control => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<T>().Single(c => c.Name == name);
            var grid = Named<Avalonia.Controls.Primitives.ToggleButton>("RoomGrid");

            grid.IsChecked = true; // the button turns the grid on
            Assert.True(preview.View.ShowGrid);
            preview.View.ShowGrid = false; // the G key turns it off and the button follows
            Assert.False(grid.IsChecked);

            preview.View.Zoom = 1;
            Named<Avalonia.Controls.Button>("RoomZoomIn").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(1.25, preview.View.Zoom, 3);
            Named<Avalonia.Controls.Button>("RoomActual").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(1, preview.View.Zoom);
        });
    }

    [Fact]
    public async Task FlippingKeepsThePictureInPlaceAndAGroupIsOneUndoStep()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.GameObjects.Count > 1);
        var name = room.Name.Content ?? string.Empty;
        var a = room.GameObjects[0];
        var b = room.GameObjects.First(o => o.InstanceID != a.InstanceID);
        var before = (a.X, a.Y, a.ScaleX, a.ScaleY, bX: b.X);

        var flip = new RoomInstanceCommand(name, RoomInstanceCommand.FlipX, a.InstanceID);
        flip.Apply(data);
        Assert.Equal(-before.ScaleX, a.ScaleX);
        flip.Revert(data);
        Assert.Equal(before, (a.X, a.Y, a.ScaleX, a.ScaleY, b.X));

        var group = new GroupCommand([new RoomInstanceCommand(name, RoomInstanceCommand.Move, a.InstanceID, a.X + 3, a.Y), new RoomInstanceCommand(name, RoomInstanceCommand.Move, b.InstanceID, b.X + 3, b.Y)]);
        var rebuilt = EditCommand.Create(group.Kind, group.Args);
        rebuilt.Apply(data);
        Assert.Equal((before.X + 3, before.bX + 3), (a.X, b.X));
        rebuilt.Revert(data);
        Assert.Equal(before, (a.X, a.Y, a.ScaleX, a.ScaleY, b.X));
    }

    [Fact]
    public async Task TheViewFindsTheTopmostInstanceAndSnapsToTheGrid()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            foreach (var room in data.Rooms.Take(40))
            {
                var scene = await Rooms.RoomScene.BuildAsync(room, textures);
                var item = scene.Items.FirstOrDefault(i => i.Instance is not null);
                if (item.Instance is null) continue;
                var view = new Rooms.RoomView { Scene = scene };
                Assert.NotNull(view.HitTest(item.Dest.Center));
                Assert.Null(view.HitTest(new Avalonia.Point(-100, -100)));
                view.Locked.Add(item.Instance);
                Assert.NotSame(item.Instance, view.HitTest(item.Dest.Center));
                view.Locked.Clear();
                view.Select([item.Instance]);
                Assert.Single(view.Selection);
                view.Zoom = 100;
                Assert.Equal(32, view.Zoom);
                return;
            }

            Assert.Fail("No room with a drawable instance in the first 40 rooms.");
        });
    }

    [Fact]
    public async Task LayerEditsUndoAndSurviveAWrite()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.Layers?.Any(l => l.InstancesData is { Instances.Count: > 0 }) == true);
        var name = room.Name.Content ?? string.Empty;
        var layer = room.Layers.First(l => l.InstancesData is { Instances.Count: > 0 });
        var (id, layerName, depth, count, master) = (layer.LayerId, layer.LayerName.Content, layer.LayerDepth, room.Layers.Count, room.GameObjects.Count);

        var hide = new RoomLayerCommand(name, RoomLayerCommand.Visible, id, "0");
        hide.Apply(data);
        Assert.False(layer.IsVisible);
        hide.Revert(data);
        Assert.True(layer.IsVisible);

        var rename = new RoomLayerCommand(name, RoomLayerCommand.Rename, id, "renamed_layer");
        rename.Apply(data);
        Assert.Equal("renamed_layer", layer.LayerName.Content);
        rename.Revert(data);
        Assert.Equal(layerName, layer.LayerName.Content);

        var setDepth = new RoomLayerCommand(name, RoomLayerCommand.Depth, id, "-777");
        setDepth.Apply(data);
        Assert.Equal(-777, layer.LayerDepth);
        setDepth.Revert(data);
        Assert.Equal(depth, layer.LayerDepth);

        var delete = new RoomLayerCommand(name, RoomLayerCommand.Delete, id);
        delete.Apply(data);
        Assert.Equal(count - 1, room.Layers.Count);
        Assert.Equal(master - layer.InstancesData.Instances.Count, room.GameObjects.Count);
        delete.Revert(data);
        Assert.Equal((count, master), (room.Layers.Count, room.GameObjects.Count));

        var add = new RoomLayerCommand(name, RoomLayerCommand.AddInstances, 0, "new_instances", -500);
        add.Apply(data);
        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var read = back.Rooms.First(r => r.Name.Content == name).Layers.FirstOrDefault(l => l.LayerName.Content == "new_instances");
        Assert.NotNull(read);
        Assert.Equal(-500, read.LayerDepth);
        add.Revert(data);
        Assert.Equal(count, room.Layers.Count);
        Assert.Throws<InvalidOperationException>(() => new RoomLayerCommand(name, RoomLayerCommand.Rename, id, room.Layers.First(l => l.LayerId != id).LayerName.Content ?? "x").Apply(data));
    }

    [Fact]
    public void AFloodFillStaysInsideTheConnectedArea()
    {
        uint[][] rows = [[1, 1, 2], [1, 2, 2], [3, 1, 1]];
        var cells = RoomTilesCommand.FloodFill(rows, 0, 0, 9);
        Assert.Equal([(0, 0), (1, 0), (0, 1)], cells.Select(c => (c.X, c.Y)).OrderBy(c => c.Y).ThenBy(c => c.X));
        Assert.All(cells, c => Assert.Equal(9u, c.Value));
        Assert.Empty(RoomTilesCommand.FloodFill(rows, 5, 5, 9));
        Assert.Empty(RoomTilesCommand.FloodFill(rows, 0, 0, 1));
    }

    [Fact]
    public async Task PaintingTilesUndoesAndSurvivesAWrite()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        GameMakerData? data = null;
        GameMakerRoom? room = null;
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length))
        {
            data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            room = data.Rooms.FirstOrDefault(r => r.Layers?.Any(l => l.TilesData is { TileData.Length: > 0 }) == true);
            if (room is not null) break;
            data.Dispose();
        }

        Assert.SkipWhen(room is null || data is null, "no corpus game has a room with a tile layer");
        using var owned = data;
        var layer = room.Layers.First(l => l.TilesData is { TileData.Length: > 0 });
        var name = room.Name.Content ?? string.Empty;
        var rows = layer.TilesData.TileData;
        var before = rows[0][0];

        var paint = new RoomTilesCommand(name, layer.LayerId, [(0, 0, 7u), (1, 0, 8u)]);
        paint.Apply(data);
        Assert.Equal(7u, rows[0][0]);
        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var read = back.Rooms.First(r => r.Name.Content == name).Layers.First(l => l.LayerId == layer.LayerId).TilesData.TileData;
        Assert.Equal((7u, 8u), (read[0][0], read[0][1]));
        paint.Revert(data);
        Assert.Equal(before, rows[0][0]);
        Assert.Throws<InvalidOperationException>(() => new RoomTilesCommand(name, layer.LayerId, [(9999, 0, 1u)]).Apply(data));
        Assert.Equal(paint.Args["cells"], EditCommand.Create(paint.Kind, paint.Args).Args["cells"]);
    }

    [Fact]
    public async Task SettingInstanceFieldsUndoesAndTheObjectCanBeSwapped()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.GameObjects.Count > 0);
        var name = room.Name.Content ?? string.Empty;
        var instance = room.GameObjects[0];
        var before = (instance.X, instance.ScaleX, instance.Rotation, instance.ImageIndex, instance.ObjectDefinition);
        var other = data.GameObjects.First(o => o != instance.ObjectDefinition);

        foreach (var (field, value) in new[] { ("x", "123"), ("scalex", "2.5"), ("rotation", "90"), ("imageindex", "3"), ("object", other.Name.Content ?? string.Empty) })
        {
            var set = new RoomInstanceCommand(name, RoomInstanceCommand.Set, instance.InstanceID, objectName: field, value: value);
            set.Apply(data);
            set.Revert(data);
        }

        Assert.Equal(before, (instance.X, instance.ScaleX, instance.Rotation, instance.ImageIndex, instance.ObjectDefinition));
        var swap = new RoomInstanceCommand(name, RoomInstanceCommand.Set, instance.InstanceID, objectName: "object", value: other.Name.Content ?? string.Empty);
        swap.Apply(data);
        Assert.Same(other, instance.ObjectDefinition);
        Assert.Throws<InvalidDataException>(() => new RoomInstanceCommand(name, RoomInstanceCommand.Set, instance.InstanceID, objectName: "bogus", value: "1").Apply(data));
    }

    [Fact]
    public async Task EveryLayerKindCanBeAddedWrittenAndReadBack()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.Layers?.Count > 0);
        var name = room.Name.Content ?? string.Empty;
        var tileSet = data.Backgrounds.FirstOrDefault(b => b.GMS2TileWidth > 0 && b.GMS2TileCount > 0);
        var adds = new List<RoomLayerCommand>
        {
            new(name, RoomLayerCommand.AddAssets, 0, "t_assets", -300),
            new(name, RoomLayerCommand.AddBackground, 0, "t_background", 3000),
        };
        if (tileSet is not null) adds.Add(new(name, RoomLayerCommand.AddTiles, 0, "t_tiles", -200, tileSet.Name.Content ?? string.Empty));
        var count = room.Layers.Count;
        foreach (var add in adds) add.Apply(data);
        Assert.Equal(count + adds.Count, room.Layers.Count);

        var assets = room.Layers.First(l => l.LayerName.Content == "t_assets");
        var move = new RoomLayerCommand(name, RoomLayerCommand.XOffset, assets.LayerId, "12.5");
        move.Apply(data);
        Assert.Equal(12.5f, assets.XOffset);
        move.Revert(data);
        Assert.Equal(0f, assets.XOffset);

        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var layers = back.Rooms.First(r => r.Name.Content == name).Layers;
        Assert.Contains(layers, l => l.LayerName.Content == "t_assets" && l.AssetsData is not null);
        Assert.Contains(layers, l => l.LayerName.Content == "t_background" && l.BackgroundData is not null);
        if (tileSet is not null) Assert.Contains(layers, l => l.LayerName.Content == "t_tiles" && l.TilesData is { TileData.Length: > 0 });
        for (var k = adds.Count - 1; k >= 0; k--) adds[k].Revert(data);
        Assert.Equal(count, room.Layers.Count);
    }

    [Fact]
    public async Task ARoomSavesAsAPngOfItsSize()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var room = data.Rooms.First(r => r.Width > 0 && r.Height > 0 && r.Width <= 4000);
            var view = new Rooms.RoomView { Scene = await Rooms.RoomScene.BuildAsync(room, textures) };
            using var stream = new MemoryStream();
            view.SavePng(stream);
            Assert.True(stream.Length > 8);
            var bytes = stream.ToArray();
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
            using var info = new ImageMagick.MagickImage(bytes);
            Assert.Equal(((uint)room.Width, (uint)room.Height), (info.Width, info.Height));
        });
    }

    [Fact]
    public async Task AssetLayerElementsMoveDeleteAndAddAndTheSceneShowsThem()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        GameMakerData? data = null;
        GameMakerRoom? room = null;
        foreach (var file in Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length))
        {
            data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
            room = data.Rooms.FirstOrDefault(r => r.Layers?.Any(l => l.AssetsData is { Sprites.Count: > 0 }) == true);
            if (room is not null) break;
            data.Dispose();
        }

        Assert.SkipWhen(room is null || data is null, "no corpus game has a room with sprite elements");
        using var owned = data;
        var layer = room.Layers.First(l => l.AssetsData is { Sprites.Count: > 0 });
        var sprites = layer.AssetsData.Sprites;
        var first = sprites[0];
        var reference = new ElementRef(room.Name.Content ?? string.Empty, layer.LayerId, RoomElements.Sprite, first.Name.Content ?? string.Empty);
        var (x, y, count) = (first.X, first.Y, sprites.Count);

        var move = new RoomElementCommand(reference, RoomElementCommand.Move, x + 9, y + 4);
        move.Apply(data);
        Assert.Equal((x + 9, y + 4), (first.X, first.Y));
        move.Revert(data);
        Assert.Equal((x, y), (first.X, first.Y));

        var delete = new RoomElementCommand(reference, RoomElementCommand.Delete);
        delete.Apply(data);
        Assert.Equal(count - 1, sprites.Count);
        delete.Revert(data);
        Assert.Same(first, sprites[0]);

        var add = new RoomElementCommand(reference with { Name = "new_graphic" }, RoomElementCommand.Add, 5, 6, first.Sprite.Name.Content ?? string.Empty);
        add.Apply(data);
        using var stream = new MemoryStream();
        await GameMakerIO.WriteAsync(stream, data, cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        using var back = await GameMakerIO.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var read = back.Rooms.First(r => r.Name.Content == room.Name.Content).Layers.First(l => l.LayerId == layer.LayerId).AssetsData.Sprites;
        Assert.Contains(read, e => e.Name.Content == "new_graphic" && e.X == 5);
        add.Revert(data);
        Assert.Equal(count, sprites.Count);
        Assert.Equal(move.Args["name"], EditCommand.Create(move.Kind, move.Args).Args["name"]);

        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var scene = await Rooms.RoomScene.BuildAsync(room, textures);
            Assert.Contains(scene.Items, item => item.Element is GameMakerRoom.SpriteInstance);
        });
    }

    [Fact]
    public async Task SeveralAddedCopiesGetDistinctIdsInOneUndoStep()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.GameObjects.Count > 0);
        var name = room.Name.Content ?? string.Empty;
        var objectName = room.GameObjects[0].ObjectDefinition.Name.Content ?? string.Empty;
        var count = room.GameObjects.Count;
        var group = new GroupCommand([.. Enumerable.Range(0, 5).Select(k => (EditCommand)new RoomInstanceCommand(name, RoomInstanceCommand.Add, 0, k * 16, 0, objectName))]);
        group.Apply(data);
        Assert.Equal(count + 5, room.GameObjects.Count);
        Assert.Equal(room.GameObjects.Count, room.GameObjects.Select(o => o.InstanceID).Distinct().Count());
        var replayed = EditCommand.Create(group.Kind, group.Args);
        group.Revert(data);
        Assert.Equal(count, room.GameObjects.Count);
        replayed.Apply(data);
        Assert.Equal(count + 5, room.GameObjects.Count);
    }

    [Fact]
    public async Task TheElementListHoldsEveryInstanceOfTheRoomAndMarksTheSelection()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var room = data.Rooms.First(r => r.Layers?.Any(l => l.InstancesData is { Instances.Count: > 0 }) == true);
        await HeadlessApp.Run(() =>
        {
            var list = new Editor.RoomElementList(room);
            list.Show(null);
            Assert.True(list.Elements.OfType<GameMakerRoom.GameObject>().Count() >= room.Layers.Sum(l => l.InstancesData?.Instances.Count ?? 0));
            var picked = new List<object>();
            list.PickedChanged += picked.AddRange;
            list.Mark([list.Elements[0]]);
            Assert.Empty(picked);
            Assert.Single(list.SelectedItems!);
            Assert.StartsWith("inst_", Editor.RoomElementList.Describe(list.Elements.OfType<GameMakerRoom.GameObject>().First()), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RulersPickReadableStepsAndGuidesAreAddedAndRemoved()
    {
        Assert.Equal(1, Rooms.RoomRuler.NiceStep(0.7));
        Assert.Equal(2, Rooms.RoomRuler.NiceStep(1.5));
        Assert.Equal(50, Rooms.RoomRuler.NiceStep(31));
        Assert.Equal(100, Rooms.RoomRuler.NiceStep(61));
        await HeadlessApp.Run(() =>
        {
            var view = new Rooms.RoomView { Zoom = 2 };
            var changes = 0;
            view.ViewChanged += () => changes++;
            view.AddGuide(false, 100.4);
            view.AddGuide(true, 40);
            Assert.Equal([100.0], view.VerticalGuides);
            Assert.False(view.RemoveGuideAt(new Avalonia.Point(110, 300)));
            Assert.True(view.RemoveGuideAt(new Avalonia.Point(101, 300)));
            Assert.Empty(view.VerticalGuides);
            Assert.True(view.RemoveGuideAt(new Avalonia.Point(0, 41)));
            view.Zoom = 4;
            view.Offset = new Avalonia.Point(10, 10);
            Assert.Equal(2, changes);
        });
    }

    [Fact]
    public async Task APaletteValueCarriesItsFlagsAndTheViewDrawsFlippedTiles()
    {
        await HeadlessApp.Run(() =>
        {
            var scene = Rooms.RoomScene.Synthetic(2, 1);
            var plain = scene.Items[0];
            scene = new Rooms.RoomScene { Width = scene.Width, Height = scene.Height, Items = [plain, plain with { Flags = 1 }, plain with { Flags = 4 }], OwnsPages = false };
            var view = new Rooms.RoomView { Scene = scene, Width = 64, Height = 32 };
            view.Measure(new Avalonia.Size(64, 32));
            view.Arrange(new Avalonia.Rect(0, 0, 64, 32));
            using var target = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(64, 32));
            target.Render(view);
            Assert.Equal(3, view.DrawnItems);
        });
        Assert.Equal(0x10000000u | 5u, 5u | (1u & 7) << 28);
    }

    [Fact]
    public async Task ResourceViewersShowTheirPicturesPathsShadersAndMoments()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            using var textures = new Rooms.TextureBitmaps();
            var font = data.Fonts.FirstOrDefault(f => f.Texture?.TexturePage?.TextureData?.Image is { Width: > 1 });
            if (font is not null)
            {
                var view = new Editor.TextureItemView(font.Texture, textures);
                await view.Ready;
                Assert.NotNull(view.PictureSize);
                view.Zoom = 4;
                Assert.Equal(4, view.Zoom);
            }

            if (data.Paths.FirstOrDefault(p => p.Points.Count > 1) is { } path)
            {
                var view = new Editor.PathView(path) { Width = 200 };
                view.Measure(new Avalonia.Size(200, 240));
                view.Arrange(new Avalonia.Rect(0, 0, 200, 240));
                using var target = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(200, 240));
                target.Render(view);
            }

            if (data.Shaders.FirstOrDefault() is { } shader)
                Assert.All(new Editor.ShaderSourceView(shader).Sources, source => Assert.False(string.IsNullOrWhiteSpace(source.Text)));

        });
    }
}
