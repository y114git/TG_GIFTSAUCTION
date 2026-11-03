using System.Diagnostics;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ExplorerModelTests(ITestOutputHelper output)
{
    /// <summary>A model with <paramref name="total"/> entries spread over sprites, objects, code and strings. Not written to a file.</summary>
    internal static GameMakerData Generate(int total)
    {
        var data = GameMakerData.CreateNew2026LTS();
        var share = total / 4;
#pragma warning disable CA2000 // the data model owns what is added to it
        for (var i = 0; i < share; i++)
        {
            data.Sprites.Add(new GameMakerSprite { Name = new GameMakerString($"spr_{i}") });
            data.GameObjects.Add(new GameMakerGameObject { Name = new GameMakerString($"obj_{i}") });
            data.Code.Add(new GameMakerCode { Name = new GameMakerString($"gml_Object_obj_{i}_Create_0") });
            data.Strings.Add(new GameMakerString($"text {i}"));
        }
#pragma warning restore CA2000

        return data;
    }

    private static ExplorerModel NewModel() => new(key => key);

    [Fact]
    public void ListsResourceTypesThenTheDataGroupAndFiltersByNameAndTypePrefix()
    {
        using var data = TestData.Create(3, withCode: true);
        var model = NewModel();
        model.Load(data);

        var rows = model.Rows;
        Assert.Contains(rows, r => r is { Kind: ExplorerRowKind.Type, Key: "objects", Count: 3, Depth: 1 });
        Assert.Contains(rows, r => r is { Kind: ExplorerRowKind.Item, Text: "obj_1", Depth: 2 });
        Assert.Equal(ExplorerRowKind.Group, rows.Single(r => r.Key == ExplorerModel.DataGroup && r.Kind == ExplorerRowKind.Group).Kind);
        Assert.DoesNotContain(rows, r => r is { Kind: ExplorerRowKind.Item, Key: "code" }); // data types start collapsed

        model.SetFilter("OBJ:OBJ_1");
        Assert.Equal(["obj_1"], model.Rows.Where(r => r.IsItem).Select(r => r.Text));
        Assert.Equal(1, model.Matches);

        model.SetFilter("scr:");
        Assert.All(model.Rows.Where(r => r.IsItem), r => Assert.Equal("scripts", r.Key));
        Assert.Equal(data.Scripts.Count, model.Matches);

        model.SetFilter("probe");
        Assert.Contains(model.Rows, r => r is { IsItem: true, Key: "code", Text: "gml_Script_probe" }); // a filter opens collapsed types

        model.SetFilter("");
        Assert.Equal(model.Total, model.Matches);
    }

    [Fact]
    public void ToggleCollapsesAndExpandsAType()
    {
        using var data = TestData.Create(3);
        var model = NewModel();
        model.Load(data);
        var before = model.Rows.Count;

        model.Toggle("objects");
        Assert.Equal(before - 3, model.Rows.Count);
        model.Toggle("objects");
        Assert.Equal(before, model.Rows.Count);
    }

    [Fact]
    public void ReloadShowsRenamedEntries()
    {
        using var data = TestData.Create(1);
        var model = NewModel();
        model.Load(data);
        data.GameObjects[0].Name.Content = "obj_renamed";

        model.Reload();

        Assert.Contains(model.Rows, r => r.Text == "obj_renamed");
    }

    [Fact]
    public void FilterOn50000EntriesReturnsInUnder100Ms()
    {
        using var data = Generate(50_000);
        var model = NewModel();
        var load = Stopwatch.StartNew();
        model.Load(data);
        output.WriteLine($"load of {model.Total} entries: {load.ElapsedMilliseconds} ms");
        Assert.InRange(model.Total, 50_000, 50_100); // the generated entries plus the few of a new data file

        var slowest = 0L;
        foreach (var filter in new[] { "obj_4", "spr:12", "gml_Object_obj_9", "text 777", "zzz-none", "e", "" })
        {
            // Best of three: a shared CI runner can stall one run, a real slowdown slows all three.
            var ms = long.MaxValue;
            for (var run = 0; run < 3; run++)
            {
                var timer = Stopwatch.StartNew();
                model.SetFilter(filter);
                ms = Math.Min(ms, timer.ElapsedMilliseconds);
            }

            output.WriteLine($"filter '{filter}': {model.Matches} matches, {model.Rows.Count} rows, {ms} ms");
            slowest = Math.Max(slowest, ms);
        }

        Budget.AssertUnder(slowest, 100, "slowest filter", output);
    }
}
