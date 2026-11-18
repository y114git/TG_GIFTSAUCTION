using System.IO.Compression;
using System.Text;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Plugins;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class UntrustedInputTests
{
    private static string Zip(TempDir dir, string name, params (string Name, string Text)[] members)
    {
        var path = dir.File(name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (member, text) in members)
        {
            using var writer = new StreamWriter(zip.CreateEntry(member).Open(), new UTF8Encoding(false));
            writer.Write(text);
        }

        return path;
    }

    private const string Manifest = "{\"format\":\"g3mstudio-pack\",\"formatVersion\":1,\"name\":\"n\",\"version\":\"1\",\"game\":\"g\",\"targetHash\":\"h\",\"gameVersion\":{\"major\":1},\"authors\":[],\"created\":\"2026-01-01T00:00:00Z\"}";

    [Fact]
    public void APackManifestWithMissingPartsIsRefusedAsInvalidData()
    {
        using var dir = new TempDir();
        var path = Zip(dir, "a.g3mpack", ("manifest.json", Manifest.Replace("\"authors\":[],", "", StringComparison.Ordinal)), ("changes.json", "[]"));
        Assert.Throws<InvalidDataException>(() => ModPackFile.Read(path));
    }

    [Theory]
    [InlineData("[{\"type\":\"Sprites\",\"name\":\"s\",\"op\":\"explode\"}]")]
    [InlineData("[null]")]
    [InlineData("[{\"type\":\"\",\"name\":\"s\",\"op\":\"add\"}]")]
    public void APackWithABrokenChangeIsRefusedAsInvalidData(string changes)
    {
        using var dir = new TempDir();
        var path = Zip(dir, "b.g3mpack", ("manifest.json", Manifest), ("changes.json", changes));
        Assert.Throws<InvalidDataException>(() => ModPackFile.Read(path));
    }

    [Fact]
    public void APayloadWithAnEntryOutsideTheFolderIsNotUnpacked()
    {
        using var dir = new TempDir();
        var payload = File.ReadAllBytes(Zip(dir, "p.zip", ("../escaped.txt", "x")));
        Assert.ThrowsAny<IOException>(() => CopyPackage.Unzip(payload, dir.File("out")));
        Assert.False(File.Exists(dir.File("escaped.txt")));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    public void APluginIdOfDotsIsRefused(string id)
    {
        using var dir = new TempDir();
        var path = Zip(dir, "plugin.zip", ("plugin.json", "{\"id\":\"" + id + "\",\"name\":\"N\",\"version\":\"1\"}"));
        Assert.Throws<InvalidDataException>(() => PluginHost.Inspect(path));
    }

    [Fact]
    public void ARunFileWithNullsLoadsWithEmptyTexts()
    {
        using var dir = new TempDir();
        var data = dir.File("data.win");
        File.WriteAllText(RunPlan.ProjectFile(data), "{\"Name\":null,\"Runner\":null,\"Arguments\":null,\"DebugOutput\":false,\"WorkingDir\":null,\"Environment\":null}");
        var profile = RunPlan.LoadProject(data);
        Assert.NotNull(profile);
        Assert.Equal("", profile.Arguments);
        Assert.NotNull(RunPlan.Unapproved(new AppSettings(), data));
    }

    [Fact]
    public void AViewStateWithNullPartsIsIgnored()
    {
        using var dir = new TempDir();
        var user = new UserData(dir.File("user"));
        var store = new ViewStateStore(user);
        var data = dir.File("data.win");
        Directory.CreateDirectory(user.Combine("view"));
        File.WriteAllText(user.Combine("view", UserData.KeyFor(data) + ".json"), "{\"Dock\":null,\"Tabs\":null}");
        Assert.Null(store.Load(data));
    }
}
