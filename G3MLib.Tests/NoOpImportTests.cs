using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Internal;
using G3MLib.Modding.Resources;
using Xunit;

namespace G3MLib.Tests;

public sealed class NoOpImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"G3MLib-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    public static IEnumerable<object[]> FixtureTypes() =>
        from fixture in new[] { "data-2022.win", "data-2023.win", "data-2024.win", "data-2026.win" }
        from type in ResourceTypeRegistry.AllTypes
        where type != "CodeEntries" && type != "TexturePageItems"
        select new object[] { fixture, type };

    [Theory]
    [MemberData(nameof(FixtureTypes))]
    public void ImportingAnUnchangedExportKeepsTheDataFileByteIdentical(string fixture, string type)
    {
        Directory.CreateDirectory(_root);
        string baseFile = Path.Combine(_root, fixture);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), baseFile);
        string exportDir = Path.Combine(_root, "export");

        using (FileStream input = File.OpenRead(baseFile))
        using (GameMakerData exportData = GameMakerIO.Read(input))
        {
            ResourceExportService.ExportTypes(exportData, exportDir, baseFile, [type]);
        }
        string typeDir = Path.Combine(exportDir, type);
        if (!Directory.Exists(typeDir))
        {
            return;
        }

        string output = Path.Combine(_root, "out.win");
        using (FileStream input = File.OpenRead(baseFile))
        using (GameMakerData data = GameMakerIO.Read(input))
        {
            ResourceImportService.SetPatchFileSystem(null);
            ResourceImportService.SetDataPaths(baseFile, output);
            ResourceImportService.Import(type, data, typeDir);
            using FileStream stream = new(output, FileMode.Create);
            GameMakerIO.Write(stream, data);
        }
        using (FileStream check = File.OpenRead(output))
        {
            using GameMakerData reread = GameMakerIO.Read(check);
        }
        Assert.Equal(File.ReadAllBytes(baseFile), File.ReadAllBytes(output));
    }

    [Fact]
    public void ImportingAnEditedRoomWritesAReadableFileThatKeepsLayerAssets()
    {
        Directory.CreateDirectory(_root);
        string baseFile = Path.Combine(_root, "data-2026.win");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"), baseFile);
        using (FileStream input = File.OpenRead(baseFile))
        using (GameMakerData exportData = GameMakerIO.Read(input))
        {
            ResourceExportService.ExportTypes(exportData, Path.Combine(_root, "export"), baseFile, ["Rooms"]);
        }
        string roomFile = Path.Combine(_root, "export", "Rooms", "rm_probe", "room.json");
        string json = File.ReadAllText(roomFile);
        Assert.Contains("\"width\": 1366,", json);
        File.WriteAllText(roomFile, json.Replace("\"width\": 1366,", "\"width\": 2000,"));

        string output = Path.Combine(_root, "out.win");
        int sequenceInstances;
        using (FileStream input = File.OpenRead(baseFile))
        using (GameMakerData data = GameMakerIO.Read(input))
        {
            sequenceInstances = data.Rooms.ByName("rm_probe").Layers.Sum(l => l.AssetsData?.Sequences?.Count ?? 0);
            ResourceImportService.SetPatchFileSystem(null);
            ResourceImportService.SetDataPaths(baseFile, output);
            ResourceImportService.Import("Rooms", data, Path.Combine(_root, "export", "Rooms"));
            using FileStream stream = new(output, FileMode.Create);
            GameMakerIO.Write(stream, data);
        }
        Assert.True(sequenceInstances > 0);
        using FileStream check = File.OpenRead(output);
        using GameMakerData reread = GameMakerIO.Read(check);
        GameMakerRoom room = reread.Rooms.ByName("rm_probe");
        Assert.Equal(2000u, room.Width);
        Assert.Equal(sequenceInstances, room.Layers.Sum(l => l.AssetsData?.Sequences?.Count ?? 0));
    }
}
