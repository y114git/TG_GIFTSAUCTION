using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace G3MStudio.Tests;

/// <summary>The crash reporting state (the sink, the Sentry session) is static: its tests run alone, so no window built by another test can switch it off in the middle.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CrashReportingGroup
{
    public const string Name = "CrashReporting";
}

/// <summary>
/// Every way a failure reaches Sentry, with the sending replaced by a transport that keeps the serialized events in memory: nothing here touches the network,
/// and the key is a made-up one that points nowhere. One event per failure, no path or name in it, and none at all while the setting is off.
/// </summary>
[Collection(CrashReportingGroup.Name)]
public sealed class CrashCaptureTests : IDisposable
{
    private const string Key = "https://0123456789abcdef0123456789abcdef@localhost:9/1";

    private sealed class MemoryTransport : ITransport
    {
        public ConcurrentQueue<string> Envelopes { get; } = new();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            Envelopes.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
        }

        // The envelopes that carry an event; other items (sessions, attachments) are not failures.
        public List<string> Events(string marker) => [.. Envelopes.Where(e => e.Contains("\"type\":\"event\"", StringComparison.Ordinal) && e.Contains(marker, StringComparison.Ordinal))];
    }

    private readonly MemoryTransport _transport = new();

    public CrashCaptureTests() => CrashReporting.Apply(true, Key, _transport);

    public void Dispose() => CrashReporting.Apply(false);

    private static string Marker() => "m" + Guid.NewGuid().ToString("N")[..10];

    // A framework message start is the only exception text that leaves; the marker rides on it.
    private static InvalidOperationException Boom(string text) => new("Operation is not valid due to the current state of the object. " + text);

    private async Task<List<string>> Sent(string marker)
    {
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        return _transport.Events(marker);
    }

    private static void RaiseAppDomainUnhandled(Exception exception)
    {
        // AppDomain.UnhandledException is kept in this static field of AppContext, which the runtime raises on a crash. Only the handlers of Sentry are called here:
        // the test host has its own, which would end the run.
        var field = typeof(AppContext).GetField("UnhandledException", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("AppContext has no UnhandledException field in this runtime.");
        var handler = (UnhandledExceptionEventHandler?)field.GetValue(null);
        foreach (var sentry in handler?.GetInvocationList().Where(d => d.Method.DeclaringType?.Namespace?.StartsWith("Sentry", StringComparison.Ordinal) == true) ?? [])
            sentry.DynamicInvoke(AppDomain.CurrentDomain, new UnhandledExceptionEventArgs(exception, false));
    }

    private static void FaultAndForget(string text) => _ = Task.Run(() => throw Boom(text));

    private static void CollectTasks()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static DispatcherUnhandledExceptionEventArgs UiArgs(Exception exception)
    {
        var args = (DispatcherUnhandledExceptionEventArgs)Activator.CreateInstance(typeof(DispatcherUnhandledExceptionEventArgs), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [Dispatcher.UIThread], null)!;
        typeof(DispatcherUnhandledExceptionEventArgs).GetField("_exception", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(args, exception);
        return args;
    }

    private static void RaiseUi(Exception exception) =>
        typeof(CrashReporting).GetMethod("OnUiException", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [null, UiArgs(exception)]);

    [Fact]
    public void ReportingIsActiveWithTheKeyAndTheSettingOn()
    {
        Assert.True(CrashReporting.Active);
        Assert.Null(CrashReporting.InitFailure);
    }

    [Fact]
    public async Task AnUnhandledExceptionOnAnyThreadIsSentOnce()
    {
        var marker = Marker();
        RaiseAppDomainUnhandled(Boom("unhandled " + marker));
        Assert.Single(await Sent(marker));
    }

    [Fact]
    public async Task ATaskNobodyObservedIsSentOnce()
    {
        var marker = Marker();
        FaultAndForget("unobserved " + marker);
        for (var i = 0; i < 50 && (await Sent(marker)).Count == 0; i++)
        {
            CollectTasks();
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Single(await Sent(marker));
    }

    [Fact]
    public async Task AnUnhandledExceptionOnTheUiThreadIsSentOnceAndALockedResourceIsNot()
    {
        var marker = Marker();
        RaiseUi(Boom("ui " + marker));
        RaiseUi(new ResourceLockedException("locked " + marker));
        var sent = await Sent(marker);
        Assert.Single(sent);
        Assert.Contains("ui.dispatcher", sent[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pages-encode")]
    [InlineData("gml.symbols")]
    [InlineData("scan.decompile")]
    [InlineData("dead.scan")]
    [InlineData("texture.fit")]
    [InlineData("lint.check")]
    [InlineData("lint.parse")]
    public async Task AFailureAWorkerSurvivesIsSentOnceWithItsAreaAndTheVersions(string area)
    {
        var marker = Marker();
        FailureReport.Report(Boom("worker " + marker), area);
        var sent = await Sent(marker);
        var json = Assert.Single(sent);
        Assert.Contains(area, json, StringComparison.Ordinal);
        Assert.Contains("\"handled\":\"true\"", json, StringComparison.Ordinal);
        Assert.Contains("\"app-version\"", json, StringComparison.Ordinal);
        Assert.Contains("\"g3mlib.version\"", json, StringComparison.Ordinal);
        Assert.Contains("\"os\"", json, StringComparison.Ordinal);
        Assert.Contains("\"release\":\"g3mstudio@", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASaveThatDoesNotReadBackIsSentOnceWithTheFileSizeAndNoPath()
    {
        using var dir = new TempDir();
        var target = TestData.WriteSample(dir, objects: 3);
        using var data = TestData.Create(5);
        var pipeline = new SavePipeline(new UserData(dir.File("user")), 3) { Hook = (step, next) => { if (step == SaveStep.Written) File.WriteAllBytes(next, File.ReadAllBytes(next)[..200]); } };

        var result = await pipeline.SaveAsync(data, target, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SaveFailure.ValidationFailed, result.Failure);
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        var sent = _transport.Events("save-validate");
        var json = Assert.Single(sent);
        Assert.Contains("\"file.size\"", json, StringComparison.Ordinal);
        Assert.Contains("\"g3mlib.version\"", json, StringComparison.Ordinal);
        AssertNoPaths(json, dir.Path, "sample", Path.GetFileName(target));
    }

    [Fact]
    public async Task AProblemWithoutAnExceptionIsSentOnceAsAMessageWithItsTags()
    {
        var marker = Marker();
        FailureReport.Problem("save-validate", $"chunks GEN8 written, none read {marker}", new Dictionary<string, string> { ["file.size"] = "2-4MB" });

        var json = Assert.Single(await Sent(marker));

        Assert.Contains("\"area\":\"save-validate\"", json, StringComparison.Ordinal);
        Assert.Contains("\"file.size\":\"2-4MB\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileG3MLibCannotReadIsSentOnceWithItsSizeRangeAndNeitherPathNorName()
    {
        using var dir = new TempDir();
        var path = dir.File("Secret Game Name.win");
        await File.WriteAllBytesAsync(path, new byte[3 * 1048576], TestContext.Current.CancellationToken);
        var users = new UserData(dir.File("user"));
        var service = new WorkspaceService(users, RecentFiles.Load(users));

        await Assert.ThrowsAnyAsync<Exception>(() => service.OpenAsync(path, cancellationToken: TestContext.Current.CancellationToken));

        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        var json = Assert.Single(_transport.Events("g3mlib.read"));
        Assert.Contains("\"file.size\":\"2-4MB\"", json, StringComparison.Ordinal);
        AssertNoPaths(json, dir.Path, "Secret Game Name");
    }

    [Fact]
    public async Task AnErrorTheUserSawIsATrailForTheNextReportWithItsNamesLeftOut()
    {
        var marker = Marker();
        var center = new NotificationCenter();
        center.Post(NotificationLevel.Error, @"Could not save 'spr_hero' to C:\Users\Ann\Games\QuestZ9\data.win");
        center.Post(NotificationLevel.Info, "Saved snail_info");
        FailureReport.Report(Boom("after " + marker), "save");

        var json = Assert.Single(await Sent(marker));

        Assert.Contains("error shown", json, StringComparison.Ordinal);
        Assert.DoesNotContain("snail_info", json, StringComparison.Ordinal);
        AssertNoPaths(json, @"C:\Users\Ann", "spr_hero", "QuestZ9", "Could not save");
    }

    [Fact]
    public async Task ANameOfAGameResourceInATextIsHiddenAndTheRestStays()
    {
        var marker = Marker();
        FailureReport.Report(new FileNotFoundException("spr_hero_idle could not be read, Not enough memory for obj_boss"), "g3mlib.read", new Dictionary<string, string> { ["marker"] = marker });

        var json = Assert.Single(await Sent(marker));

        AssertNoPaths(json, "spr_hero_idle", "obj_boss");
        Assert.Contains("could not be read, Not enough memory for", json, StringComparison.Ordinal);
        Assert.Contains("FileNotFoundException", json, StringComparison.Ordinal);
        Assert.Contains("\"area\":\"g3mlib.read\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailureKeepsItsStackItsRedactedTextAndTheFactsAndLeaksNothingPersonal()
    {
        var marker = Marker();
        try
        {
            OuterStep($@"Index was out of range {marker}: cannot read 'obj_secret' at C:\Users\FakePerson\game\data.win for fake@example.org from 10.1.2.3 (sprite ""spr_hero"")");
        }
        catch (InvalidDataException e)
        {
            FailureReport.Report(e, "g3mlib.read");
        }

        CrashReporting.TabChanged("sprites");
        FailureReport.Trail("command", "file.save");
        FailureReport.Report(Boom("second " + marker), "save");

        var all = (await Sent(marker)).ToList();
        var json = all[0];
        var plain = Plain(json);
        Assert.Contains("InvalidDataException", json, StringComparison.Ordinal);
        Assert.Contains("InnerStep", json, StringComparison.Ordinal);
        Assert.Contains("OuterStep", json, StringComparison.Ordinal);
        Assert.Contains("Index was out of range", json, StringComparison.Ordinal);
        Assert.Contains("C:\\Users\\<user>", plain, StringComparison.Ordinal);
        Assert.Contains("<email>", plain, StringComparison.Ordinal);
        Assert.Contains("<ip>", plain, StringComparison.Ordinal);
        AssertNoPaths(json, "FakePerson", "obj_secret", "spr_hero", "fake@example.org", "10.1.2.3");
        foreach (var fact in new[] { "\"build\"", "\"runtime\"", "\"culture\"", "\"cpu-cores\"", "\"memory\"", "\"os-arch\"", "\"area\":\"g3mlib.read\"" })
            Assert.Contains(fact, json, StringComparison.Ordinal);
        var later = all[^1];
        Assert.Contains("\"tab\":\"sprites\"", later, StringComparison.Ordinal);
        Assert.Contains("file.save", later, StringComparison.Ordinal);
        foreach (var name in new[] { Environment.UserName, Environment.MachineName }.Where(n => n.Length >= 4)) Assert.False(Mentions(later, name), $"'{name}' leaked");
    }

    [Fact]
    public async Task TheListOfLoadedAssembliesCarriesNoInstallFolder()
    {
        var marker = Marker();
        FailureReport.Report(Boom(marker), "save");

        var json = Assert.Single(await Sent(marker));

        Assert.Contains("code_file", json, StringComparison.Ordinal);
        AssertNoPaths(json, AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static void OuterStep(string text) => InnerStep(text);

    private static void InnerStep(string text) => throw new InvalidDataException(text);

    [Fact]
    public async Task APathAndAQuotedNameInAnExceptionNeverLeave()
    {
        var marker = Marker();
        FailureReport.Report(new InvalidDataException($@"Index was out of range. {marker} Cannot read 'obj_secret' at C:\Users\Ann\Games\QuestZ9\data.win (sprite ""spr_hero"")"), "g3mlib.read");

        var json = Assert.Single(await Sent(marker));

        AssertNoPaths(json, "obj_secret", "spr_hero", "QuestZ9", @"Users\Ann");
        Assert.Contains("<name>", Plain(json), StringComparison.Ordinal);
        Assert.DoesNotContain("\"absPath\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingIsSentWhileTheSettingIsOff()
    {
        var marker = Marker();
        CrashReporting.Apply(false);
        Assert.False(CrashReporting.Active);

        RaiseAppDomainUnhandled(Boom("off unhandled " + marker));
        RaiseUi(Boom("off ui " + marker));
        FailureReport.Report(Boom("off handled " + marker), "save");
        FailureReport.Problem("save-validate", "off problem " + marker);
        new NotificationCenter().Post(NotificationLevel.Error, "off notice " + marker);
        FaultAndForget("off unobserved " + marker);
        CollectTasks();
        await Task.Delay(500, TestContext.Current.CancellationToken);
        CollectTasks();

        Assert.Empty(_transport.Events(marker));
        Assert.Null(FailureReport.Sink);
    }

    [Fact]
    public async Task ReportingStartsAgainWhenTheSettingComesBackOn()
    {
        CrashReporting.Apply(false);
        CrashReporting.Apply(true, Key, _transport);
        var marker = Marker();
        FailureReport.Report(Boom("again " + marker), "save");
        Assert.Single(await Sent(marker));
    }

    // The JSON writer escapes angle brackets and backslashes; this reads the text as a person would.
    private static string Plain(string json) => System.Text.RegularExpressions.Regex.Unescape(json);

    // A runner account called "runner" must not trip over the loaded module "xunit.v3.runner.inproc.console": a name inside a dotted identifier is not a path part.
    // The source paths in stack frames are those of the machine that built the tests (/home/runner/... on a runner called "runner"), not of the person using the app.
    internal static bool Mentions(string text, string name) => MentionContext(text, name) is not null;

    internal static string? MentionContext(string text, string name)
    {
        foreach (var key in new[] { "\"filename\":\"", "\"abs_path\":\"", "\"debug_file\":\"", "\"code_file\":\"" })
        {
            for (var at = text.IndexOf(key, StringComparison.Ordinal); at >= 0; at = text.IndexOf(key, at, StringComparison.Ordinal))
            {
                var start = at + key.Length;
                var end = text.IndexOf('"', start);
                text = end < 0 ? text[..start] : text.Remove(start, end - start);
                at = start;
            }
        }

        var match = System.Text.RegularExpressions.Regex.Match(text, $@"(?<![\w.]){System.Text.RegularExpressions.Regex.Escape(name)}(?!\w)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? text.Substring(Math.Max(0, match.Index - 80), Math.Min(200, text.Length - Math.Max(0, match.Index - 80))) : null;
    }

    private static void AssertNoPaths(string json, params string[] forbidden)
    {
        foreach (var text in forbidden)
            Assert.DoesNotContain(text, json.Replace(@"\\", @"\", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }
}
