using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class RunProfileTests
{
    [Fact]
    public async Task EachDataFileRemembersTheRunProfilePickedWhileItWasOpen()
    {
        using var dir = new TempDir();
        var service = new WorkspaceService(new UserData(dir.File("user")), RecentFiles.Load(new UserData(dir.File("user"))));
        var one = TestData.WriteSample(dir, "one.win");
        var two = TestData.WriteSample(dir, "two.win");
        service.Settings.Update(x => x with { RunProfiles = [new RunProfile("A", "runner-a", "-a", false), new RunProfile("B", "runner-b", "-b", true)] });

        await service.OpenAsync(one, cancellationToken: TestContext.Current.CancellationToken);
        service.Settings.Update(x => x with { ActiveRunProfile = "A", RunRunner = "runner-a", RunArguments = "-a" });
        await service.OpenAsync(two, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("A", service.Settings.Current.ActiveRunProfile); // nothing remembered for the second file yet: the options stay
        service.Settings.Update(x => x with { ActiveRunProfile = "B", RunRunner = "runner-b", RunArguments = "-b", RunDebugOutput = true });

        await service.OpenAsync(one, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(("A", "runner-a", "-a", false), (service.Settings.Current.ActiveRunProfile, service.Settings.Current.RunRunner, service.Settings.Current.RunArguments, service.Settings.Current.RunDebugOutput));
        await service.OpenAsync(two, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(("B", "runner-b", "-b", true), (service.Settings.Current.ActiveRunProfile, service.Settings.Current.RunRunner, service.Settings.Current.RunArguments, service.Settings.Current.RunDebugOutput));
        service.Close();
    }
}
