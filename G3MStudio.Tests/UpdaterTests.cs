using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using G3MStudio.Core.Updates;

namespace G3MStudio.Tests;

public sealed class UpdaterTests
{
    private static readonly SemVer Current = new(1, 0, 0, "");

    [Fact]
    public void FeedPicksTheHighestEligibleZipForThisSystem()
    {
        const string hash = "AB12";
        string Asset(string version, string os = "Windows", string arch = "x64", string? digest = "sha256:" + hash, string host = "https://github.com/o/r/releases/download/") =>
            $$"""{"name":"G3MStudio-{{os}}-{{arch}}-{{version}}.zip","browser_download_url":"{{host}}v{{version}}/G3MStudio-{{os}}-{{arch}}-{{version}}.zip","size":10{{(digest is null ? "" : $",\"digest\":\"{digest}\"")}}}""";
        string Release(string version, string asset, bool pre = false, bool draft = false) =>
            $$"""{"tag_name":"v{{version}}","prerelease":{{pre.ToString().ToLowerInvariant()}},"draft":{{draft.ToString().ToLowerInvariant()}},"assets":[{{asset}}]}""";
        var feed = UpdateFeed.GitHub("o/r");
        var json = "[" + string.Join(",",
            Release("1.0.0", Asset("1.0.0")),
            Release("1.1.0", Asset("1.1.0")),
            Release("1.2.0", Asset("1.2.0", digest: null)),
            Release("1.3.0", Asset("1.3.0", arch: "arm64")),
            Release("1.4.0", Asset("1.4.0", host: "https://evil.example/")),
            Release("1.5.0", Asset("1.5.0"), draft: true),
            Release("2.0.0-beta.1", Asset("2.0.0-beta.1"), pre: true)) + "]";

        var stable = feed.Pick(json, Current, false, "Windows", "x64");
        Assert.Equal("1.1.0", stable?.Version.ToString());
        Assert.Equal(hash.ToLowerInvariant(), stable?.Sha256);
        Assert.Equal("2.0.0-beta.1", feed.Pick(json, Current, true, "Windows", "x64")?.Version.ToString());
        Assert.Null(feed.Pick(json, new SemVer(1, 1, 0, ""), false, "Windows", "x64"));
        Assert.Null(feed.Pick(json, new SemVer(3, 0, 0, ""), true, "Windows", "x64"));
        Assert.True(new SemVer(1, 0, 0, "rc.2") < new SemVer(1, 0, 0, ""));
        Assert.True(new SemVer(1, 0, 0, "beta.2") < new SemVer(1, 0, 0, "beta.10"));
    }

    [Fact]
    public async Task CheckReadsTheFeedOverHttp()
    {
        using var temp = new TempDir();
        var zip = MakeZip(("a.txt", "new"));
        using var server = new FakeFeed(zip, "1.1.0");
        using var http = new HttpClient();
        var offer = await server.Feed.CheckAsync(http, Current, false, "Windows", "x64", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(server.Offer.Url, offer?.Url);
        Assert.Equal(zip.Length, offer?.Size);
    }

    [Fact]
    public async Task DownloadVerifiesTheDigestAndLeavesOnlyTheZip()
    {
        using var temp = new TempDir();
        using var server = new FakeFeed(Random(200_000), "1.1.0");
        using var http = new HttpClient();
        var zip = await UpdateDownloader.DownloadAsync(http, server.Offer, temp.Path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(server.Offer.Sha256, await UpdateDownloader.Sha256Async(zip, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal([Path.GetFileName(zip)], Directory.GetFiles(temp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public async Task InterruptedDownloadResumesWithARangeRequest()
    {
        using var temp = new TempDir();
        using var server = new FakeFeed(Random(200_000), "1.1.0") { CutFirstAfter = 70_000 };
        using var http = new HttpClient();
        await Assert.ThrowsAnyAsync<Exception>(() => UpdateDownloader.DownloadAsync(http, server.Offer, temp.Path, cancellationToken: TestContext.Current.CancellationToken));
        var part = Directory.GetFiles(temp.Path, "*.part").Single();
        Assert.InRange(new FileInfo(part).Length, 1, 199_999);

        var zip = await UpdateDownloader.DownloadAsync(http, server.Offer, temp.Path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(server.Offer.Sha256, await UpdateDownloader.Sha256Async(zip, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains(server.Ranges, r => r is not null && r.StartsWith("bytes=", StringComparison.Ordinal) && r.EndsWith('-'));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.part"));
    }

    [Fact]
    public async Task InterruptedDownloadRestartsWhenTheServerIgnoresRange()
    {
        using var temp = new TempDir();
        using var server = new FakeFeed(Random(200_000), "1.1.0") { CutFirstAfter = 50_000, IgnoreRange = true };
        using var http = new HttpClient();
        await Assert.ThrowsAnyAsync<Exception>(() => UpdateDownloader.DownloadAsync(http, server.Offer, temp.Path, cancellationToken: TestContext.Current.CancellationToken));
        var zip = await UpdateDownloader.DownloadAsync(http, server.Offer, temp.Path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(server.Offer.Sha256, await UpdateDownloader.Sha256Async(zip, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DigestMismatchDeletesTheFileAndRefusesTheUpdate()
    {
        using var temp = new TempDir();
        var good = Random(10_000);
        using var server = new FakeFeed(good, "1.1.0") { ServedBytes = Random(10_000) };
        var error = await Assert.ThrowsAsync<UpdateException>(() => UpdateDownloader.DownloadAsync(new HttpClient(), server.Offer, temp.Path, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(UpdateError.DigestMismatch, error.Error);
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public async Task ABadResumedPartRestartsOnceAndSucceeds()
    {
        using var temp = new TempDir();
        using var server = new FakeFeed(Random(50_000), "1.1.0");
        await File.WriteAllBytesAsync(Path.Combine(temp.Path, server.Offer.AssetName + ".part"), Random(20_000), TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        var zip = await UpdateDownloader.DownloadAsync(http, server.Offer, temp.Path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(server.Offer.Sha256, await UpdateDownloader.Sha256Async(zip, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StageExtractsEverythingAndLeavesTheProgramFolderUntouched()
    {
        using var temp = new TempDir();
        var zip = Path.Combine(temp.Path, "u.zip");
        File.WriteAllBytes(zip, MakeZip(("app.cmd", "new"), ("sub/data.txt", "d")));
        var program = Directory.CreateDirectory(Path.Combine(temp.Path, "program")).FullName;
        File.WriteAllText(Path.Combine(program, "app.cmd"), "old");

        var staging = UpdateInstaller.Stage(zip, program);
        Assert.Equal("d", File.ReadAllText(Path.Combine(staging, "sub", "data.txt")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(program, "app.cmd")));
    }

    [Fact]
    public void StageOfABrokenZipLeavesNothingBehind()
    {
        using var temp = new TempDir();
        var program = Directory.CreateDirectory(Path.Combine(temp.Path, "program")).FullName;
        File.WriteAllText(Path.Combine(program, "app.cmd"), "old");
        var bytes = MakeZip(("app.cmd", new string('x', 5000)), ("b.txt", "b"));
        var zip = Path.Combine(temp.Path, "u.zip");
        File.WriteAllBytes(zip, bytes[..(bytes.Length / 2)]);

        var error = Assert.Throws<UpdateException>(() => UpdateInstaller.Stage(zip, program));
        Assert.Equal(UpdateError.BadArchive, error.Error);
        Assert.False(Directory.Exists(Path.Combine(program, UpdateInstaller.WorkFolder, "new")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(program, "app.cmd")));
    }

    [Fact]
    public void ANonWritableFolderIsReportedAndNothingIsReplaced()
    {
        using var temp = new TempDir();
        var program = Directory.CreateDirectory(Path.Combine(temp.Path, "program")).FullName;
        File.WriteAllText(Path.Combine(program, "app.cmd"), "old");
        var zip = Path.Combine(temp.Path, "u.zip");
        File.WriteAllBytes(zip, MakeZip(("app.cmd", "new")));

        using (new ReadOnlyFolder(program))
        {
            Assert.SkipWhen(CanWrite(program), "this account can write to the folder anyway (for example root)");
            var error = Assert.Throws<UpdateException>(() => UpdateInstaller.Stage(zip, program));
            Assert.Equal(UpdateError.NotWritable, error.Error);
            Assert.Equal(program, error.Path);
        }
        Assert.Equal("old", File.ReadAllText(Path.Combine(program, "app.cmd")));
        Assert.False(Directory.Exists(Path.Combine(program, UpdateInstaller.WorkFolder)));
    }

    [Fact]
    public async Task ScriptWaitsForTheAppThenReplacesFilesAndRestarts()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows script");
        using var temp = new TempDir();
        var program = Program(temp, ("app.cmd", Marker), ("data.txt", "old"), ("keep.txt", "keep"));
        var zip = Path.Combine(temp.Path, "u.zip");
        await File.WriteAllBytesAsync(zip, MakeZip(("app.cmd", Marker), ("data.txt", "new"), ("sub/extra.txt", "extra")), TestContext.Current.CancellationToken);
        UpdateInstaller.Stage(zip, program);

        using var stub = Process.Start(new ProcessStartInfo("ping", "-n 4 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
        UpdateInstaller.Launch(UpdateInstaller.WriteScript(program, "app.cmd", stub.Id, Path.Combine(temp.Path, "script")));
        await Task.Delay(1000, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("old", (await File.ReadAllTextAsync(Path.Combine(program, "data.txt"), TestContext.Current.CancellationToken)));

        await stub.WaitForExitAsync(cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor(() => File.Exists(Path.Combine(program, "started.txt")));
        Assert.Equal("ok", (await File.ReadAllTextAsync(UpdateInstaller.ResultFile(program), TestContext.Current.CancellationToken)).Trim());
        Assert.Equal("new", (await File.ReadAllTextAsync(Path.Combine(program, "data.txt"), TestContext.Current.CancellationToken)));
        Assert.Equal("extra", (await File.ReadAllTextAsync(Path.Combine(program, "sub", "extra.txt"), TestContext.Current.CancellationToken)));
        Assert.Equal("keep", (await File.ReadAllTextAsync(Path.Combine(program, "keep.txt"), TestContext.Current.CancellationToken)));
        Assert.False(Directory.Exists(Path.Combine(program, UpdateInstaller.WorkFolder, "old")));
        Assert.False(Directory.Exists(Path.Combine(program, UpdateInstaller.WorkFolder, "new")));
    }

    [Fact]
    public async Task ScriptReplacesTheExecutableOfARunningProgramFolder()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows script");
        using var temp = new TempDir();
        var system = Environment.SystemDirectory;
        var program = Directory.CreateDirectory(Path.Combine(temp.Path, "Program Files (x86) 100%")).FullName;
        var exe = Path.Combine(program, "app.exe");
        File.Copy(Path.Combine(system, "ping.exe"), exe);
        var replacement = await File.ReadAllBytesAsync(Path.Combine(system, "hostname.exe"), TestContext.Current.CancellationToken);
        var staged = Directory.CreateDirectory(Path.Combine(program, UpdateInstaller.WorkFolder, "new")).FullName;
        await File.WriteAllBytesAsync(Path.Combine(staged, "app.exe"), replacement, TestContext.Current.CancellationToken);

        using var running = Process.Start(new ProcessStartInfo(exe, "-n 4 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
        Assert.Throws<UnauthorizedAccessException>(() => File.Delete(exe));
        UpdateInstaller.Launch(UpdateInstaller.WriteScript(program, "app.exe", running.Id, Path.Combine(temp.Path, "script")));

        await running.WaitForExitAsync(cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor(() => File.Exists(UpdateInstaller.ResultFile(program)));
        Assert.Equal("ok", (await File.ReadAllTextAsync(UpdateInstaller.ResultFile(program), TestContext.Current.CancellationToken)).Trim());
        Assert.Equal(replacement, (await File.ReadAllBytesAsync(exe, TestContext.Current.CancellationToken)));
        Assert.False(Directory.Exists(Path.Combine(program, UpdateInstaller.WorkFolder, "old")));
    }

    [Fact]
    public async Task ScriptRollsBackWhenAFileCannotBeReplaced()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows script");
        using var temp = new TempDir();
        var program = Program(temp, ("app.cmd", Marker), ("a.txt", "old a"), ("b.txt", "old b"));
        var staged = Directory.CreateDirectory(Path.Combine(program, UpdateInstaller.WorkFolder, "new")).FullName;
        foreach (var (name, text) in new[] { ("a.txt", "new a"), ("b.txt", "new b"), ("c.txt", "new c") })
            await File.WriteAllTextAsync(Path.Combine(staged, name), text, TestContext.Current.CancellationToken);

        using (new FileStream(Path.Combine(program, "b.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using var stub = Process.Start(new ProcessStartInfo("ping", "-n 2 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
            UpdateInstaller.Launch(UpdateInstaller.WriteScript(program, "app.cmd", stub.Id, Path.Combine(temp.Path, "script")));
            await WaitFor(() => File.Exists(Path.Combine(program, "started.txt")));
        }
        Assert.StartsWith("failed", (await File.ReadAllTextAsync(UpdateInstaller.ResultFile(program), TestContext.Current.CancellationToken)).Trim(), StringComparison.Ordinal);
        Assert.Equal("old a", (await File.ReadAllTextAsync(Path.Combine(program, "a.txt"), TestContext.Current.CancellationToken)));
        Assert.Equal("old b", (await File.ReadAllTextAsync(Path.Combine(program, "b.txt"), TestContext.Current.CancellationToken)));
        Assert.False(File.Exists(Path.Combine(program, "c.txt")));
        Assert.False(Directory.Exists(Path.Combine(program, UpdateInstaller.WorkFolder, "new")));
    }

    [Fact]
    public async Task ShellScriptReplacesFilesAndRestarts()
    {
        var sh = OperatingSystem.IsWindows() ? @"C:\Program Files\Git\usr\bin\sh.exe" : "/bin/sh";
        Assert.SkipUnless(File.Exists(sh), "no sh available");
        using var temp = new TempDir();
        var program = Path.Combine(temp.Path, "program").Replace('\\', '/');
        Directory.CreateDirectory(program);
        await File.WriteAllTextAsync(Path.Combine(program, "app.sh"), "#!/bin/sh\necho old > \"$(dirname \"$0\")/started.txt\"\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(program, "data.txt"), "old", TestContext.Current.CancellationToken);
        var staged = Directory.CreateDirectory(Path.Combine(program, UpdateInstaller.WorkFolder, "new", "sub")).Parent!.FullName;
        await File.WriteAllTextAsync(Path.Combine(staged, "app.sh"), "#!/bin/sh\necho new > \"$(dirname \"$0\")/started.txt\"\n", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(staged, "data.txt"), "new", TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(staged, "app.sh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await File.WriteAllTextAsync(Path.Combine(staged, "sub", "it's.txt"), "quote", TestContext.Current.CancellationToken);

        // The script waits on a pid that is already gone, then swaps; sh must not use the Windows pid space, so give it an unused one.
        var script = Path.Combine(temp.Path, "update.sh");
        await File.WriteAllTextAsync(script, UpdateInstaller.UnixScript(program, "app.sh", 99999999, ["app.sh", "data.txt", Path.Combine("sub", "it's.txt")]).ReplaceLineEndings("\n"), TestContext.Current.CancellationToken);
        using var run = Process.Start(new ProcessStartInfo(sh, [script.Replace('\\', '/')]) { UseShellExecute = false, CreateNoWindow = true })!;
        await run.WaitForExitAsync(cancellationToken: TestContext.Current.CancellationToken);
        await WaitFor(() => File.Exists(Path.Combine(program, "started.txt")));

        Assert.Equal("ok", (await File.ReadAllTextAsync(UpdateInstaller.ResultFile(program), TestContext.Current.CancellationToken)).Trim());
        Assert.Equal("new", (await File.ReadAllTextAsync(Path.Combine(program, "data.txt"), TestContext.Current.CancellationToken)));
        Assert.Equal("new", (await File.ReadAllTextAsync(Path.Combine(program, "started.txt"), TestContext.Current.CancellationToken)).Trim());
        Assert.Equal("quote", (await File.ReadAllTextAsync(Path.Combine(program, "sub", "it's.txt"), TestContext.Current.CancellationToken)));
        Assert.False(Directory.Exists(Path.Combine(program, UpdateInstaller.WorkFolder, "old")));
    }

    private const string Marker = "@echo off\r\necho started> \"%~dp0started.txt\"\r\nexit\r\n";

    private static string Program(TempDir temp, params (string Name, string Text)[] files)
    {
        var program = Directory.CreateDirectory(Path.Combine(temp.Path, "program")).FullName;
        foreach (var (name, text) in files) File.WriteAllText(Path.Combine(program, name), text);
        return program;
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(100);
        Assert.True(condition(), "timed out");
    }

    private static byte[] Random(int size) => RandomNumberGenerator.GetBytes(size);

    private static byte[] MakeZip(params (string Name, string Text)[] files)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, text) in files)
                using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) writer.Write(text);
        return stream.ToArray();
    }

    private static bool CanWrite(string folder)
    {
        try
        {
            var probe = Path.Combine(folder, "probe");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "g3ms-upd-" + Guid.NewGuid().ToString("N")[..8])).FullName;

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Removes the right to create files for everyone (ACL on Windows, mode on Unix) until disposed.</summary>
    private sealed class ReadOnlyFolder : IDisposable
    {
        private readonly string _folder;

        public ReadOnlyFolder(string folder)
        {
            _folder = folder;
            if (OperatingSystem.IsWindows()) Icacls("/deny", "*S-1-1-0:(OI)(CI)(WD,AD)");
            else File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        public void Dispose()
        {
            if (OperatingSystem.IsWindows()) Icacls("/remove:d", "*S-1-1-0");
            else File.SetUnixFileMode(_folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        private void Icacls(params string[] args) =>
            Process.Start(new ProcessStartInfo("icacls", [_folder, .. args]) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!.WaitForExit();
    }

    /// <summary>Local stand-in for the GitHub releases feed and the asset host, with Range support and fault switches.</summary>
    private sealed class FakeFeed : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly byte[] _zip;
        private readonly string _json;
        private int _requests;

        public FakeFeed(byte[] zip, string version)
        {
            _zip = zip;
            var port = ((IPEndPoint)Probe()).Port;
            Base = $"http://127.0.0.1:{port}/";
            var name = $"G3MStudio-Windows-x64-{version}.zip";
            Offer = new UpdateOffer(new SemVer(1, 1, 0, ""), name, new Uri(Base + "dl/" + name), zip.Length, Convert.ToHexStringLower(SHA256.HashData(zip)));
            _json = $$"""[{"tag_name":"v{{version}}","prerelease":false,"draft":false,"assets":[{"name":"{{name}}","browser_download_url":"{{Offer.Url}}","size":{{zip.Length}},"digest":"sha256:{{Offer.Sha256}}"}]}]""";
            Feed = new UpdateFeed(new Uri(Base + "releases"), Base + "dl/");
            _listener.Prefixes.Add(Base);
            _listener.Start();
            _ = Task.Run(Loop);
        }

        public string Base { get; }

        public UpdateOffer Offer { get; }

        public UpdateFeed Feed { get; }

        public int CutFirstAfter { get; set; } = -1;

        public bool IgnoreRange { get; set; }

        public byte[]? ServedBytes { get; set; }

        public List<string?> Ranges { get; } = [];

        private static EndPoint Probe()
        {
            using var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var endpoint = l.LocalEndpoint;
            l.Stop();
            return endpoint;
        }

        private async Task Loop()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { return; }
                Handle(context);
            }
        }

        private void Handle(HttpListenerContext context)
        {
            var response = context.Response;
            if (context.Request.Url!.AbsolutePath == "/releases")
            {
                var json = Encoding.UTF8.GetBytes(_json);
                response.ContentType = "application/json";
                response.OutputStream.Write(json);
                response.Close();
                return;
            }
            var range = context.Request.Headers["Range"];
            Ranges.Add(range);
            var body = ServedBytes ?? _zip;
            var start = !IgnoreRange && range is not null && range.StartsWith("bytes=", StringComparison.Ordinal) ? long.Parse(range[6..^1], System.Globalization.CultureInfo.InvariantCulture) : 0;
            if (start > 0)
            {
                response.StatusCode = 206;
                response.AddHeader("Content-Range", $"bytes {start}-{body.Length - 1}/{body.Length}");
            }
            response.ContentLength64 = body.Length - start;
            var cut = Interlocked.Increment(ref _requests) == 1 && CutFirstAfter >= 0;
            response.OutputStream.Write(body, (int)start, cut ? CutFirstAfter : body.Length - (int)start);
            if (cut) response.Abort();
            else response.Close();
        }

        public void Dispose() => _listener.Close();
    }
}
