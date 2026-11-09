using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class IncludedFilesTests
{
    private static (IncludedFiles Files, string Folder, UserData Home) Setup(TempDir dir)
    {
        var folder = Directory.CreateDirectory(dir.File("game")).FullName;
        File.WriteAllText(Path.Combine(folder, "data.win"), "x");
        File.WriteAllText(Path.Combine(folder, "data.win.bak"), "x");
        File.WriteAllText(Path.Combine(folder, "audiogroup1.dat"), "x");
        Directory.CreateDirectory(Path.Combine(folder, "lang"));
        File.WriteAllText(Path.Combine(folder, "lang", "lang_en.json"), "{}");
        File.WriteAllText(Path.Combine(folder, "mus.ogg"), "audio");
        var home = new UserData(dir.File("user"));
        return (new IncludedFiles(Path.Combine(folder, "data.win"), home), folder, home);
    }

    [Fact]
    public void ScanListsLooseFilesOnly()
    {
        using var dir = new TempDir();
        var (files, _, _) = Setup(dir);
        Assert.Equal(["lang/lang_en.json", "mus.ogg"], files.Scan().Files.Select(f => f.Path));
        Assert.True(files.Scan(1).Truncated);
    }

    [Fact]
    public void OnlyTheDataFileItsBackupsAndTempFilesAreHidden()
    {
        using var dir = new TempDir();
        var (files, folder, _) = Setup(dir);
        File.WriteAllText(Path.Combine(folder, "data.winter.txt"), "x");
        File.WriteAllText(Path.Combine(folder, "mus.ogg.g3mtmp"), "x");
        Assert.Equal(["data.winter.txt", "lang/lang_en.json", "mus.ogg"], files.Scan().Files.Select(f => f.Path));
        Assert.Equal(Path.Combine(folder, "data.winter.txt"), files.Resolve("data.winter.txt"));
        Assert.Throws<InvalidOperationException>(() => files.Resolve("data.win.bak"));
        Assert.Throws<InvalidOperationException>(() => files.Resolve("mus.ogg.g3mtmp"));
    }

    [Fact]
    public void ADataFileInTheRootFolderStillResolvesItsFiles()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var files = new IncludedFiles(Path.Combine(root, "data.win"));
        Assert.Equal(Path.Combine(root, "x.txt"), files.Resolve("x.txt"));
        Assert.Throws<InvalidOperationException>(() => files.Resolve("data.win"));
    }

    [Fact]
    public void ACaseOnlyRenameIsAllowedAndAFailedReplaceLeavesNoTempFile()
    {
        using var dir = new TempDir();
        var (files, folder, _) = Setup(dir);
        File.WriteAllText(Path.Combine(folder, "Notes.txt"), "n");
        Assert.Equal("notes.txt", files.Rename("Notes.txt", "notes.txt", null, true));
        Assert.Contains("notes.txt", files.Scan().Files.Select(f => f.Path));
        Assert.DoesNotContain("Notes.txt", files.Scan().Files.Select(f => f.Path));
        Assert.Throws<InvalidOperationException>(() => files.Rename("notes.txt", "mus.ogg", null, true)); // a real clash still fails

        Assert.ThrowsAny<IOException>(() => files.Replace("notes.txt", dir.File("missing.bin")));
        Assert.Empty(Directory.GetFiles(folder, "*.g3mtmp"));
    }

    [Fact]
    public void AddReportsAFileThatCannotBeCopiedAndGoesOn()
    {
        using var dir = new TempDir();
        var (files, folder, _) = Setup(dir);
        var good = dir.File("good.txt");
        File.WriteAllText(good, "g");
        var (added, _, failed) = files.Add([dir.File("missing.txt"), good]);
        Assert.Equal(["good.txt"], added);
        Assert.Equal(["missing.txt"], failed);
        Assert.True(File.Exists(Path.Combine(folder, "good.txt")));
    }

    [Fact]
    public void AddSkipsExistingReplaceExportAndPathCheck()
    {
        using var dir = new TempDir();
        var (files, folder, _) = Setup(dir);
        var source = dir.File("new.txt");
        File.WriteAllText(source, "new");
        Assert.Equal(["new.txt"], files.Add([source]).Added);
        Assert.Equal(["new.txt"], files.Add([source]).Skipped);
        File.WriteAllText(source, "newer");
        files.Replace("new.txt", source);
        Assert.Equal("newer", File.ReadAllText(Path.Combine(folder, "new.txt")));
        files.Export("new.txt", dir.File("out.txt"));
        Assert.Equal("newer", File.ReadAllText(dir.File("out.txt")));
        Assert.Throws<InvalidOperationException>(() => files.Resolve("../outside.txt"));
        Assert.Throws<InvalidOperationException>(() => files.Resolve("data.win"));
    }

    [Fact]
    public void RenameAndDeleteWarnWhenASoundStreamsTheFile()
    {
        using var dir = new TempDir();
        var (files, folder, home) = Setup(dir);
        using var data = TestData.Create();
#pragma warning disable CA2000 // the data model owns what is added to it
        data.Sounds.Add(new GameMakerSound { Name = data.Strings.MakeString("snd_bgm"), File = data.Strings.MakeString("mus.ogg") });
#pragma warning restore CA2000
        Assert.Equal(["snd_bgm", "strings"], IncludedFiles.UsedBy(data, "mus.ogg")); // the sound name string and the file name string

        Assert.Throws<IncludedFileUseException>(() => files.Rename("mus.ogg", "bgm.ogg", data, false));
        Assert.Throws<IncludedFileUseException>(() => files.Delete("mus.ogg", data, false));
        Assert.Equal("bgm.ogg", files.Rename("mus.ogg", "bgm.ogg", data, true));
        Assert.True(File.Exists(Path.Combine(folder, "bgm.ogg")));
        Assert.Throws<InvalidOperationException>(() => files.Rename("bgm.ogg", "lang/x.ogg", data, true));

        files.Delete("lang/lang_en.json", data, false); // no sound or string names it
        Assert.False(File.Exists(Path.Combine(folder, "lang", "lang_en.json")));
        Assert.Single(Directory.GetFiles(home.Combine("trash"), "lang_en.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void TheExplorerListsTheEntryInTheGameGroup()
    {
        using var data = TestData.Create();
        var item = ResourceTypes.All.First(t => t.Key == "general").Items(data)!.OfType<IncludedFilesEntry>().Single();
        Assert.Equal(ResourceTypes.IncludedFilesName, ResourceTypes.NameOf(item));
    }
}
