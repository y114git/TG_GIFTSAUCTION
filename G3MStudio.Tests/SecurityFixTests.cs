using System.Text.Json;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class SecurityFixTests
{
    private static void WaitFor(Func<bool> done)
    {
        for (var i = 0; i < 400 && !done(); i++) Thread.Sleep(25);
        Assert.True(done());
    }

    private static LayerSet Stack(byte shade) => new([new PaintLayer("a", new PixelBuffer(2, 2)), new PaintLayer("b", new PixelBuffer(2, 2), Opacity: shade)]);

    private static (ModBaseCache Cache, string Folder, string File) Cached(TempDir dir)
    {
        var user = new UserData(dir.File("user"));
        var cache = new ModBaseCache(user);
        var file = dir.File("a.win");
        File.WriteAllBytes(file, [.. Enumerable.Range(0, 3000).Select(i => (byte)(i * 7))]);
        cache.Remember(file, file, 1 << 20);
        return (cache, user.Combine("modbase"), file);
    }

    [Fact]
    public void AnUnreadableIndexStopsRememberAndTrimWithoutDeletingCopies()
    {
        using var dir = new TempDir();
        var (cache, folder, _) = Cached(dir);
        var blob = Directory.GetFiles(folder, "*.gz").Single();
        File.WriteAllText(Path.Combine(folder, "index.json"), "{ not json");
        var other = dir.File("b.win");
        File.WriteAllBytes(other, [1, 2, 3, 4]);
        Assert.Throws<IOException>(() => cache.Remember(other, other, 1 << 20));
        cache.Trim(0);
        Assert.True(File.Exists(blob));
        Assert.Null(cache.Find(other));
    }

    [Fact]
    public void AnUnreadableCopyStaysButADamagedOneIsDropped()
    {
        using var dir = new TempDir();
        var (cache, folder, file) = Cached(dir);
        var entry = cache.Find(file)!;
        var blob = Directory.GetFiles(folder, "*.gz").Single();
        var target = dir.File("out");
        Directory.CreateDirectory(target);
        using (new FileStream(blob, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Null(cache.TryExtract(entry, target)); // a transient lock is not damage
        Assert.True(File.Exists(blob));
        Assert.NotNull(cache.Find(file));
        Assert.NotNull(cache.TryExtract(entry, target));
        File.WriteAllBytes(blob, [9, 9, 9, 9]);
        Assert.Null(cache.TryExtract(entry, target));
        Assert.False(File.Exists(blob));
        Assert.Null(cache.Find(file));
    }

    [Fact]
    public void ADifferentFileOnDiskIsDetectedAndLeftoverPartsAreSwept()
    {
        using var dir = new TempDir();
        var (cache, folder, file) = Cached(dir);
        var entry = cache.Find(file)!;
        Assert.False(ModBaseCache.Differs(entry, file));
        var bytes = File.ReadAllBytes(file);
        bytes[10] ^= 0xFF; // same size, other content
        File.WriteAllBytes(file, bytes);
        Assert.True(ModBaseCache.Differs(entry, file));
        File.WriteAllBytes(file, [1]);
        Assert.True(ModBaseCache.Differs(entry, file));

        var part = Path.Combine(folder, "stale.part");
        File.WriteAllText(part, "x");
        cache.Remember(dir.File("n.win"), file, 1 << 20);
        Assert.True(File.Exists(part)); // fresh: another Studio may still be writing it
        File.SetLastWriteTimeUtc(part, DateTime.UtcNow.AddHours(-1));
        cache.Remember(dir.File("m.win"), file, 1 << 20);
        Assert.False(File.Exists(part));
        File.WriteAllText(part, "x");
        Assert.True(cache.Clear());
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void TheUnsavedPromptHasItsOwnIdAndDoesNotTouchTheRiskPolicy()
    {
        Assert.NotEqual(Prompts.RiskyAction, Prompts.UnsavedChanges);
        Assert.Contains(Prompts.All, p => p.Id == Prompts.UnsavedChanges);
        var s = Prompts.Set(new AppSettings(), Prompts.UnsavedChanges, PromptChoice.Yes);
        Assert.False(s.AllowUnsafeSilently);
        Assert.Equal(PromptChoice.Ask, Prompts.Get(s, Prompts.RiskyAction));
        Assert.Equal(PromptChoice.Yes, Prompts.Get(s, Prompts.UnsavedChanges));
    }

    [Fact]
    public void ARelativeRunnerIsTakenFromTheDataFolderAndAScannedOneNeedsTheGameName()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.File("bin"));
        var runner = dir.File(Path.Combine("bin", "tool"));
        File.WriteAllText(runner, "");
        var data = dir.File("data.win");
        Assert.Equal(runner, RunLocator.Resolve("bin/tool", data));
        Assert.Null(RunLocator.Resolve("bin/missing", data));

        var generic = dir.File("runner");
        File.WriteAllText(generic, "");
        Assert.Equal(RunnerCheck.Unknown, RunLocator.CheckFound(generic, data, "Game.exe"));
        var game = dir.File("Game");
        File.WriteAllText(game, "");
        Assert.Equal(RunnerCheck.Known, RunLocator.CheckFound(game, data, "Game.exe"));
        Assert.Equal(RunnerCheck.Missing, RunLocator.CheckFound(dir.File("none"), data, null));
    }

    [Fact]
    public void ARelativeRunnerFromGlobalSettingsIsNotTakenFromTheDataFolderButOneFromTheRunFileIs()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.File("bin"));
        var game = dir.File(Path.Combine("bin", "Game"));
        File.WriteAllText(game, "");
        var data = dir.File("data.win");
        var user = new UserData(dir.File("user"));

        // A global setting (or an override) is not resolved against the data folder, where a downloaded mod could plant the program.
        var global = new AppSettings { RunRunner = "bin/Game" };
        Assert.Equal(RunnerCheck.Missing, RunPlan.Resolve(global, data, "Game", "run.tmp", null, user).Check);
        Assert.Equal(RunnerCheck.Missing, RunPlan.Resolve(new AppSettings(), data, "Game", "run.tmp", "bin/Game", user).Check);

        // The run file in Studio's folder is trusted, so its relative path is taken from the data folder.
        RunPlan.SaveProject(new AppSettings(), user, data, new RunProfile("", "bin/Game", "", false));
        var (spec, check, _) = RunPlan.Resolve(new AppSettings(), data, "Game", "run.tmp", null, user);
        Assert.Equal(game, spec?.Runner);
        Assert.Equal(RunnerCheck.Known, check);

        // A cwd-relative path that lands inside the data folder does not count as known on its name alone.
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), game);
        if (!Path.IsPathRooted(relative)) Assert.Equal(RunnerCheck.Unknown, RunLocator.Check(relative, data, "Game"));
    }

    [Fact]
    public void TheSidecarWinsOverTheViewStateLayers()
    {
        var old = new LayerStore();
        old.Set("spr", 0, Stack(10));
        var fresh = new LayerStore();
        fresh.Set("spr", 0, Stack(90));
        var target = new LayerStore();
        target.ImportSaved(old.Export(), fresh.Export());
        Assert.Equal(fresh.Export()["spr#0"], target.Export()["spr#0"]);
    }

    [Fact]
    public async Task EditsAreNotBlockedByAWriteAndAnEditDuringItCausesAnotherWrite()
    {
        var store = new LayerStore();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var autosave = new LayerAutosave(store, _ => { started.Set(); release.Wait(TestContext.Current.CancellationToken); }, TimeSpan.FromMilliseconds(30));
        store.Set("spr", 0, Stack(10));
        Assert.True(started.Wait(5000, TestContext.Current.CancellationToken));
        await Task.Run(() => store.Set("spr", 0, Stack(20)), TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken); // the first write is still blocked
        release.Set();
        WaitFor(() => autosave.Writes == 2);
    }

    [Fact]
    public void AFailingExportIsCountedAndReportedOnceInsteadOfCrashing()
    {
        var store = new LayerStore();
        var raised = 0;
        using var autosave = new LayerAutosave(store, _ => throw new ArgumentException("bad picture"), TimeSpan.FromMilliseconds(30));
        autosave.Failed += _ => Interlocked.Increment(ref raised);
        store.Set("spr", 0, Stack(10));
        WaitFor(() => autosave.Failures == 1);
        store.Set("spr", 0, Stack(11));
        WaitFor(() => autosave.Failures == 2);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ADamagedLayerSidecarIsKeptAsBakAndReported()
    {
        using var dir = new TempDir();
        var views = new ViewStateStore(new UserData(dir.File("user")));
        var kept = new List<string>();
        views.LayersDamaged += kept.Add;
        views.SaveLayers("game.win", new Dictionary<string, string>());
        var sidecar = Directory.GetFiles(dir.File("user"), "*.layers.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(sidecar, "{ broken");
        Assert.Null(views.LoadLayers("game.win"));
        Assert.False(File.Exists(sidecar));
        Assert.Equal("{ broken", File.ReadAllText(sidecar + ".bak"));
        Assert.Equal([sidecar + ".bak"], kept);
        File.WriteAllText(sidecar, "{ again");
        Assert.Null(views.LoadLayers("game.win"));
        Assert.Equal("{ again", File.ReadAllText(sidecar + ".bak")); // an older .bak is replaced
    }

    [Fact]
    public async Task AnAutoTileImportRejectsBigFilesManySlotsAndBadValues()
    {
        var slots = Enumerable.Range(1, AutoTileStore.MaxSlots + 1).ToDictionary(i => i, i => i);
        var many = new AutoTileRuleSet("n", "t", AutoTileLayout.Blob47, 1, slots);
        Assert.Null(AutoTileStore.Import(JsonSerializer.Serialize(many)));
        Assert.Null(AutoTileStore.Import("{\"name\":\"n\",\"tileSet\":\"t\",\"layout\":{\"x\":1},\"slots\":5}"));
        Assert.Null(AutoTileStore.Import("[[[[[["));
        using var dir = new TempDir();
        var big = dir.File("big.json");
        await File.WriteAllBytesAsync(big, new byte[AutoTileStore.MaxImportBytes + 1], TestContext.Current.CancellationToken);
        Assert.Null(await AutoTileStore.ImportFileAsync(big));
    }
}
