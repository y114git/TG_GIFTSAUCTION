using System.Buffers.Binary;
using G3MStudio.Core.Links;

namespace G3MStudio.Tests;

/// <summary>Registration of the scheme, the Open with entries and the file associations. Every test writes to a memory store and a temp folder, never to the real registry.</summary>
public sealed class SystemRegistrationTests
{
    [Fact]
    public void RegisteringWritesTheSchemeAndRefusesAMissingProgram()
    {
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio.exe");
        File.WriteAllText(exe, "x");
        var memory = new MemoryClasses();
        var places = Places.Sandbox(dir.File("sandbox"), memory);
        Assert.Throws<FileNotFoundException>(() => LinkRegistration.Register(dir.File("nothing.exe"), places));
        Assert.False(LinkRegistration.IsCurrent(exe, places));

        LinkRegistration.Ensure(exe, places);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal($"\"{exe}\" \"%1\"", memory.Get(@"g3mstudio\shell\open\command", null));
            Assert.Equal("", memory.Get("g3mstudio", "URL Protocol"));
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.True(File.Exists(Path.Combine(places.Applications, LinkRegistration.DesktopFile)));
        }

        if (!OperatingSystem.IsMacOS()) Assert.True(LinkRegistration.IsCurrent(exe, places)); // a second start finds it all in place
    }

    [Fact]
    public void OpenWithListsTheProgramForEveryDataExtensionWithoutTakingTheDefault()
    {
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio.exe");
        File.WriteAllText(exe, "x");
        var memory = new MemoryClasses();
        memory.Set(".win", null, "SomeoneElse.File");
        memory.Set(@"Applications\G3MStudio.exe\shell\open\command", null, "left by an older version");
        var places = Places.Sandbox(dir.File("sandbox"), memory);
        FileAssociations.OpenWith(exe, places);
        if (!OperatingSystem.IsWindows())
        {
            // Linux lists it through the .desktop file and the MIME package; macOS has no bundle here and writes nothing.
            Assert.Equal(OperatingSystem.IsLinux(), File.Exists(Path.Combine(places.Applications, LinkRegistration.DesktopFile)) && File.Exists(FileAssociations.MimeFile(places)));
            Assert.Equal("SomeoneElse.File", memory.Get(".win", null));
            return;
        }

        foreach (var extension in FileAssociations.Extensions)
        {
            Assert.NotNull(memory.Get($@"{extension}\OpenWithProgids", FileAssociations.ProgId));
        }

        // Nothing may list G3MStudio for a file type of other programs: no Applications entry (the chooser offers those for every type), no wildcard entry, no other extension.
        Assert.DoesNotContain(memory.Keys, k => k.StartsWith("APPLICATIONS", StringComparison.Ordinal) && !k.StartsWith(@"APPLICATIONS\G3MSTUDIO.EXE", StringComparison.Ordinal) || k.StartsWith('*') || k.StartsWith("SYSTEMFILEASSOCIATIONS", StringComparison.Ordinal));
        Assert.All(memory.Keys.Where(k => k.StartsWith('.')), k => Assert.Contains(FileAssociations.Extensions.Select(e => e.ToUpperInvariant()), e => k == e || k.StartsWith(e + "\\", StringComparison.Ordinal)));

        Assert.NotNull(memory.Get(@"Applications\G3MStudio.exe", "NoOpenWith")); // hidden from the chooser, the old command stays for a default chosen through it
        Assert.Equal("left by an older version", memory.Get(@"Applications\G3MStudio.exe\shell\open\command", null));
        Assert.Equal("SomeoneElse.File", memory.Get(".win", null));
        Assert.Equal($"\"{exe}\" \"%1\"", memory.Get($@"{FileAssociations.ProgId}\shell\open\command", null));
        Assert.EndsWith("g3mstudio.ico", memory.Get($@"{FileAssociations.ProgId}\DefaultIcon", null), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(dir.File("sandbox"), "icons", "g3mstudio.ico")));
    }

    [Fact]
    public void RegisterTakesOnlyTheDataExtensionsAndRemoveGivesThemBackWithoutTouchingOthers()
    {
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio.exe");
        File.WriteAllText(exe, "x");
        Assert.Throws<FileNotFoundException>(() => FileAssociations.Register(dir.File("nothing.exe"), FileAssociations.Extensions, Places.Sandbox(dir.File("s"))));
        var memory = new MemoryClasses();
        var places = Places.Sandbox(dir.File("sandbox"), memory);
        FileAssociations.Register(exe, [".win", ".csx"], places); // .csx is not a data extension and is ignored
        Assert.Null(memory.Get(".csx", null));
        if (!OperatingSystem.IsWindows())
        {
            // The class entries are Windows only; Linux takes the whole MIME type, macOS leaves the default to Finder.
            Assert.Equal(OperatingSystem.IsLinux() ? FileAssociations.Extensions : [], FileAssociations.Registered(places));
            FileAssociations.Remove(places);
            Assert.Empty(FileAssociations.Registered(places));
            return;
        }

        Assert.Equal([".win"], FileAssociations.Registered(places));
        Assert.Equal($"\"{exe}\" --run \"%1\"", memory.Get($@"{FileAssociations.ProgId}\shell\launch\command", null));
        Assert.Equal($"\"{exe}\" --run-with-runtime \"%1\"", memory.Get($@"{FileAssociations.ProgId}\shell\special_launch\command", null));
        Assert.True(memory.Notified > 0); // the shell is told, so the icons refresh
        memory.Set(".unx", null, "SomeoneElse.File"); // another program's extension stays
        FileAssociations.Remove(places);
        Assert.Empty(FileAssociations.Registered(places));
        Assert.Equal("SomeoneElse.File", memory.Get(".unx", null));
        Assert.Null(memory.Get(".win", null)); // there was no default before: the value is gone, so the machine-wide default shows again
        Assert.NotNull(memory.Get($@"{FileAssociations.ProgId}\shell\open\command", null)); // still listed under Open with
    }

    [Fact]
    public void RemoveBringsBackTheDefaultProgramThatWasThereBeforeRegister()
    {
        if (!OperatingSystem.IsWindows()) return; // the class entries exist only there; the Linux list is covered below
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio.exe");
        File.WriteAllText(exe, "x");
        var memory = new MemoryClasses();
        memory.Set(".win", null, "SomeoneElse.File");
        var places = Places.Sandbox(dir.File("sandbox"), memory);
        FileAssociations.Register(exe, [".win"], places);
        FileAssociations.Register(exe, [".win"], places); // a second click must not keep G3MStudio as the previous program
        Assert.Equal(FileAssociations.ProgId, memory.Get(".win", null));
        FileAssociations.Remove(places);
        Assert.Equal("SomeoneElse.File", memory.Get(".win", null));
        Assert.Null(memory.Get(".win", "G3MStudio.Previous"));
        Assert.Empty(FileAssociations.Registered(places));
    }

    [Fact]
    public void RegisteringRefusesTheDotnetHostAndABuildOutputFolder()
    {
        using var dir = new TempDir();
        var host = dir.File("dotnet.exe");
        File.WriteAllText(host, "x");
        var debug = dir.File("bin/Debug/net10.0/G3MStudio.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(debug)!);
        File.WriteAllText(debug, "x");
        var memory = new MemoryClasses();
        var places = Places.Sandbox(dir.File("sandbox"), memory);
        foreach (var program in new[] { host, debug })
        {
            Assert.Throws<IOException>(() => LinkRegistration.Register(program, places));
            Assert.Throws<IOException>(() => FileAssociations.Register(program, FileAssociations.Extensions, places));
            LinkRegistration.Ensure(program, places); // never throws, writes nothing
        }

        Assert.Null(memory.Get(@"g3mstudio\shell\open\command", null));
        Assert.False(Directory.Exists(places.Applications));
        Assert.NotNull(FileAssociations.TryRegister(host, places)); // the Settings page shows the reason
    }

    [Fact]
    public void TheDefaultListOfTheDataTypeIsEditedInItsSectionOnlyAndWrittenOnlyWhenItChanges()
    {
        using var dir = new TempDir();
        var places = Places.Sandbox(dir.File("sandbox"));
        var file = Path.Combine(places.Config, "mimeapps.list");
        Directory.CreateDirectory(places.Config);
        var mime = FileAssociations.MimeType;
        File.WriteAllLines(file, ["[Added Associations]", $"{mime}=other.desktop;", "", "[Default Applications]", $"{mime}=other.desktop;g3mstudio.desktop;", "text/plain=edit.desktop;"]);
        Assert.Equal(["other.desktop", "g3mstudio.desktop"], FileAssociations.DefaultEntries(File.ReadAllLines(file)));

        FileAssociations.SetDefaultLine(places, add: true);
        Assert.Equal(["[Added Associations]", $"{mime}=other.desktop;", "", "[Default Applications]", $"{mime}=g3mstudio.desktop;other.desktop;", "text/plain=edit.desktop;"], File.ReadAllLines(file));
        var written = File.GetLastWriteTimeUtc(file);
        File.SetLastWriteTimeUtc(file, written.AddMinutes(-5));
        FileAssociations.SetDefaultLine(places, add: true); // nothing to change: no write
        Assert.Equal(written.AddMinutes(-5), File.GetLastWriteTimeUtc(file));

        FileAssociations.SetDefaultLine(places, add: false);
        Assert.Equal(["[Added Associations]", $"{mime}=other.desktop;", "", "[Default Applications]", $"{mime}=other.desktop;", "text/plain=edit.desktop;"], File.ReadAllLines(file));
        File.WriteAllLines(file, ["[Default Applications]", $"{mime}=g3mstudio.desktop;"]);
        FileAssociations.SetDefaultLine(places, add: false);
        Assert.Equal(["[Default Applications]"], File.ReadAllLines(file)); // the last entry takes the key with it
        Assert.Empty(Directory.GetFiles(places.Config, "*.tmp"));
    }

    [Fact]
    public void AnInfoPlistOfASignedBundleOrWithItsOwnTypesIsLeftAlone()
    {
        using var dir = new TempDir();
        string Bundle(string name, string plist, bool signed)
        {
            var contents = Path.Combine(dir.File(name), "Contents");
            Directory.CreateDirectory(Path.Combine(contents, "MacOS"));
            File.WriteAllText(Path.Combine(contents, "Info.plist"), plist);
            if (signed) Directory.CreateDirectory(Path.Combine(contents, "_CodeSignature"));
            return Path.Combine(contents, "MacOS", "G3MStudio");
        }

        const string plain = "<plist>\n<dict>\n</dict>\n</plist>\n";
        var unsigned = Bundle("a.app", plain, false);
        Assert.True(MacBundle.Declare(unsigned, false));
        Assert.Contains("CFBundleDocumentTypes", File.ReadAllText(MacBundle.PlistOf(unsigned)!), StringComparison.Ordinal);
        Assert.True(MacBundle.Declare(unsigned, false)); // declared once: the marker says so
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(MacBundle.PlistOf(unsigned)!)!, "*.tmp"));

        var signed = Bundle("b.app", plain, true);
        Assert.False(MacBundle.Declare(signed, false));
        Assert.Equal(plain, File.ReadAllText(MacBundle.PlistOf(signed)!));

        const string own = "<plist>\n<dict>\n<key>CFBundleDocumentTypes</key>\n<array/>\n</dict>\n</plist>\n";
        Assert.Equal(own, MacBundle.Merge(own));
        var theirs = Bundle("c.app", own, false);
        Assert.False(MacBundle.Declare(theirs, false));
        Assert.Equal(own, File.ReadAllText(MacBundle.PlistOf(theirs)!));
    }

    [Fact]
    public void ALinuxStartThatStoppedAfterTheDesktopFileTriesAgain()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio");
        File.WriteAllText(exe, "x");
        var places = Places.Sandbox(dir.File("sandbox"));
        LinkRegistration.Ensure(exe, places);
        Assert.True(LinkRegistration.IsCurrent(exe, places));
        File.Delete(FileAssociations.MimeFile(places));
        Assert.False(LinkRegistration.IsCurrent(exe, places));
    }

    [Fact]
    public void RemoveFailingOnALockedKeyBecomesAMessageInsteadOfAnException()
    {
        if (!OperatingSystem.IsWindows()) return; // the class entries are Windows only
        using var dir = new TempDir();
        var places = Places.Sandbox(dir.File("sandbox"), new LockedClasses());
        Assert.Equal("locked", FileAssociations.TryRemove(places));
    }

    private sealed class LockedClasses : IClassesStore
    {
        public void Set(string key, string? name, string value) { }

        public string? Get(string key, string? name) => FileAssociations.ProgId;

        public void DeleteValue(string key, string? name) => throw new UnauthorizedAccessException("locked");

        public void DeleteTree(string key) { }

        public void Changed() { }
    }

    [Fact]
    public void ARunningToolIsEndedAfterTheTimeoutAndAFastOneIsNot()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) return;
        var slow = OperatingSystem.IsWindows() ? new System.Diagnostics.ProcessStartInfo("ping", "-n 30 127.0.0.1") : new System.Diagnostics.ProcessStartInfo("sleep", "30");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(LinkRegistration.Wait(slow, 300));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
        var quick = OperatingSystem.IsWindows() ? new System.Diagnostics.ProcessStartInfo("cmd", "/c echo hi") : new System.Diagnostics.ProcessStartInfo("echo", "hi");
        Assert.True(LinkRegistration.Wait(quick, 10000));
    }

    [Fact]
    public void LinuxAndMacFilesDeclareTheTypesTheLauncherAndTheScheme()
    {
        var entry = LinkRegistration.DesktopEntry("/opt/g3m studio/G3MStudio");
        Assert.Contains("Exec=\"/opt/g3m studio/G3MStudio\" %U", entry, StringComparison.Ordinal);
        Assert.Contains("x-scheme-handler/g3mstudio;application/x-gamemaker-data;", entry, StringComparison.Ordinal);
        var xml = FileAssociations.MimeXml();
        foreach (var extension in FileAssociations.Extensions) Assert.Contains($"<glob pattern=\"*{extension}\"/>", xml, StringComparison.Ordinal);
        Assert.Contains("application/x-gamemaker-data", xml, StringComparison.Ordinal);
        Assert.DoesNotContain(".3ds", xml, StringComparison.Ordinal); // 3D Studio models are not game data
        Assert.DoesNotContain(".3ds", FileAssociations.Extensions);
        var plist = MacBundle.Merge("<plist version=\"1.0\">\n<dict>\n  <key>CFBundleName</key>\n  <string>G3MStudio</string>\n</dict>\n</plist>\n");
        Assert.Contains("<string>g3mstudio</string>", plist, StringComparison.Ordinal);
        Assert.Contains("<string>unx</string>", plist, StringComparison.Ordinal);
        Assert.Contains("Alternate", plist, StringComparison.Ordinal);
        Assert.Equal(plist, MacBundle.Merge(plist)); // merged once
        Assert.Equal("/a/G3MStudio.app/Contents/Info.plist", MacBundle.PlistOf("/a/G3MStudio.app/Contents/MacOS/G3MStudio")?.Replace('\\', '/'));
        Assert.Null(MacBundle.PlistOf("/usr/bin/G3MStudio"));
    }

    [Fact]
    public void TheDesktopExecLineEscapesWhatTheSpecReservesAndAnAppImageMountIsNotRegistered()
    {
        Assert.Equal("/a/b%%c\\\\\\\\d\\\\\"e\\\\$f\\\\`g", LinkRegistration.ExecQuote("/a/b%c\\d\"e$f`g"));
        Assert.True(LinkRegistration.IsMountedImage("/tmp/.mount_G3MStuAbC12/usr/bin/G3MStudio"));
        Assert.False(LinkRegistration.IsMountedImage("/opt/g3m/G3MStudio"));
    }

    [Fact]
    public void LinuxWritesTheDesktopFileTheMimeTypeAndTheDefaultLineInItsSandbox()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var dir = new TempDir();
        var exe = dir.File("G3MStudio");
        File.WriteAllText(exe, "x");
        var places = Places.Sandbox(dir.File("sandbox"));
        FileAssociations.Register(exe, FileAssociations.Extensions, places);
        Assert.True(File.Exists(Path.Combine(places.Mime, "packages", "g3mstudio.xml")));
        Assert.Equal(FileAssociations.Extensions, FileAssociations.Registered(places));
        FileAssociations.Remove(places);
        Assert.Empty(FileAssociations.Registered(places));
    }

    [Fact]
    public void TheIconIsAnIcoWithAPngPicture()
    {
        var ico = StudioIcon.Ico();
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(ico.AsSpan(2)));
        var png = StudioIcon.Png();
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png[..4]);
        Assert.Equal(256, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.True(ico.AsSpan().IndexOf(png.AsSpan(0, 16)) >= 0);
    }
}
