using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Tests;

#pragma warning disable CA2000 // the room, layer and tile set belong to the data, which is disposed
public sealed class AutoTileTests
{
    private static readonly (int Dx, int Dy, int Bit)[] Around = [(0, -1, AutoTiler.N), (1, -1, AutoTiler.NE), (1, 0, AutoTiler.E), (1, 1, AutoTiler.SE), (0, 1, AutoTiler.S), (-1, 1, AutoTiler.SW), (-1, 0, AutoTiler.W), (-1, -1, AutoTiler.NW)];

    /// <summary>A rule set whose tile is its slot plus an offset, so a result names the slot that was chosen.</summary>
    private static AutoTileRuleSet Identity(AutoTileLayout layout, string name = "r", int terrain = 1, int offset = 1) =>
        new(name, "ts", layout, terrain, AutoTiler.Slots(layout).ToDictionary(s => s, s => s + offset));

    private static uint[][] Grid(int width, int height) => [.. Enumerable.Range(0, height).Select(_ => new uint[width])];

    private static void Apply(uint[][] rows, List<(int X, int Y, uint Value)> changes)
    {
        foreach (var (x, y, v) in changes) rows[y][x] = v;
    }

    /// <summary>A 3 by 3 grid whose middle cell has exactly the neighbours of the mask, all terrain of tile 1.</summary>
    private static uint[][] Around3(int mask)
    {
        var rows = Grid(3, 3);
        rows[1][1] = 1;
        foreach (var (dx, dy, bit) in Around)
            if ((mask & bit) != 0) rows[1 + dy][1 + dx] = 1;
        return rows;
    }

    [Fact]
    public void TheLayoutsHave47And16And256Slots()
    {
        Assert.Equal(47, AutoTiler.Slots(AutoTileLayout.Blob47).Count);
        Assert.Equal(16, AutoTiler.Slots(AutoTileLayout.Edge16).Count);
        Assert.Equal(256, AutoTiler.Slots(AutoTileLayout.Blob256).Count);
        Assert.Equal(47, Enumerable.Range(0, 256).Select(AutoTiler.Canonical).Distinct().Count());
    }

    [Fact]
    public void ACornerOnlyCountsWithBothEdgesBesideIt()
    {
        Assert.Equal(AutoTiler.N | AutoTiler.E, AutoTiler.Canonical(AutoTiler.N | AutoTiler.E));
        Assert.Equal(AutoTiler.N, AutoTiler.Canonical(AutoTiler.N | AutoTiler.NE));
        Assert.Equal(AutoTiler.N | AutoTiler.E | AutoTiler.NE, AutoTiler.Canonical(AutoTiler.N | AutoTiler.E | AutoTiler.NE));
    }

    [Fact]
    public void EveryMaskOfEightNeighboursGivesTheTileOfItsSlotInEveryLayout()
    {
        foreach (var layout in Enum.GetValues<AutoTileLayout>())
        {
            var rules = Identity(layout); // slot s has tile s + 1, so the terrain tile 1 of the grid is the tile of slot 0
            for (var mask = 0; mask < 256; mask++)
            {
                var rows = Around3(mask);
                Assert.Equal(mask, AutoTiler.Mask(1, 1, (x, y) => (x, y) != (1, 1) && rows[y][x] != 0));
                Apply(rows, AutoTiler.Retile(rows, rules, [rules], 1, 1, 1, 1));
                Assert.Equal((uint)(AutoTiler.SlotOf(layout, mask) + 1), rows[1][1]);
            }
        }
    }

    [Fact]
    public void EveryCombinationOfFourEdgesGivesTheTileOfItsSlot()
    {
        var rules = Identity(AutoTileLayout.Edge16);
        for (var edges = 0; edges < 16; edges++)
        {
            var rows = Around3((edges & 1) * AutoTiler.N | (edges >> 1 & 1) * AutoTiler.E | (edges >> 2 & 1) * AutoTiler.S | (edges >> 3 & 1) * AutoTiler.W);
            Apply(rows, AutoTiler.Retile(rows, rules, [rules], 1, 1, 1, 1));
            Assert.Equal((uint)(edges + 1), rows[1][1]);
        }
    }

    [Fact]
    public void RetilingKeepsTheFlagBitsOfACell()
    {
        const uint mirror = 0x10000000;
        var rules = Identity(AutoTileLayout.Edge16);
        var rows = Around3(AutoTiler.N);
        rows[1][1] |= mirror; // terrain tile 1 with the mirror flag
        Apply(rows, AutoTiler.Retile(rows, rules, [rules], 1, 1, 1, 1));
        Assert.Equal(mirror | (uint)(AutoTiler.SlotOf(AutoTileLayout.Edge16, AutoTiler.N) + 1), rows[1][1]);

        var grid = Grid(3, 1);
        grid[0][0] = 1u | mirror;
        Apply(grid, AutoTiler.Paint(grid, rules, [rules], [(1, 0)], false)); // the painted cell joins its flagged neighbour
        Assert.Equal(mirror, grid[0][0] & mirror);
        Assert.Equal(0u, grid[0][1] & ~0x0007FFFFu);
    }

    [Fact]
    public void PaintingRetilesTheCellAndItsNeighboursAndErasingGoesBack()
    {
        var rules = Identity(AutoTileLayout.Edge16);
        var rows = Grid(5, 3);
        Apply(rows, AutoTiler.Paint(rows, rules, [rules], [(1, 1)], false));
        Assert.Equal(1u, rows[1][1]); // alone: slot 0
        Apply(rows, AutoTiler.Paint(rows, rules, [rules], [(2, 1)], false));
        Assert.Equal(2u + 1, rows[1][1]); // east neighbour: slot 2
        Assert.Equal(8u + 1, rows[1][2]); // west neighbour: slot 8
        Apply(rows, AutoTiler.Paint(rows, rules, [rules], [(2, 1)], true));
        Assert.Equal(0u, rows[1][2]);
        Assert.Equal(1u, rows[1][1]);
    }

    [Fact]
    public void FlagsAreNotWrittenAndOtherTilesAreLeftAlone()
    {
        var rules = Identity(AutoTileLayout.Edge16);
        var rows = Grid(3, 3);
        rows[0][0] = 99u | 1u << 28;
        var changes = AutoTiler.Paint(rows, rules, [rules], [(1, 1)], false);
        Assert.All(changes, c => Assert.Equal(0u, c.Value & 0xF0000000));
        Assert.DoesNotContain(changes, c => (c.X, c.Y) == (0, 0));
    }

    [Fact]
    public void TheEdgeOfTheLayerIsEmptyUnlessTheOptionMakesItTerrain()
    {
        var rules = Identity(AutoTileLayout.Edge16);
        var rows = Grid(3, 3);
        Assert.Equal((0, 0, 1u), Assert.Single(AutoTiler.Paint(rows, rules, [rules], [(0, 0)], false)));
        var outside = AutoTiler.Paint(rows, rules, [rules], [(0, 0)], false, outsideIsTerrain: true);
        Assert.Equal((0, 0, 1u + 1 + 8), Assert.Single(outside)); // north (1) and west (8) count
        Assert.Empty(AutoTiler.Paint(rows, rules, [rules], [(-1, 0), (3, 3)], false)); // cells outside are ignored
    }

    [Fact]
    public void RuleSetsOfOtherTerrainsDoNotFightButTheSameTerrainSharesNeighbours()
    {
        var grass = Identity(AutoTileLayout.Edge16, "grass", 1, 1);
        var sand = Identity(AutoTileLayout.Edge16, "sand", 2, 100);
        var variant = Identity(AutoTileLayout.Edge16, "flowers", 1, 200);
        var all = new[] { grass, sand, variant };
        var rows = Grid(3, 1);
        rows[0][0] = 100; // a sand tile beside the cell to paint
        Assert.Equal((1, 0, 1u), Assert.Single(AutoTiler.Paint(rows, grass, all, [(1, 0)], false))); // sand is no neighbour and is not retiled
        rows[0][0] = 200; // a tile of another rule set with the same terrain
        var changes = AutoTiler.Paint(rows, grass, all, [(1, 0)], false);
        Assert.Contains((1, 0, 8u + 1), changes); // the cell sees a west neighbour
        Assert.Contains((0, 0, 2u + 200), changes); // and the neighbour is retiled by its own rule set
    }

    [Fact]
    public void FillPaintsTheConnectedNonTerrainArea()
    {
        var rules = Identity(AutoTileLayout.Edge16);
        var rows = Grid(3, 3);
        rows[1][0] = rows[1][1] = rows[1][2] = 1; // a terrain wall across the middle
        Assert.Equal(3, AutoTiler.FillArea(rows, rules, [rules], 0, 0).Count);
        Assert.Empty(AutoTiler.FillArea(rows, rules, [rules], 1, 1));
    }

    [Fact]
    public void RetileTouchesOnlyTerrainCellsInsideTheRectangle()
    {
        var rules = Identity(AutoTileLayout.Edge16);
        var rows = Grid(3, 1);
        rows[0][0] = rows[0][1] = 1; // both hold slot 0 though they touch
        var changes = AutoTiler.Retile(rows, rules, [rules], 0, 0, 1, 1);
        Assert.Equal((0, 0, 2u + 1), Assert.Single(changes));
        Assert.Empty(AutoTiler.Retile(rows, rules, [rules], 2, 0, 1, 1)); // an empty cell is not terrain
    }

    [Fact]
    public void AutoFillFollowsTheStandardSheet()
    {
        var filled = AutoTiler.AutoFill(AutoTileRuleSet.Empty("e", "ts", AutoTileLayout.Edge16), 1, 8, 64);
        Assert.Equal(16, filled.Slots.Count);
        Assert.Equal(1, filled.Tile(0));
        Assert.Equal(1 + 8 + 1, filled.Tile(5)); // second row of the template, second column
        Assert.Equal(1 + 3 * 8 + 3, filled.Tile(15));
        Assert.Equal(7, AutoTiler.AutoFill(AutoTileRuleSet.Empty("e", "ts", AutoTileLayout.Blob47), 1, 8, 8).Slots.Count); // the set ends after tile 7
    }

    [Fact]
    public void TheStoreSavesAtomicallyReloadsAndSharesRuleSetsByTileSet()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var store = new AutoTileStore(userData, dir.File("game.win"));
        var changes = 0;
        store.Changed += () => changes++;
        store.Save(AutoTiler.AutoFill(AutoTileRuleSet.Empty("edges", "ts", AutoTileLayout.Edge16, 2), 1, 8, 64));
        store.Save(AutoTileRuleSet.Empty("other", "ts2", AutoTileLayout.Blob47));
        Assert.Equal(2, changes);
        Assert.Empty(Directory.GetFiles(userData.Combine("autotile"), "*.tmp"));
        var again = new AutoTileStore(userData, dir.File("game.win"));
        Assert.Equal(["edges"], again.For("ts").Select(r => r.Name));
        Assert.Equal(2, again.For("ts")[0].Terrain);
        Assert.Equal(16, again.For("ts")[0].Slots.Count);
        Assert.Equal("edges 2", store.FreeName("ts", "edges"));
        store.Save(store.For("ts")[0] with { Name = "renamed" }, "edges");
        Assert.Equal(["renamed"], store.For("ts").Select(r => r.Name));
        store.Remove("ts", "renamed");
        Assert.Empty(new AutoTileStore(userData, dir.File("game.win")).For("ts"));
        Assert.Empty(new AutoTileStore(userData, dir.File("other.win")).All);
    }

    [Fact]
    public void ARenamedTileSetKeepsItsRuleSetsAndAFailedWriteIsReportedNotThrown()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var store = new AutoTileStore(userData, dir.File("game.win"));
        store.Save(AutoTileRuleSet.Empty("edges", "ts", AutoTileLayout.Edge16));
        store.RenameTileSet("ts", "ts_new");
        Assert.Empty(store.For("ts"));
        Assert.Equal(["edges"], new AutoTileStore(userData, dir.File("game.win")).For("ts_new").Select(r => r.Name));

        var file = Directory.GetFiles(userData.Combine("autotile"), "*.json").Single();
        File.Delete(file);
        Directory.CreateDirectory(file); // the sidecar cannot be replaced
        Exception? failure = null;
        store.Failed += e => failure = e;
        store.Remove("ts_new", "edges");
        Assert.NotNull(failure);
        Assert.Single(store.For("ts_new")); // nothing changed in memory
    }

    [Fact]
    public void ARuleSetExportsAndImportsAndADamagedFileIsRejected()
    {
        var rules = AutoTiler.AutoFill(AutoTileRuleSet.Empty("edges", "ts", AutoTileLayout.Blob47, 3), 1, 8, 64);
        var back = AutoTileStore.Import(AutoTileStore.Export(rules))!;
        Assert.Equal((rules.Name, rules.TileSet, rules.Layout, rules.Terrain), (back.Name, back.TileSet, back.Layout, back.Terrain));
        Assert.Equal(rules.Slots.OrderBy(p => p.Key), back.Slots.OrderBy(p => p.Key));
        Assert.Null(AutoTileStore.Import("{ not json"));
        Assert.Null(AutoTileStore.Import("{\"name\":\"\"}"));
        var wild = AutoTileStore.Import("{\"name\":\"x\",\"tileSet\":\"t\",\"layout\":\"Edge16\",\"terrain\":999,\"slots\":{\"3\":4,\"99\":5,\"2\":-1}}")!;
        Assert.Equal(255, wild.Terrain);
        Assert.Equal([(3, 4)], wild.Slots.Select(p => (p.Key, p.Value)));
    }

    [Fact]
    public void TheDefaultLayoutOfBadSettingsFallsBackToTheBlob()
    {
        using var dir = new TempDir();
        using var store = new SettingsStore(new UserData(dir.File("user")));
        store.Update(x => x with { AutoTile = new AutoTileOptions { DefaultLayout = (AutoTileLayout)9, StartOn = true } });
        Assert.Equal((AutoTileLayout.Blob47, true), (store.Current.AutoTile.DefaultLayout, store.Current.AutoTile.StartOn));
    }

    [Fact]
    public async Task TheRuleSetEditorCreatesAssignsFillsAndMovesRuleSetsThroughJson()
    {
        using var dir = new TempDir();
        var store = new AutoTileStore(new UserData(dir.File("user")), dir.File("game.win"));
        var file = dir.File("rules.json");
        using var set = new GameMakerBackground { Name = new GameMakerString { Content = "ts_ui" }, GMS2TileWidth = 16, GMS2TileHeight = 16, GMS2TileColumns = 8, GMS2TileCount = 64 };
        await HeadlessApp.Run(async () =>
        {
            var view = new AutoTileView(set, store) { FilePicker = (_, _) => Task.FromResult<string?>(file) };
            Assert.Null(view.Current);
            view.New(AutoTileLayout.Edge16);
            Assert.Equal(AutoTileLayout.Edge16, view.Current!.Layout);
            Assert.Equal(0, view.SelectedSlot);
            Assert.True(view.Assign(5)); // slot 0 gets tile 5 and the next slot is selected
            Assert.Equal(1, view.SelectedSlot);
            Assert.False(view.Assign(64)); // outside the set
            Assert.Equal(5, store.For("ts_ui")[0].Tile(0));
            view.AutoFill(1);
            Assert.Equal(16, store.For("ts_ui")[0].Slots.Count);
            await view.ExportAsync();
            view.Delete();
            Assert.Null(view.Current);
            await view.ImportAsync();
            Assert.Equal(16, view.Current!.Slots.Count);
            Assert.Equal("Terrain", view.Current.Name);
            await view.ImportAsync(); // the same name again gets a number
            Assert.Equal(["Terrain", "Terrain 2"], store.For("ts_ui").Select(r => r.Name));
        });
    }

    [Fact]
    public async Task TheBrushPaintsErasesFillsAndRetilesAsOneUndoStepPerStroke()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        var data = workspace.Data;
        var set = new GameMakerBackground { Name = data.Strings.MakeString("ts_auto", out _), GMS2TileWidth = 16, GMS2TileHeight = 16, GMS2TileColumns = 8, GMS2TileCount = 64 };
        data.Backgrounds.Add(set);
        var room = new GameMakerRoom { Name = data.Strings.MakeString("rm_auto", out _), Layers = [] };
        var layer = new Layer { LayerName = data.Strings.MakeString("tiles", out _), LayerType = LayerType.Tiles, LayerId = 1, ParentRoom = room };
        layer.Data = new Layer.LayerTilesData { ParentLayer = layer, Background = set, TilesX = 6, TilesY = 4, TileData = Grid(6, 4) };
        room.Layers.Add(layer);
        data.Rooms.Add(room);
        var store = new AutoTileStore(userData, path);
        store.Save(AutoTiler.AutoFill(AutoTileRuleSet.Empty("edges", "ts_auto", AutoTileLayout.Edge16), 1, 8, 64));
        var rules = store.For("ts_auto")[0];
        var rows = layer.TilesData.TileData;
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var preview = new RoomPreview(room, textures, workspace) { AutoTiles = store };
            await preview.Ready;
            preview.ChooseTileLayer(layer);
            Assert.Null(preview.ActiveRules);
            Assert.True(preview.ToggleAutoTile());
            Assert.Equal("edges", preview.ActiveRules!.Name);

            preview.Paint(layer, RoomTool.Line, [(1, 1), (2, 1), (3, 1)]);
            Assert.Equal(rules.Tile(2), (int)rows[1][1]); // east only
            Assert.Equal(rules.Tile(10), (int)rows[1][2]); // east and west
            Assert.Equal(rules.Tile(8), (int)rows[1][3]); // west only
            workspace.History.Undo(); // the whole stroke was one step
            Assert.All(rows, r => Assert.All(r, c => Assert.Equal(0u, c)));
            workspace.History.Redo();

            preview.Paint(layer, RoomTool.Erase, [(2, 1)]);
            Assert.Equal(0u, rows[1][2]);
            Assert.Equal(rules.Tile(0), (int)rows[1][1]);

            preview.Paint(layer, RoomTool.Fill, [(0, 3)]); // everything connected to this empty cell
            Assert.All(rows[3], c => Assert.NotEqual(0u, c));

            rows[2][4] = 63; // a tile of no rule set inside the selection is not terrain, so a retile leaves it
            preview.View.TileSelection = (0, 0, 6, 4);
            var before = rows.Select(r => (uint[])r.Clone()).ToArray();
            Assert.True(preview.RetileSelection());
            Assert.Equal(before[2][4], rows[2][4]);

            Assert.True(preview.ChooseRules(null));
            Assert.Null(preview.ActiveRules);
            Assert.False(preview.ChooseRules("missing"));
        });
    }
}
