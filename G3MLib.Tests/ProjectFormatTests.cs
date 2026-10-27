using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.DataFile.Project;
using Xunit;

namespace G3MLib.Tests;

public sealed class ProjectFormatTests
{
    private static string NewTempDirectory() => Path.Combine(Path.GetTempPath(), $"G3MLib-{Guid.NewGuid():N}");

    private static GameMakerData ReadFixture(string fileName)
    {
        using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        return GameMakerIO.Read(input);
    }

    private static IEnumerable<IProjectAsset> ExportableAssets(GameMakerData data)
    {
        IEnumerable<IProjectAsset>[] groups =
        [
            data.AnimationCurves, data.Backgrounds, data.Code, data.Fonts, data.GameObjects, data.ParticleSystems,
            data.Paths, data.Rooms, data.Scripts, data.Sequences, data.Shaders, data.Sounds, data.Sprites
        ];
        return groups.Where(g => g is not null).SelectMany(g => g).Where(a => a?.ProjectExportable ?? false);
    }

    private static Dictionary<string, byte[]> Snapshot(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(directory, f).Replace('\\', '/'), File.ReadAllBytes);

    private static void ExportSelected(GameMakerData data, string directory)
    {
        ProjectContext context = new(data, "input.win", "output.win", Path.Combine(directory, "project.json"), "Test");
        foreach (IProjectAsset asset in ExportableAssets(data))
        {
            context.MarkAssetForExport(asset);
        }
        context.Export(clearMarkedAssets: true);
    }

    public static IEnumerable<object[]> Fixtures() =>
        new[] { "data-2022.win", "data-2023.win", "data-2024.win", "data-2026.win" }.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ExportTwiceProducesIdenticalBytes(string fileName)
    {
        string first = NewTempDirectory();
        string second = NewTempDirectory();
        try
        {
            using GameMakerData data = ReadFixture(fileName);
            ExportSelected(data, first);
            ExportSelected(data, second);
            Dictionary<string, byte[]> a = Snapshot(first);
            Dictionary<string, byte[]> b = Snapshot(second);
            Assert.Equal(a.Keys.Order(StringComparer.Ordinal), b.Keys.Order(StringComparer.Ordinal));
            Assert.All(a, pair => Assert.Equal(pair.Value, b[pair.Key]));
        }
        finally
        {
            Directory.Delete(first, true);
            Directory.Delete(second, true);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ExportedTextFilesAreUtf8WithoutBomLfAndFinalNewline(string fileName)
    {
        string directory = NewTempDirectory();
        try
        {
            using GameMakerData data = ReadFixture(fileName);
            ExportSelected(data, directory);
            string[] files = [.. Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".json", StringComparison.Ordinal) || f.EndsWith(".gml", StringComparison.Ordinal))];
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
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ExportAllWritesEveryExportableAssetAndRecordsFullMode(string fileName)
    {
        string directory = NewTempDirectory();
        string mainFile = Path.Combine(directory, "project.json");
        try
        {
            using GameMakerData data = ReadFixture(fileName);
            ProjectContext context = new(data, "input.win", "output.win", mainFile, "Test");
            context.ExportAll();
            Assert.False(context.HasUnexportedAssets);
            string[] expected = [.. ExportableAssets(data).Select(a => a.ProjectName).Distinct(StringComparer.Ordinal)];
            string[] written = [.. Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".json", StringComparison.Ordinal) || f.EndsWith(".gml", StringComparison.Ordinal))
                .Select(Path.GetFileNameWithoutExtension)];
            Assert.All(expected, name => Assert.Contains(written, w => w == name || w.StartsWith(name, StringComparison.Ordinal)));

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(mainFile));
            Assert.Equal("full", manifest.RootElement.GetProperty("G3M").GetProperty("Mode").GetString());
            Assert.Equal("Test", manifest.RootElement.GetProperty("Name").GetString());

            ProjectContext reopened = ProjectContext.CreateWithDirectories(directory, directory, mainFile);
            using GameMakerData target = GameMakerData.CreateNew2026LTS();
            reopened.Import(target, new GameFileNoOpBackup());
            Assert.Equal(ProjectExportMode.Full, reopened.ExportMode);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SelectiveExportKeepsManifestFreeOfFullMode()
    {
        string directory = NewTempDirectory();
        try
        {
            using GameMakerData data = ReadFixture("data-2022.win");
            ExportSelected(data, directory);
            Assert.DoesNotContain("G3M", File.ReadAllText(Path.Combine(directory, "project.json")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ExportAllTwiceProducesIdenticalBytes(string fileName)
    {
        string first = NewTempDirectory();
        string second = NewTempDirectory();
        try
        {
            using GameMakerData data = ReadFixture(fileName);
            new ProjectContext(data, "input.win", "output.win", Path.Combine(first, "project.json"), "Test").ExportAll();
            new ProjectContext(data, "input.win", "output.win", Path.Combine(second, "project.json"), "Test").ExportAll();
            Dictionary<string, byte[]> a = Snapshot(first);
            Dictionary<string, byte[]> b = Snapshot(second);
            Assert.Equal(a.Keys.Order(StringComparer.Ordinal), b.Keys.Order(StringComparer.Ordinal));
            Assert.All(a, pair => Assert.Equal(pair.Value, b[pair.Key]));
        }
        finally
        {
            Directory.Delete(first, true);
            Directory.Delete(second, true);
        }
    }

    private static (string Directory, string Base, string Main) ExportWithBase(string fixture, out GameMakerData data)
    {
        string directory = NewTempDirectory();
        Directory.CreateDirectory(directory);
        string basePath = Path.Combine(directory, "base.win");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), basePath);
        string mainFile = Path.Combine(directory, "project", "project.json");
        data = ReadFixture(fixture);
        new ProjectContext(data, basePath, Path.Combine(directory, "out.win"), mainFile, "Test").ExportAll();
        return (directory, basePath, mainFile);
    }

    [Fact]
    public void ExportAllRecordsBaseFileSizeAndMd5()
    {
        (string directory, string basePath, string mainFile) = ExportWithBase("data-2022.win", out GameMakerData data);
        try
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(mainFile));
            JsonElement g3m = manifest.RootElement.GetProperty("G3M");
            Assert.Equal(new FileInfo(basePath).Length, g3m.GetProperty("BaseFileSize").GetInt64());
            Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(File.ReadAllBytes(basePath))), g3m.GetProperty("BaseFileMd5").GetString());
        }
        finally
        {
            data.Dispose();
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ImportWarnsWhenBaseFileDiffersFromRecordedIdentity()
    {
        (string directory, string basePath, string mainFile) = ExportWithBase("data-2022.win", out GameMakerData data);
        try
        {
            ProjectContext matching = ProjectContext.CreateWithDataFilePaths(basePath, Path.Combine(directory, "out.win"), mainFile);
            matching.Import(existingFileBackup: new GameFileNoOpBackup());
            Assert.Empty(matching.Warnings);

            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2023.win"), basePath, true);
            List<string> reported = [];
            ProjectContext other = ProjectContext.CreateWithDataFilePaths(basePath, Path.Combine(directory, "out2.win"), mainFile);
            other.ScriptWarningHandler = reported.Add;
            other.Import(existingFileBackup: new GameFileNoOpBackup());
            Assert.Single(other.Warnings);
            Assert.Equal(other.Warnings, reported);
            Assert.Contains("base data file", other.Warnings[0]);
        }
        finally
        {
            data.Dispose();
            Directory.Delete(directory, true);
        }
    }
}
