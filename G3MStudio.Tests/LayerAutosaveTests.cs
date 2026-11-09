using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class LayerAutosaveTests
{
    private static LayerSet Stack(byte shade) =>
        new([new PaintLayer("a", new PixelBuffer(2, 2)), new PaintLayer("b", new PixelBuffer(2, 2), Opacity: shade)]);

    private static void WaitFor(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++) Thread.Sleep(25);
        Assert.True(done());
    }

    [Fact]
    public void ABurstOfEditsMakesOneWriteAfterTheDelayOnAnotherThread()
    {
        var store = new LayerStore();
        var threads = new List<int>();
        var written = new List<Dictionary<string, string>>();
        using var autosave = new LayerAutosave(store, layers => { lock (threads) { threads.Add(Environment.CurrentManagedThreadId); written.Add(layers); } }, TimeSpan.FromMilliseconds(1500));
        for (var i = 1; i <= 5; i++) store.Set("spr", 0, Stack((byte)(i * 10)));
        Assert.Equal(0, autosave.Writes); // the edits returned before anything was written
        WaitFor(() => autosave.Writes == 1);
        Thread.Sleep(1700); // a second write would fall inside this wait
        Assert.Equal(1, autosave.Writes);
        Assert.NotEqual(Environment.CurrentManagedThreadId, threads.Single());
        Assert.Contains("spr#0", written.Single().Keys);
    }

    [Fact]
    public void DisposeWaitsForAWriteThatIsRunning()
    {
        var store = new LayerStore();
        using var started = new ManualResetEventSlim();
        var finished = false;
        var autosave = new LayerAutosave(store, _ => { started.Set(); Thread.Sleep(300); finished = true; }, TimeSpan.FromMilliseconds(20));
        store.Set("spr", 0, Stack(50));
        Assert.True(started.Wait(5000, TestContext.Current.CancellationToken));
        autosave.Dispose();
        Assert.True(finished);
    }

    [Fact]
    public void AtomicWriteDoesNotUseTheFixedTempName()
    {
        using var dir = new TempDir();
        var target = dir.File("a.json");
        File.WriteAllText(target + ".tmp", "other"); // another writer uses the old fixed name
        UserData.WriteAllTextAtomic(target, "{}");
        Assert.Equal("{}", File.ReadAllText(target));
        Assert.Equal("other", File.ReadAllText(target + ".tmp"));
    }

    [Fact]
    public void OnlyOldTempFilesOfAnAtomicWriteAreSwept()
    {
        using var dir = new TempDir();
        var user = new UserData(dir.File("user"));
        var stale = user.Combine("a.json." + Guid.NewGuid().ToString("N") + ".tmp");
        var fresh = user.Combine("b.json." + Guid.NewGuid().ToString("N") + ".tmp");
        var foreign = user.Combine("keep.tmp");
        foreach (var f in new[] { stale, fresh, foreign }) File.WriteAllText(f, "x");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(foreign, DateTime.UtcNow.AddDays(-3));
        user.SweepStaleTemp(TimeSpan.FromDays(1));
        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(foreign));
    }

    [Fact]
    public void TurnedOffItWritesNothingAndTurnedOnItWritesTheCurrentStacks()
    {
        var store = new LayerStore();
        using var autosave = new LayerAutosave(store, _ => { }, TimeSpan.FromMilliseconds(50)) { Enabled = false };
        store.Set("spr", 0, Stack(50));
        Thread.Sleep(300);
        Assert.Equal(0, autosave.Writes);
        autosave.Enabled = true;
        WaitFor(() => autosave.Writes == 1);
    }

    [Fact]
    public void TheSidecarIsWrittenAtomicallyAndReadBack()
    {
        using var dir = new TempDir();
        var views = new ViewStateStore(new UserData(dir.File("user")));
        var store = new LayerStore();
        store.Set("spr", 1, Stack(40));
        views.SaveLayers("game.win", store.Export());
        views.SaveLayers("game.win", store.Export());
        Assert.Empty(Directory.GetFiles(dir.File("user"), "*.tmp", SearchOption.AllDirectories)); // the temp file was moved over the target
        var loaded = new LayerStore();
        loaded.Import(views.LoadLayers("game.win"));
        Assert.True(loaded.Has("spr", 1));
        Assert.Null(views.LoadLayers("other.win"));
    }

    [Fact]
    public void AFailingWriteIsSkippedAndTheNextEditTriesAgain()
    {
        var store = new LayerStore();
        var fail = true;
        using var autosave = new LayerAutosave(store, _ => { if (fail) throw new IOException("disk full"); }, TimeSpan.FromMilliseconds(50));
        store.Set("spr", 0, Stack(30));
        Thread.Sleep(300);
        Assert.Equal(0, autosave.Writes);
        fail = false;
        store.Set("spr", 0, Stack(31));
        WaitFor(() => autosave.Writes == 1);
    }
}
