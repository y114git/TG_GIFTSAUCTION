using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Project;
using Xunit;

namespace G3MLib.Tests;

public sealed class ProjectDataTypeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"G3MLib-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    public static IEnumerable<object[]> Fixtures() =>
        new[] { "data-2022.win", "data-2023.win", "data-2024.win", "data-2026.win" }.Select(f => new object[] { f });

    private string Prepare(string fixture, Action<GameMakerData> change = null)
    {
        Directory.CreateDirectory(_root);
        string baseFile = Path.Combine(_root, "base.win");
        using (FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture)))
        using (GameMakerData data = GameMakerIO.Read(input))
        {
            change?.Invoke(data);
            using FileStream output = File.Create(baseFile);
            GameMakerIO.Write(output, data);
        }
        return baseFile;
    }

    private G3MProjectResult Export(string baseFile)
    {
        using FileStream input = File.OpenRead(baseFile);
        using GameMakerData data = GameMakerIO.Read(input);
        return G3MProjectService.Export(data, Path.Combine(_root, "project"), baseFile, new G3MProjectOptions { Name = "Test" });
    }

    // A different recorded base makes the build import every unit instead of skipping unchanged ones.
    private string ForceImportAll(G3MProjectResult exported, string baseFile)
    {
        // Recompiled code is not guaranteed to match the original bytes, so it is left out.
        Directory.Delete(Path.Combine(_root, "project", "code"), true);
        JsonNode project = JsonNode.Parse(File.ReadAllText(exported.Path));
        project["baseFile"]["md5"] = new string('0', 32);
        File.WriteAllText(exported.Path, project.ToJsonString());
        string rebuilt = Path.Combine(_root, "rebuilt.win");
        G3MProjectService.Build(exported.Path, baseFile, rebuilt);
        return rebuilt;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ExportWritesTheDataFileOnlyTypes(string fixture)
    {
        G3MProjectResult exported = Export(Prepare(fixture));
        string project = Path.Combine(_root, "project");
        Assert.True(File.Exists(Path.Combine(project, "strings", "strings.json")));
        Assert.True(Directory.Exists(Path.Combine(project, "textures")));
        Assert.True(Directory.Exists(Path.Combine(project, "audio")));
        Assert.Empty(exported.Warnings);
    }

    // data-2023.win is left out: the integrity repair after an import rewrites its general info branch on purpose.
    [Theory]
    [InlineData("data-2022.win")]
    [InlineData("data-2024.win")]
    [InlineData("data-2026.win")]
    public void ImportingEveryExportedUnitUnchangedKeepsTheDataFileByteIdentical(string fixture)
    {
        string baseFile = Prepare(fixture);
        string rebuilt = ForceImportAll(Export(baseFile), baseFile);
        Assert.Equal(File.ReadAllBytes(baseFile), File.ReadAllBytes(rebuilt));
    }

    private static void AddUILayer(GameMakerData data)
    {
        GameMakerUILayer layer = new() { Name = data.Strings.MakeString("ui_probe"), DrawSpace = GameMakerUILayer.DrawSpaceKind.GUI, Visible = true };
        layer.Children.Add(new GameMakerUINode { Node = new GameMakerUIFlexPanel { Name = data.Strings.MakeString("panel"), GapRow = 4.5f } });
        layer.Children.Add(new GameMakerUINode
        {
            Node = new GameMakerUIGameObject { ObjectDefinition = data.GameObjects.ByName("obj_probe"), X = 12, Y = 34, Visible = true, Anchor = 3 }
        });
        layer.Children.Add(new GameMakerUINode { Node = new GameMakerUIEffectLayer { Enabled = true, EffectType = data.Strings.MakeString("blur") } });
        data.FORM.UILR.List.Add(new GameMakerUIRootNode { Node = layer });
    }

    [Fact]
    public void UILayersExportAndImportKeepTheDataFileByteIdentical()
    {
        string baseFile = Prepare("data-2026.win", AddUILayer);
        G3MProjectResult exported = Export(baseFile);
        Assert.True(File.Exists(Path.Combine(_root, "project", "uilayers", "ui_probe", "uilayer.json")));
        string rebuilt = ForceImportAll(exported, baseFile);
        Assert.Equal(File.ReadAllBytes(baseFile), File.ReadAllBytes(rebuilt));
    }

    [Fact]
    public void EditedUILayerIsAppliedOnBuild()
    {
        string baseFile = Prepare("data-2026.win", AddUILayer);
        G3MProjectResult exported = Export(baseFile);
        string file = Path.Combine(_root, "project", "uilayers", "ui_probe", "uilayer.json");
        Assert.Contains("\"GapRow\": 4.5", File.ReadAllText(file));
        File.WriteAllText(file, File.ReadAllText(file).Replace("\"GapRow\": 4.5", "\"GapRow\": 9"));
        string rebuilt = Path.Combine(_root, "rebuilt.win");
        G3MProjectService.Build(exported.Path, baseFile, rebuilt);
        using FileStream stream = File.OpenRead(rebuilt);
        using GameMakerData data = GameMakerIO.Read(stream);
        GameMakerUILayer layer = (GameMakerUILayer)data.FORM.UILR.List.Single(r => r.Name.Content == "ui_probe").Node;
        Assert.Equal(9f, ((GameMakerUIFlexPanel)layer.Children[0].Node).GapRow);
    }

    [Fact]
    public void EditedStringIsAppliedOnBuild()
    {
        string baseFile = Prepare("data-2026.win");
        G3MProjectResult exported = Export(baseFile);
        string file = Path.Combine(_root, "project", "strings", "strings.json");
        JsonArray strings = JsonNode.Parse(File.ReadAllText(file)).AsArray();
        strings[0] = "renamed string";
        File.WriteAllText(file, strings.ToJsonString());
        string rebuilt = Path.Combine(_root, "rebuilt.win");
        G3MProjectService.Build(exported.Path, baseFile, rebuilt);
        using FileStream stream = File.OpenRead(rebuilt);
        using GameMakerData data = GameMakerIO.Read(stream);
        Assert.Equal("renamed string", data.Strings[0].Content);
    }
}
