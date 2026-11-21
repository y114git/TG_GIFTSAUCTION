using G3MLib.DataFile;
using G3MLib.DataFile.Project;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;
using ImageMagick;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

/// <summary>Resolve Outside Changes on the project folder: resource folders pasted, added, deleted or edited outside reach the data file; nothing changed says so.</summary>
public sealed class ProjectFolderReplaceTests
{
    private static byte[] Png(int shade)
    {
        using var image = new MagickImage(new MagickColor((byte)(shade * 40), 0, 0, 255), 32, 32);
        image.Format = MagickFormat.Png;
        return image.ToByteArray();
    }

    private static GameMakerData SpriteData(int frames, int objects = 0, string name = "spr_a")
    {
        var data = TestData.Create(objects);
        TestData.AddPage(data);
        SpriteTemplate.Create(data, name);
        if (frames > 0) new FramesImportCommand(name, [.. Enumerable.Range(1, frames).Select(Png)], true).Apply(data);
        return data;
    }

    private static async Task<(ProjectMergeTests.Session Session, Workspace Workspace, ProjectLink Link, string Path)> Start(TempDir dir, int objects = 0)
    {
        string path;
        using (var data = SpriteData(0, objects)) path = TestData.Write(dir.File("data.win"), data);
        var session = new ProjectMergeTests.Session(dir);
        var workspace = await session.Open(path);
        var link = await session.Service.Projects.EnableAsync(workspace, dir.File("projects"), cancellationToken: TestContext.Current.CancellationToken);
        return (session, workspace, link, path);
    }

    /// <summary>The folder of a sprite as another project wrote it with <paramref name="frames"/> frames.</summary>
    private static string OtherFolder(TempDir dir, ProjectLink link, int frames, string name = "spr_a")
    {
        var other = dir.File("other-" + name + frames);
        using (var data = SpriteData(frames, 0, name)) G3MProjectService.Export(data, other, null, new G3MProjectOptions { Name = link.Record.Name }, null, TestContext.Current.CancellationToken);
        return Directory.GetDirectories(other, name, SearchOption.AllDirectories).Single();
    }

    private static string OurFolder(ProjectLink link, string name = "spr_a") => Path.Combine(link.Folder, "sprites", name);

    private static void CopyInto(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void Replace(string from, string to)
    {
        if (Directory.Exists(to)) Directory.Delete(to, true);
        CopyInto(from, to);
    }

    private static int Frames(Workspace workspace, string name = "spr_a") => workspace.Data.Sprites.Single(s => s.Name.Content == name).Textures.Count;

    [Fact]
    public async Task AFiveFrameSpriteFolderCopiedOverTheProjectIsTakenAfterOwnFrameImports()
    {
        using var dir = new TempDir();
        var (session, workspace, link, path) = await Start(dir);
        workspace.Edit(new FramesImportCommand("spr_a", [Png(1), Png(2)], true)); // the tester imported two frames in the app first
        Assert.True((await link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        Replace(OtherFolder(dir, link, 5), OurFolder(link));

        Assert.False(link.DataFileChanged); // the data file did not change, so the command used to stop with "nothing outside" here
        Assert.True((await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken))!.Saved);

        Assert.Equal(5, Frames(await session.Open(path)));
    }

    [Fact]
    public async Task APastedFolderWinsOverPendingEditsOfTheSameResourceAndOtherPendingEditsAreKept()
    {
        using var dir = new TempDir();
        var (session, workspace, link, path) = await Start(dir, objects: 1);
        workspace.Edit(new FramesImportCommand("spr_a", [Png(1), Png(2)], true));
        workspace.Edit(new RenameResourceCommand("objects", "obj_0", "obj_b")); // pending too, a different resource
        Replace(OtherFolder(dir, link, 5), OurFolder(link));

        Assert.True(link.HasPendingWrite);
        Assert.True((await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken))!.Saved);

        var again = await session.Open(path);
        Assert.Equal(5, Frames(again));
        Assert.NotNull(again.Data.GameObjects.ByName("obj_b"));
    }

    [Fact]
    public async Task ResolveWithNothingChangedSaysSoTwiceAndAfterAResolve()
    {
        using var dir = new TempDir();
        var (session, _, link, _) = await Start(dir);
        Assert.Null(await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));
        Replace(OtherFolder(dir, link, 3), OurFolder(link));
        Assert.NotNull(await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));
        Assert.Null(await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ANewFolderWhoseNameStartsAnExistingOneIsStillSeenAsNew()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        CopyInto(OtherFolder(dir, link, 2, "spr"), OurFolder(link, "spr")); // "spr" is a prefix of the known "spr_a"

        Assert.True((await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken))!.Saved);

        Assert.Equal(2, Frames(await session.Open(path), "spr"));
    }

    [Fact]
    public async Task ANewResourceFolderCopiedInIsAddedToTheDataFile()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        CopyInto(OtherFolder(dir, link, 3, "spr_b"), OurFolder(link, "spr_b"));

        Assert.True((await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken))!.Saved);

        var again = await session.Open(path);
        Assert.Equal(3, Frames(again, "spr_b"));
        Assert.Equal(1, Frames(again));
    }

    [Fact]
    public async Task ADeletedResourceFolderLeavesTheResourceInTheDataFile()
    {
        using var dir = new TempDir();
        var (session, _, link, path) = await Start(dir);
        Directory.Delete(OurFolder(link), true);

        await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken);

        Assert.Equal(1, Frames(await session.Open(path))); // the library keeps it: it cannot tell a deletion from a folder that was never exported
    }

    [Fact]
    public async Task OneFrameFileEditedInPlaceIsTaken()
    {
        using var dir = new TempDir();
        var (session, workspace, link, path) = await Start(dir);
        workspace.Edit(new FramesImportCommand("spr_a", [Png(1), Png(2)], true));
        Assert.True((await link.SyncAsync(session.Pipeline, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        var frame = Directory.GetFiles(OurFolder(link), "*.png", SearchOption.AllDirectories).Order(StringComparer.Ordinal).First();
        await File.WriteAllBytesAsync(frame, Png(6), TestContext.Current.CancellationToken);

        Assert.True((await link.BringInFolderAsync(session.Pipeline, TestContext.Current.CancellationToken))!.Saved);

        Assert.Equal(2, Frames(await session.Open(path)));
    }
}
