using System.IO.Compression;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ModImportTests
{
    private const string Config = """
        {
          "config_version": "2.0.0", "id": "demo", "name": "Demo mod", "version": "1.2", "authors": [], "game": "undertale",
          "files": [
            { "source": "${mod_path}/a.g3mpatch", "target": "${game_path}/data.win", "type": "patch" },
            { "Chapter 2": [ { "source": "${mod_path}/b.xdelta", "target": "${game_path}/chapter2_windows/data.win", "type": "patch" },
                             { "source": "${mod_path}/c.g3mpatch", "target": "${game_path}/chapter2_windows/data.win", "type": "patch" } ] },
            { "source": "${mod_path}/readme.md", "type": "info" },
            { "source": "${mod_path}/img.png", "target": "${game_path}/img.png", "type": "overwrite" }
          ]
        }
        """;

    private static string MakeZip(TempDir dir, string config = Config)
    {
        var path = dir.File("mod.zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var w = new StreamWriter(zip.CreateEntry("mod_config.json").Open())) w.Write(config);
        foreach (var name in new[] { "a.g3mpatch", "b.xdelta", "c.g3mpatch" })
            using (var w = new StreamWriter(zip.CreateEntry(name).Open())) w.Write("payload " + name);
        return path;
    }

    [Fact]
    public void OnlyPatchOperationsAreReadIncludingThoseInGroupsAndTheyMatchADataFileByName()
    {
        using var dir = new TempDir();
        var mod = ModImport.Read(MakeZip(dir));
        Assert.Equal(("Demo mod", "1.2", "undertale"), (mod.Name, mod.Version, mod.Game));
        Assert.Equal(3, mod.Patches.Count);
        Assert.Equal(["a.g3mpatch", "b.xdelta", "c.g3mpatch"], ModImport.ForDataFile(mod, Path.Combine(Path.GetTempPath(), "UT", "DATA.WIN")).Select(p => p.Member));
        Assert.Empty(ModImport.ForDataFile(mod, "other.win"));
        Assert.Equal([true, false, true], mod.Patches.Select(p => p.Supported));
    }

    [Fact]
    public void APayloadIsCopiedOutUnderItsFileNameOnlyAndABadPathIsRefused()
    {
        using var dir = new TempDir();
        var zip = MakeZip(dir);
        var mod = ModImport.Read(zip);
        var path = ModImport.Extract(zip, mod.Patches[0], dir.File("out"));
        Assert.Equal("payload a.g3mpatch", File.ReadAllText(path));
        Assert.Equal(dir.File("out"), Path.GetDirectoryName(path));
        Assert.Throws<InvalidDataException>(() => ModImport.Extract(zip, new ModPatch("${mod_path}/../x.g3mpatch", "d.win", "patch"), dir.File("out")));
        Assert.Throws<InvalidDataException>(() => ModImport.Extract(zip, new ModPatch("${mod_path}/missing.g3mpatch", "d.win", "patch"), dir.File("out")));
    }

    [Fact]
    public void AZipWithoutAConfigOrWithBrokenJsonIsRejected()
    {
        using var dir = new TempDir();
        var bare = dir.File("bare.zip");
        using (var zip = ZipFile.Open(bare, ZipArchiveMode.Create)) zip.CreateEntry("x.txt");
        Assert.Throws<InvalidDataException>(() => ModImport.Read(bare));
        var broken = MakeZip(dir, "{ not json");
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ModImport.Read(broken));
    }
}
