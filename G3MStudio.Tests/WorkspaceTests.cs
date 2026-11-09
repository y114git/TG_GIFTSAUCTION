using System.Security.Cryptography;
using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class WorkspaceTests
{
    private sealed class Recorder : IProgress<GameMakerProgress>
    {
        public readonly List<GameMakerProgress> Items = [];
        public Action<GameMakerProgress>? OnReport;

        public void Report(GameMakerProgress value)
        {
            Items.Add(value);
            OnReport?.Invoke(value);
        }
    }

    private static WorkspaceService NewService(TempDir dir)
    {
        var userData = new UserData(dir.File("user"));
        return new WorkspaceService(userData, RecentFiles.Load(userData));
    }

    [Fact]
    public async Task OpenReadsTheFileWithProgressAndIdentity()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var service = NewService(dir);
        var progress = new Recorder();

        var workspace = await service.OpenAsync(path, progress, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(workspace, service.Current);
        Assert.Equal(3, workspace.Data.GameObjects.Count);
        Assert.Equal(new FileInfo(path).Length, workspace.Identity.Size);
#pragma warning disable CA5351 // the identity uses MD5 as a file fingerprint, not for security
        Assert.Equal(Convert.ToHexStringLower(MD5.HashData((await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)))), workspace.Identity.Md5);
#pragma warning restore CA5351
        Assert.Equal(Path.GetFullPath(path), workspace.Identity.Path);
        Assert.NotEmpty(progress.Items);
        Assert.Equal(1.0, progress.Items[^1].Fraction);
        Assert.True(progress.Items.Zip(progress.Items.Skip(1)).All(p => p.First.Fraction <= p.Second.Fraction));
        service.Close();
    }

    [Fact]
    public async Task OpenRecordsTheFileInTheRecentListAndReopenKeepsOneEntry()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var service = NewService(dir);

        await service.OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);
        await service.OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        var entry = Assert.Single(service.Recent.Entries);
        Assert.Equal(Path.GetFullPath(path), entry.Path);
        Assert.Equal("New GameMaker Game", entry.Name);
        service.Close();
    }

    [Fact]
    public async Task CancelBeforeAndDuringTheReadLeavesTheCurrentWorkspaceAlone()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir);
        var other = TestData.WriteSample(dir, "other.win");
        var service = NewService(dir);
        var first = await service.OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken);

        using var before = new CancellationTokenSource();
        await before.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.OpenAsync(other, null, before.Token));

        using var during = new CancellationTokenSource();
        var progress = new Recorder { OnReport = _ => during.Cancel() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.OpenAsync(other, progress, during.Token));

        Assert.Same(first, service.Current);
        Assert.Single(service.Recent.Entries);
        service.Close();
    }

    [Fact]
    public async Task OpeningAMissingFileThrows()
    {
        using var dir = new TempDir();
        await Assert.ThrowsAsync<FileNotFoundException>(() => NewService(dir).OpenAsync(dir.File("nope.win"), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RecentListPinsOrdersPersistsAndSurvivesADamagedFile()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        var recent = RecentFiles.Load(userData);
        var t = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        recent.Add(dir.File("a.win"), "A", "1", t);
        recent.Add(dir.File("b.win"), "B", "1", t.AddDays(1));
        recent.Add(dir.File("c.win"), "C", "1", t.AddDays(2));
        recent.SetPinned(dir.File("a.win"), true);

        Assert.Equal(["A", "C", "B"], recent.Entries.Select(e => e.Name));
        Assert.Equal(["A", "C", "B"], RecentFiles.Load(userData).Entries.Select(e => e.Name));

        recent.Remove(dir.File("c.win"));
        recent.Clear();
        Assert.Equal(["A"], recent.Entries.Select(e => e.Name));

        File.WriteAllText(userData.Combine("recent.json"), "{ not json");
        Assert.Empty(RecentFiles.Load(userData).Entries);
    }

    [Fact]
    public void RecentListIsCappedAndKeepsPinnedEntries()
    {
        using var dir = new TempDir();
        var recent = RecentFiles.Load(new UserData(dir.File("user")));
        var t = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < RecentFiles.MaxUnpinned + 5; i++) recent.Add(dir.File($"{i}.win"), $"{i}", "1", t.AddMinutes(i));
        recent.SetPinned(dir.File("10.win"), true);
        recent.Add(dir.File("new.win"), "new", "1", t.AddDays(1));

        Assert.Equal(RecentFiles.MaxUnpinned + 1, recent.Entries.Count);
        Assert.Contains(recent.Entries, e => e.Name == "10" && e.Pinned);
    }
}

public sealed class FileHoldersTests
{
    [Fact]
    public void AFileHeldOpenNamesItsHolderAndAFreeFileNamesNone()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the Restart Manager is part of Windows");
        using var dir = new TempDir();
        var path = dir.File("held.bin");
        File.WriteAllText(path, "x");
        Assert.Null(FileHolders.Find(path));
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.False(string.IsNullOrWhiteSpace(FileHolders.Find(path))); // the name Restart Manager gives depends on the test host
    }
}
