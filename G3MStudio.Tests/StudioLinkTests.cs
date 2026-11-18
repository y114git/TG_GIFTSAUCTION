using G3MStudio.Core.Links;

namespace G3MStudio.Tests;

public sealed class StudioLinkTests
{
    [Fact]
    public void ALinkToAnExistingLocalFileGivesItsPathAndTheLinkOfAFileRoundTrips()
    {
        using var dir = new TempDir();
        var file = TestData.WriteSample(dir, "my game.win");
        var link = StudioLink.ForFile(file);
        Assert.StartsWith("g3mstudio://file:///", link, StringComparison.Ordinal);
        Assert.Equal(new StudioLink(StudioLinkKind.File, Path.GetFullPath(file)), StudioLink.Parse(link));
        Assert.Equal(StudioLinkKind.File, StudioLink.Parse(link + "/ ")!.Kind); // a trailing slash and spaces are dropped
        Assert.Equal(StudioLinkKind.File, StudioLink.Parse(link + ",ignored")!.Kind); // the text stops at the first comma
        Assert.Equal(StudioLinkKind.File, StudioLink.Parse(link.Replace("g3mstudio://", "G3MSTUDIO://", StringComparison.Ordinal))!.Kind);
    }

    [Fact]
    public void AFileThatIsMissingOrOnTheNetworkIsRefused()
    {
        using var dir = new TempDir();
        Assert.Null(StudioLink.Parse(StudioLink.ForFile(Path.Combine(dir.Path, "missing.win"))));
        Assert.Null(StudioLink.Parse("g3mstudio://file://server/share/data.win"));
        Assert.Null(StudioLink.Parse("g3mstudio://file:///"));
        Assert.Null(StudioLink.Parse("g3mstudio://file:///not/a/real/path/at/all.win"));
    }

    [Fact]
    public void ActionsAreLimitedToTheNamedOnesAndAWebAddressIsRepaired()
    {
        Assert.Equal(new StudioLink(StudioLinkKind.Action, "new-mod"), StudioLink.Parse("g3mstudio://action/new-mod"));
        Assert.Equal(new StudioLink(StudioLinkKind.Action, "open"), StudioLink.Parse("g3mstudio://action/OPEN/"));
        Assert.Null(StudioLink.Parse("g3mstudio://action/format-disk"));
        Assert.Equal(new StudioLink(StudioLinkKind.Download, "https://example.com/mod.zip"), StudioLink.Parse("g3mstudio://https://example.com/mod.zip"));
        Assert.Equal(new StudioLink(StudioLinkKind.Download, "https://example.com/mod.zip"), StudioLink.Parse("g3mstudio://https//example.com/mod.zip"));
        Assert.Null(StudioLink.Parse("g3mstudio://http://example.com/mod.zip")); // plain http is not taken
        Assert.Null(StudioLink.Parse("g3mstudio://https://example.com"));
        Assert.Null(StudioLink.Parse("g3m://file:///C:/data.win"));
        Assert.Null(StudioLink.Parse(""));
        Assert.Null(StudioLink.Parse(null));
    }

    [Fact]
    public async Task ALaterLaunchHandsItsTextToTheRunningProgramAndNothingAnswersWithoutOne()
    {
        var name = "g3mt-" + Guid.NewGuid().ToString("N")[..10];
        Assert.False(SingleInstance.TrySend("nobody listens", name, 100));
        var got = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var instance = new SingleInstance();
        Assert.True(instance.Listen(got.SetResult, name));
        using var second = new SingleInstance();
        Assert.False(second.Listen(_ => { }, name)); // the name is taken, so this one is not the first
        Assert.True(SingleInstance.DefaultName.Length < 30); // short enough for a macOS domain socket
        Assert.True(SingleInstance.TrySend("g3mstudio://action/open", name));
        Assert.Equal("g3mstudio://action/open", await got.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

}
