#pragma warning disable CA2201 // the tests throw the exception the runtime throws
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class MemoryGovernorTests
{
    private const long Gb = 1L << 30;

    private sealed class FakeSource : IMemorySource
    {
        public MemorySample Sample { get; set; }

        public MemorySample Read() => Sample;

        /// <summary>Sets the heap to this share of a 1 GB limit, with plenty of free system memory.</summary>
        public void Heap(double share) => Sample = new MemorySample((long)(share * Gb), Gb / 4, 64 * Gb);
    }

    private static (MemoryGovernor Governor, FakeSource Source, List<MemoryLevel> Changes, int[] Trims) Make()
    {
        var source = new FakeSource();
        source.Heap(0.1);
        var trims = new int[1];
        var governor = new MemoryGovernor(source) { LimitBytes = Gb, Trim = () => trims[0]++ };
        var changes = new List<MemoryLevel>();
        governor.LevelChanged += (_, now) => changes.Add(now);
        return (governor, source, changes, trims);
    }

    [Fact]
    public void LevelsFollowTheBordersOfTheLimit()
    {
        var (governor, source, changes, _) = Make();
        governor.Poll();
        Assert.Equal(MemoryLevel.Normal, governor.Level);
        source.Heap(0.81);
        governor.Poll();
        Assert.Equal(MemoryLevel.High, governor.Level);
        source.Heap(0.93);
        governor.Poll();
        Assert.Equal(MemoryLevel.Critical, governor.Level);
        Assert.True(governor.IsLow);
        Assert.Equal([MemoryLevel.High, MemoryLevel.Critical], changes);
    }

    [Fact]
    public void WithoutALimitTheFreeMemoryOfTheSystemIsTheBudget()
    {
        var (governor, source, _, _) = Make();
        governor.LimitBytes = 0;
        source.Sample = new MemorySample(Gb, 9 * Gb, 1 * Gb); // the process holds 90% of what it and the system have
        governor.Poll();
        Assert.Equal(MemoryLevel.High, governor.Level);
    }

    [Fact]
    public void AReadingNearABorderDoesNotFlap()
    {
        var (governor, source, changes, _) = Make();
        source.Heap(0.85);
        governor.Poll();
        foreach (var share in new[] { 0.79, 0.81, 0.78, 0.80, 0.75 })
        {
            source.Heap(share);
            governor.Poll();
        }

        Assert.Equal(MemoryLevel.High, governor.Level); // stays above the lower border of 0.72
        source.Heap(0.70);
        governor.Poll();
        Assert.Equal(MemoryLevel.Normal, governor.Level);
        Assert.Equal([MemoryLevel.High, MemoryLevel.Normal], changes);

        source.Heap(0.95);
        governor.Poll();
        source.Heap(0.89); // below the critical border, above the lower one: still critical
        governor.Poll();
        Assert.Equal(MemoryLevel.Critical, governor.Level);
        source.Heap(0.80); // between the borders of High and Critical
        governor.Poll();
        Assert.Equal(MemoryLevel.High, governor.Level);
    }

    [Fact]
    public void ReleasesRunOnceForTheLevelsCrossedAndThenTrim()
    {
        var (governor, source, _, trims) = Make();
        var high = 0;
        var critical = 0;
        governor.OnPressure(MemoryLevel.High, () => high++);
        governor.OnPressure(MemoryLevel.Critical, () => critical++);
        source.Heap(0.85);
        governor.Poll();
        governor.Poll();
        Assert.Equal((1, 0), (high, critical));
        Assert.Equal(1, trims[0]);
        source.Heap(0.95);
        governor.Poll();
        Assert.Equal((1, 1), (high, critical)); // High already ran when it was entered
        source.Heap(0.1);
        governor.Poll();
        Assert.Equal((1, 1), (high, critical)); // falling releases nothing
        source.Heap(0.95);
        governor.Poll();
        Assert.Equal((2, 2), (high, critical)); // a jump from Normal to Critical runs both
    }

    [Fact]
    public void AReleaseThatThrowsDoesNotStopTheOthersAndLongPressureReleasesAgain()
    {
        var (governor, source, _, _) = Make();
        var ran = 0;
        governor.OnPressure(MemoryLevel.High, () => throw new InvalidOperationException("busy"));
        governor.OnPressure(MemoryLevel.High, () => ran++);
        source.Heap(0.85);
        for (var i = 0; i < 11; i++) governor.Poll();
        Assert.Equal(2, ran); // on entering, and again after ten more polls
    }

    [Fact]
    public void OneNoticeWhileCriticalAndItClearsWhenPressureDrops()
    {
        var (governor, source, _, _) = Make();
        var center = new NotificationCenter();
        _ = new LowMemoryNotice(governor, center, () => "Memory is low");
        source.Heap(0.95);
        governor.Poll();
        source.Heap(0.97);
        governor.Poll();
        source.Heap(0.93);
        governor.Poll();
        Assert.Single(center.Active);
        Assert.Single(center.History);
        source.Heap(0.80); // High: the notice goes
        governor.Poll();
        Assert.Empty(center.Active);
        source.Heap(0.95);
        governor.Poll();
        Assert.Single(center.Active);
        Assert.Equal(2, center.History.Count); // a new episode posts again
    }

    [Fact]
    public void RelieveRunsEveryReleaseAtAnyLevel()
    {
        var (governor, _, _, trims) = Make();
        var ran = 0;
        governor.OnPressure(MemoryLevel.Critical, () => ran++);
        governor.Relieve();
        Assert.Equal((1, 1), (ran, trims[0]));
    }

    [Fact]
    public void TheLimitIsASettingThatSurvivesARestartAndIsClamped()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using (var store = new SettingsStore(userData))
        {
            Assert.Equal(0, store.Current.MemoryLimitMb);
            store.Update(x => x with { MemoryLimitMb = 4096 });
        }

        using (var again = new SettingsStore(userData))
        {
            Assert.Equal(4096, again.Current.MemoryLimitMb);
            File.WriteAllText(userData.Combine("settings.json"), /*lang=json,strict*/ "{ \"memoryLimitMb\": 12 }");
            Assert.True(again.Reload());
            Assert.Equal(256, again.Current.MemoryLimitMb);
            File.WriteAllText(userData.Combine("settings.json"), /*lang=json,strict*/ "{ \"memoryLimitMb\": -5 }");
            Assert.True(again.Reload());
            Assert.Equal(0, again.Current.MemoryLimitMb);
        }
    }

    [Fact]
    public void ARefusedLimitIsReportedAndARefreshThatWorksReturnsNull()
    {
        Assert.Contains("too low", MemoryLimit.Apply(1111, () => throw new InvalidOperationException("The hard limit is too low.")), StringComparison.Ordinal);
        var refreshed = 0;
        Assert.Null(MemoryLimit.Apply(2222, () => refreshed++));
        Assert.Equal(1, refreshed);
        Assert.Null(MemoryLimit.Apply(2222, () => refreshed++)); // same value: nothing to do
        Assert.Equal(1, refreshed);
        Assert.Null(MemoryLimit.Apply(0, () => { })); // back to no limit
    }

    [Fact]
    public async Task AnOperationThatRunsOutOfMemoryRunsOnceMoreAfterTheRelease()
    {
        var tries = 0;
        var relieved = 0;
        var value = await MemoryLimit.RetryOnOutOfMemoryAsync(() => ++tries == 1 ? throw new OutOfMemoryException() : Task.FromResult(7), () => relieved++);
        Assert.Equal((7, 2, 1), (value, tries, relieved));

        tries = 0;
        await Assert.ThrowsAsync<OutOfMemoryException>(() => MemoryLimit.RetryOnOutOfMemoryAsync<int>(() => { tries++; throw new OutOfMemoryException(); }, () => relieved++));
        Assert.Equal(2, tries); // once more, not forever

        tries = 0;
        Assert.Equal(3, MemoryLimit.RetryOnOutOfMemory(() => ++tries == 1 ? throw new OutOfMemoryException() : 3, () => relieved++));
    }
}
