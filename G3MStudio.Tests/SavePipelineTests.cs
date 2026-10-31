using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class SavePipelineTests
{
    private static SavePipeline Pipeline(TempDir dir, int backups = 3, Action<SaveStep, string>? hook = null) =>
        new(new UserData(dir.File("user")), backups) { Hook = hook, LockRetries = 1, LockRetryDelay = TimeSpan.FromMilliseconds(10) };

    private static void AssertValidFile(string path, int objects)
    {
        using var stream = File.OpenRead(path);
        using var back = GameMakerIO.Read(stream);
        Assert.Equal(objects, back.GameObjects.Count);
    }

    [Fact]
    public async Task SaveReplacesTheTargetAndLeavesNoTempFile()
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 3);
        using var data = TestData.Create(5);

        var result = await Pipeline(dir).SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Saved);
        AssertValidFile(target, 5);
        Assert.False(File.Exists(target + SavePipeline.NextSuffix));
    }

    [Fact]
    public async Task SaveRotatesAtMostTheConfiguredNumberOfBackupsNewestFirst()
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 1);
        var pipeline = Pipeline(dir, backups: 2);
        for (var objects = 2; objects <= 5; objects++)
        {
            using var data = TestData.Create(objects);
            Assert.True((await pipeline.SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        }

        AssertValidFile(target, 5);
        AssertValidFile(pipeline.BackupSlot(target, 1), 4);
        AssertValidFile(pipeline.BackupSlot(target, 2), 3);
        Assert.False(File.Exists(pipeline.BackupSlot(target, 3)));
    }

    [Fact]
    public async Task SaveToANewPathWritesWithoutBackup()
    {
        using var dir = new TempDir();
        using var data = TestData.Create(2);
        var target = dir.File("new.win");
        var pipeline = Pipeline(dir);

        Assert.True((await pipeline.SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken)).Saved);
        AssertValidFile(target, 2);
        Assert.False(File.Exists(pipeline.BackupSlot(target, 1)));
    }

    [Fact]
    public async Task ABrokenWriteIsRefusedAndTheTargetIsUntouched()
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 3);
        var before = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        using var data = TestData.Create(5);
        var pipeline = Pipeline(dir, hook: (step, next) =>
        {
            if (step == SaveStep.Written) File.WriteAllBytes(next, File.ReadAllBytes(next)[..200]);
        });

        var result = await pipeline.SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Saved);
        Assert.Equal(SaveFailure.ValidationFailed, result.Failure);
        Assert.Equal(before, (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)));
        Assert.False(File.Exists(target + SavePipeline.NextSuffix));
    }

    [Fact]
    public async Task CancelWhileWritingKeepsTheTargetAndRemovesTheTempFile()
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 3);
        var before = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        using var data = TestData.Create(5);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Pipeline(dir, hook: (step, _) => { if (step == SaveStep.Writing) cts.Cancel(); }).SaveAsync(data, target, null, cts.Token));

        Assert.Equal(before, (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)));
        Assert.False(File.Exists(target + SavePipeline.NextSuffix));
    }

    [Theory]
    [InlineData(SaveStep.Writing)]
    [InlineData(SaveStep.Written)]
    [InlineData(SaveStep.Validated)]
    [InlineData(SaveStep.BackupRotated)]
    [InlineData(SaveStep.BackedUp)]
    [InlineData(SaveStep.Replaced)]
    public async Task AKillAtAnyStepLeavesTheOldOrTheNewValidFile(SaveStep killAt)
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 3);
        var before = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        using var data = TestData.Create(5);
        var killing = Pipeline(dir, hook: (step, _) => { if (step == killAt) throw new SaveInterruptedException(); });

        await Assert.ThrowsAsync<SaveInterruptedException>(() => killing.SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken));

        if (killAt != SaveStep.Replaced) Assert.Equal(before, (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)));
        AssertValidFile(target, killAt == SaveStep.Replaced ? 5 : 3);

        // The next save starts from whatever the kill left behind and completes.
        var again = await Pipeline(dir).SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(again.Saved, again.Message);
        AssertValidFile(target, 5);
        Assert.False(File.Exists(target + SavePipeline.NextSuffix));
    }

    [Fact]
    public async Task ALockedTargetIsRetriedThenReportedWithItsHolderAndTheEditsStay()
    {
        if (!OperatingSystem.IsWindows()) return; // Unix rename ignores open handles, so there is no lock to hit.
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 3);
        var before = await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken);
        using var data = TestData.Create(5);

        SaveResult result;
        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            result = await Pipeline(dir).SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Saved);
        Assert.Equal(SaveFailure.Locked, result.Failure);
        Assert.False(string.IsNullOrEmpty(result.Holder));
        Assert.Equal(before, (await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)));
        Assert.Equal(5, data.GameObjects.Count);

        Assert.True((await Pipeline(dir).SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken)).Saved);
    }
}
