using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Project;
using Xunit;

namespace G3MLib.Tests;

public sealed class G3MProjectTests : IDisposable
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

    private string FixtureCopy(string fixture)
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, fixture);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), path);
        return path;
    }

    private static G3MProjectResult Export(string baseFile, string directory)
    {
        using FileStream input = File.OpenRead(baseFile);
        using GameMakerData data = GameMakerIO.Read(input);
        return G3MProjectService.Export(data, directory, baseFile, new G3MProjectOptions { Name = "Test" });
    }

    private static Dictionary<string, byte[]> Snapshot(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(directory, f).Replace('\\', '/'), File.ReadAllBytes);

    private static void EditObjectDepth(string directory)
    {
        string file = Path.Combine(directory, "objects", "obj_probe__idx0000", "object.json");
        string text = File.ReadAllText(file);
        Assert.Contains("\"depth\": 0,", text);
        File.WriteAllText(file, text.Replace("\"depth\": 0,", "\"depth\": 25,"));
    }

    [Fact]
    public void ExportWritesProjectFileAndGameMakerStyleFolders()
    {
        string baseFile = FixtureCopy("data-2026.win");
        string directory = Path.Combine(_root, "project");
        G3MProjectResult result = Export(baseFile, directory);
        Assert.Empty(result.Warnings);
        Assert.Equal(Path.Combine(directory, "Test.g3mproj"), result.Path);
        using JsonDocument project = JsonDocument.Parse(File.ReadAllText(result.Path));
        JsonElement root = project.RootElement;
        Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
        Assert.Equal("full", root.GetProperty("mode").GetString());
        Assert.Equal(new FileInfo(baseFile).Length, root.GetProperty("baseFile").GetProperty("size").GetInt64());
        Assert.Contains(root.GetProperty("resources").EnumerateArray(), r => r.GetProperty("id").GetProperty("path").GetString() == "sprites/spr_probe");
        Assert.True(File.Exists(Path.Combine(directory, "sprites", "spr_probe", "spr_probe.json")));
        Assert.True(File.Exists(Path.Combine(directory, "rooms", "rm_probe", "room.json")));
        Assert.True(root.GetProperty("resourceOrder").TryGetProperty("Rooms", out _));
        Assert.Empty(Directory.EnumerateDirectories(directory, ".staging-*"));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ExportTwiceProducesIdenticalBytes(string fixture)
    {
        string baseFile = FixtureCopy(fixture);
        Export(baseFile, Path.Combine(_root, "a"));
        Export(baseFile, Path.Combine(_root, "b"));
        Dictionary<string, byte[]> a = Snapshot(Path.Combine(_root, "a"));
        Dictionary<string, byte[]> b = Snapshot(Path.Combine(_root, "b"));
        Assert.Equal(a.Keys.Order(StringComparer.Ordinal), b.Keys.Order(StringComparer.Ordinal));
        Assert.All(a, pair => Assert.Equal(pair.Value, b[pair.Key]));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ExportingOverAnExistingProjectChangesNoFile(string fixture)
    {
        string baseFile = FixtureCopy(fixture);
        string directory = Path.Combine(_root, "project");
        Export(baseFile, directory);
        Dictionary<string, byte[]> first = Snapshot(directory);
        Export(baseFile, directory);
        Dictionary<string, byte[]> second = Snapshot(directory);
        Assert.Equal(first.Keys.Order(StringComparer.Ordinal), second.Keys.Order(StringComparer.Ordinal));
        Assert.All(first, pair => Assert.Equal(pair.Value, second[pair.Key]));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void TextFilesAreUtf8WithoutBomLfAndFinalNewline(string fixture)
    {
        string directory = Path.Combine(_root, "project");
        Export(FixtureCopy(fixture), directory);
        string[] extensions = [".json", ".gml", ".asm", ".g3mproj"];
        string[] files = [.. Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)))];
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            byte[] bytes = File.ReadAllBytes(file);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), $"BOM in {file}");
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.True(bytes.Length > 0 && bytes[^1] == (byte)'\n', $"missing final newline in {file}");
            Assert.False(bytes.Length > 1 && bytes[^2] == (byte)'\n', $"extra blank line at end of {file}");
        }
    }

    [Fact]
    public void ExportTextNormalizesJsonGmlAndAsmOnlyForProjectExports()
    {
        Directory.CreateDirectory(_root);
        string json = Path.Combine(_root, "a.json");
        string text = Path.Combine(_root, "a.txt");
        try
        {
            Modding.Resources.ExportText.NormalizeForProject = true;
            Modding.Resources.ExportText.WriteAllText(json, "{\r\n}\r\n\r\n", Encoding.UTF8);
            Modding.Resources.ExportText.WriteAllText(text, "x\r\n", Encoding.UTF8);
        }
        finally
        {
            Modding.Resources.ExportText.NormalizeForProject = false;
        }
        Assert.Equal("{\n}\n"u8.ToArray(), File.ReadAllBytes(json));
        Assert.True(File.ReadAllBytes(text).AsSpan().StartsWith(Encoding.UTF8.Preamble));

        Modding.Resources.ExportText.WriteAllText(json, "{\r\n}", Encoding.UTF8);
        Assert.True(File.ReadAllBytes(json).AsSpan().StartsWith(Encoding.UTF8.Preamble));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void FullExportThenBuildIsByteIdentical(string fixture)
    {
        string baseFile = FixtureCopy(fixture);
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        string rebuilt = Path.Combine(_root, "rebuilt.win");
        G3MProjectResult built = G3MProjectService.Build(exported.Path, baseFile, rebuilt);
        Assert.Empty(built.Warnings);
        Assert.Equal(0, built.Resources);
        Assert.Equal(exported.Resources, built.Unchanged);
        Assert.Equal(File.ReadAllBytes(baseFile), File.ReadAllBytes(rebuilt));
    }

    [Fact]
    public void BuildImportsOnlyChangedResources()
    {
        string baseFile = FixtureCopy("data-2026.win");
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        EditObjectDepth(Path.Combine(_root, "project"));

        string rebuilt = Path.Combine(_root, "rebuilt.win");
        G3MProjectResult built = G3MProjectService.Build(exported.Path, baseFile, rebuilt);
        Assert.Equal(1, built.Resources);
        Assert.Equal(exported.Resources - 1, built.Unchanged);
        using FileStream stream = File.OpenRead(rebuilt);
        using GameMakerData result = GameMakerIO.Read(stream);
        Assert.Equal(25, result.GameObjects.ByName("obj_probe").Depth);
    }

    [Fact]
    public void BuildWarnsAndImportsEverythingWhenBaseFileDiffers()
    {
        string baseFile = FixtureCopy("data-2022.win");
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        string recorded = JsonDocument.Parse(File.ReadAllText(exported.Path)).RootElement.GetProperty("baseFile").GetProperty("md5").GetString();
        File.WriteAllText(exported.Path, File.ReadAllText(exported.Path).Replace(recorded, new string('0', 32)));
        G3MProjectResult built = G3MProjectService.Build(exported.Path, baseFile, Path.Combine(_root, "rebuilt.win"));
        Assert.Contains(built.Warnings, w => w.Contains("base data file"));
        Assert.Equal(0, built.Unchanged);
        Assert.Equal(exported.Resources, built.Resources);
    }

    [Theory]
    [InlineData("\"formatVersion\": 2", "newer")]
    [InlineData("\"formatVersion\": 0", "unknown")]
    public void BuildRefusesAnUnsupportedFormatVersionWithAClearMessage(string replacement, string expected)
    {
        string baseFile = FixtureCopy("data-2022.win");
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        File.WriteAllText(exported.Path, File.ReadAllText(exported.Path).Replace("\"formatVersion\": 1", replacement));
        ProjectException error = Assert.Throws<ProjectException>(() => G3MProjectService.Build(exported.Path, baseFile, Path.Combine(_root, "rebuilt.win")));
        Assert.Contains(expected, error.Message);
        Assert.Contains("version", error.Message);
    }

    [Fact]
    public void ExportRefusesToOverwriteAProjectFromANewerVersion()
    {
        string baseFile = FixtureCopy("data-2022.win");
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        File.WriteAllText(exported.Path, File.ReadAllText(exported.Path).Replace("\"formatVersion\": 1", "\"formatVersion\": 2"));
        Assert.Throws<ProjectException>(() => Export(baseFile, Path.Combine(_root, "project")));
    }

    [Fact]
    public void BuildAppliesRecordedResourceOrder()
    {
        string baseFile = FixtureCopy("data-2026.win");
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        JsonNode project = JsonNode.Parse(File.ReadAllText(exported.Path));
        JsonArray rooms = project["resourceOrder"]["Rooms"].AsArray();
        string[] original = [.. rooms.Select(n => n.GetValue<string>())];
        Assert.True(original.Length > 1);
        string[] reversed = [.. original.Reverse()];
        project["resourceOrder"]["Rooms"] = new JsonArray([.. reversed.Select(n => (JsonNode)JsonValue.Create(n))]);
        File.WriteAllText(exported.Path, project.ToJsonString());

        string rebuilt = Path.Combine(_root, "rebuilt.win");
        G3MProjectService.Build(exported.Path, baseFile, rebuilt);
        using FileStream stream = File.OpenRead(rebuilt);
        using GameMakerData after = GameMakerIO.Read(stream);
        Assert.Equal(reversed, after.Rooms.Select(r => r.Name.Content));
    }

    [Fact]
    public void DeltaExportWritesOnlyTheNamedResources()
    {
        string baseFile = FixtureCopy("data-2026.win");
        using FileStream input = File.OpenRead(baseFile);
        using GameMakerData data = GameMakerIO.Read(input);
        string directory = Path.Combine(_root, "project");
        G3MProjectResult result = G3MProjectService.Export(data, directory, baseFile, new G3MProjectOptions
        {
            Name = "Delta",
            Mode = ProjectExportMode.Delta,
            Only = new Dictionary<string, IReadOnlySet<string>> { ["Rooms"] = new HashSet<string> { "rm_probe" } }
        });
        Assert.Equal(1, result.Resources);
        Assert.True(File.Exists(Path.Combine(directory, "rooms", "rm_probe", "room.json")));
        Assert.False(Directory.Exists(Path.Combine(directory, "sprites")));
        using JsonDocument project = JsonDocument.Parse(File.ReadAllText(result.Path));
        Assert.Equal("delta", project.RootElement.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task OpenReturnsTheResourceIndexWithoutReadingResourceFolders()
    {
        string baseFile = FixtureCopy("data-2026.win");
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        List<GameMakerProgress> reports = [];
        G3MProject project = await G3MProjectService.OpenAsync(exported.Path, new Progress<GameMakerProgress>(reports.Add), TestContext.Current.CancellationToken);
        Assert.Equal("Test", project.Name);
        Assert.Equal(G3MProjectService.FormatVersion, project.FormatVersion);
        Assert.Equal("full", project.Mode);
        Assert.Equal(Path.GetDirectoryName(exported.Path), project.Directory);
        Assert.Equal(new FileInfo(baseFile).Length, project.BaseFile.Size);
        Assert.Equal(exported.Resources, project.Resources.Count);
        G3MProjectResource sprite = project.Find("sprites/spr_probe");
        Assert.NotNull(sprite);
        Assert.Equal("Sprites", sprite.Type);
        Assert.Equal("spr_probe", sprite.Name);
        Assert.Equal(Path.Combine(project.Directory, "sprites", "spr_probe"), project.GetPath(sprite));
        Assert.All(project.OfType("Rooms"), r => Assert.StartsWith("rooms/", r.Path, StringComparison.Ordinal));
        Assert.Contains("rm_probe", project.ResourceOrder["Rooms"]);
        Assert.Null(project.Find("sprites/missing"));
        Assert.Equal(1.0, reports.Last().Fraction);
    }

    [Fact]
    public async Task OpenHonoursCancellationAndRejectsNewerFormats()
    {
        string baseFile = FixtureCopy("data-2026.win");
        G3MProjectResult exported = Export(baseFile, Path.Combine(_root, "project"));
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => G3MProjectService.OpenAsync(exported.Path, null, cancelled.Token));

        JsonNode node = JsonNode.Parse(File.ReadAllText(exported.Path));
        node["formatVersion"] = G3MProjectService.FormatVersion + 1;
        File.WriteAllText(exported.Path, node.ToJsonString());
        await Assert.ThrowsAsync<ProjectException>(() => G3MProjectService.OpenAsync(exported.Path, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ProjectException>(() => G3MProjectService.OpenAsync(Path.Combine(_root, "none.g3mproj"), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void UtmtLayoutExportWarnsAboutWhatTheLayoutCannotHold()
    {
        string baseFile = FixtureCopy("data-2026.win");
        using FileStream input = File.OpenRead(baseFile);
        using GameMakerData data = GameMakerIO.Read(input);
        ProjectContext context = new(data, baseFile, Path.Combine(_root, "out.win"), Path.Combine(_root, "utmt", "project.json"), "Utmt");
        context.ExportAll();
        string warning = Assert.Single(context.Warnings);
        Assert.Contains("event action", warning);
        Assert.Contains("layer effect", warning);
        Assert.Contains("texture", warning);
    }

    [Fact]
    public void DeltaBuildAppliesRecordedDeletions()
    {
        string baseFile = FixtureCopy("data-2026.win");
        using FileStream input = File.OpenRead(baseFile);
        using GameMakerData data = GameMakerIO.Read(input);
        Assert.NotNull(data.Rooms.ByName("Room2"));
        G3MProjectResult exported = G3MProjectService.Export(data, Path.Combine(_root, "project"), baseFile, new G3MProjectOptions
        {
            Name = "Delta",
            Mode = ProjectExportMode.Delta,
            Only = new Dictionary<string, IReadOnlySet<string>> { ["Rooms"] = new HashSet<string> { "rm_probe" } },
            Deleted = new Dictionary<string, IReadOnlySet<string>> { ["Rooms"] = new HashSet<string> { "Room2" } }
        });
        using (JsonDocument project = JsonDocument.Parse(File.ReadAllText(exported.Path)))
        {
            JsonElement deleted = Assert.Single(project.RootElement.GetProperty("deleted").EnumerateArray());
            Assert.Equal("Rooms", deleted.GetProperty("type").GetString());
            Assert.Equal("Room2", deleted.GetProperty("name").GetString());
        }

        string rebuilt = Path.Combine(_root, "rebuilt.win");
        G3MProjectResult built = G3MProjectService.Build(exported.Path, baseFile, rebuilt);
        Assert.Empty(built.Warnings);
        using FileStream stream = File.OpenRead(rebuilt);
        using GameMakerData after = GameMakerIO.Read(stream);
        Assert.Null(after.Rooms.ByName("Room2"));
        Assert.NotNull(after.Rooms.ByName("rm_probe"));
    }

    [Fact]
    public void ExportingAgainKeepsRecordedDeletions()
    {
        string baseFile = FixtureCopy("data-2026.win");
        using FileStream input = File.OpenRead(baseFile);
        using GameMakerData data = GameMakerIO.Read(input);
        string directory = Path.Combine(_root, "project");
        G3MProjectOptions first = new() { Name = "Keep", Deleted = new Dictionary<string, IReadOnlySet<string>> { ["Rooms"] = new HashSet<string> { "Room2" } } };
        G3MProjectService.Export(data, directory, baseFile, first);
        G3MProjectResult second = G3MProjectService.Export(data, directory, baseFile, new G3MProjectOptions { Name = "Keep" });
        using JsonDocument project = JsonDocument.Parse(File.ReadAllText(second.Path));
        Assert.Single(project.RootElement.GetProperty("deleted").EnumerateArray());
    }
}
