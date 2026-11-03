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
    public void RegisteringWritesTheUserKeyOrTheDesktopFileAndRefusesAMissingProgram()
    {
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio.exe");
        File.WriteAllText(exe, "x");
        Assert.Throws<FileNotFoundException>(() => LinkRegistration.Register(dir.File("nothing.exe")));
        var entry = LinkRegistration.DesktopEntry("/opt/g3m studio/G3MStudio");
        Assert.Contains("Exec=\"/opt/g3m studio/G3MStudio\" %u", entry, StringComparison.Ordinal);
        Assert.Contains("x-scheme-handler/g3mstudio", entry, StringComparison.Ordinal);

        if (OperatingSystem.IsWindows())
        {
            const string key = @"Software\G3MStudioTests\g3mstudio";
            try
            {
                LinkRegistration.Register(exe, key);
                using var command = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key + @"\shell\open\command");
                Assert.Equal($"\"{exe}\" \"%1\"", command?.GetValue(""));
            }
            finally
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\G3MStudioTests", false);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            LinkRegistration.Register(exe, applications: dir.File("apps"));
            Assert.True(File.Exists(Path.Combine(dir.File("apps"), LinkRegistration.DesktopFile)));
        }
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

    [Fact]
    public void FileAssociationsPointOnlyTheDataExtensionsAtTheProgramAndRemoveGivesThemBack()
    {
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio.exe");
        File.WriteAllText(exe, "x");
        Assert.Throws<FileNotFoundException>(() => FileAssociations.Register(dir.File("nothing.exe"), FileAssociations.Extensions));
        if (!OperatingSystem.IsWindows())
        {
            Assert.False(FileAssociations.Supported);
            Assert.Empty(FileAssociations.Registered());
            return;
        }

        const string classes = @"Software\G3MStudioTests\Classes";
        try
        {
            FileAssociations.Register(exe, [".win", ".csx"], classes); // .csx is not a data extension and is ignored
            Assert.Equal([".win"], FileAssociations.Registered(classes));
            using (var other = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(classes + @"\.unx")) other.SetValue("", "SomeoneElse.File"); // another program's extension stays
            FileAssociations.Remove(classes);
            Assert.Empty(FileAssociations.Registered(classes));
            using var kept = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(classes + @"\.unx");
            Assert.Equal("SomeoneElse.File", kept?.GetValue(""));
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\G3MStudioTests", false);
        }
    }
}
