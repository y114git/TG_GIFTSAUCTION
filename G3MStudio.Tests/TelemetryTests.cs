using System.Collections.Concurrent;
using System.Text;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace G3MStudio.Tests;

/// <summary>
/// Every channel beyond errors (transactions, logs, feedback, attachments, sessions, the usage summary), with the sending replaced by a transport that keeps the serialized
/// envelopes in memory. Whatever sensitive text goes in, none of it may come out, and nothing is sent while a switch is off.
/// </summary>
[Collection(CrashReportingGroup.Name)]
public sealed class TelemetryTests : IDisposable
{
    private const string Key = "https://0123456789abcdef0123456789abcdef@localhost:9/1";
    private const string Path = "C:/Users/Someone/Game/data.win";
    private const string Mail = "someone@example.org";
    private const string Ip = "203.0.113.77";
    private const string Name = "spr_hero";

    private sealed class MemoryTransport : ITransport
    {
        public ConcurrentQueue<string> Envelopes { get; } = new();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            Envelopes.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
        }

        public string All => string.Join("\n", Envelopes);
    }

    private readonly MemoryTransport _transport = new();

    public void Dispose() => CrashReporting.Apply(false);

    private static readonly string[] Secrets = [Path, "Someone", Mail, Ip, Name, "Game/data"];

    private async Task<string> Flushed()
    {
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        return _transport.All;
    }

    private static void AssertClean(string sent)
    {
        foreach (var secret in Secrets) Assert.DoesNotContain(secret, sent, StringComparison.OrdinalIgnoreCase);
        foreach (var own in new[] { Environment.UserName, Environment.MachineName }.Where(n => n.Length >= 3))
        {
            Assert.True(CrashCaptureTests.MentionContext(sent, own) is not { } near, $"'{own}' leaked near: {CrashCaptureTests.MentionContext(sent, own)}");
        }
    }

    private void Start(bool usage = true, string? id = null) => CrashReporting.Apply(true, Key, _transport, usage, id ?? FailureReport.NewInstallId());

    [Fact]
    public void NothingIsSentBeforeTheStartPageWasClosed()
    {
        var first = new AppSettings();
        Assert.True(first.CrashReports);
        Assert.False(CrashReporting.ShouldReport(first));
        CrashReporting.Apply(CrashReporting.ShouldReport(first), Key, _transport, first.UsageStats);
        Assert.False(CrashReporting.Active);
        FailureReport.Problem("test", "before the start page");
        Assert.Empty(_transport.Envelopes);

        var done = first with { OnboardingDone = true };
        CrashReporting.Apply(CrashReporting.ShouldReport(done), Key, _transport, done.UsageStats);
        Assert.True(CrashReporting.Active);
        Assert.False(CrashReporting.ShouldReport(done with { CrashReports = false }));
    }

    [Theory]
    [InlineData("1.2.0", false, "release")]
    [InlineData("1.2.0-beta.3", false, "beta")]
    [InlineData("1.2.0-beta.3", true, "dev")]
    public void TheEnvironmentFollowsTheVersionAndTheBuild(string version, bool debug, string expected) => Assert.Equal(expected, CrashReporting.EnvironmentFor(version, debug));

    [Fact]
    public async Task AnEventCarriesTheInstallIdAndTheEnvironment()
    {
        var id = FailureReport.NewInstallId();
        Assert.True(FailureReport.IsInstallId(id));
        Assert.NotEqual(id, FailureReport.NewInstallId());
        Assert.False(FailureReport.IsInstallId("C:/Users/Someone"));
        Start(true, id);
        FailureReport.Problem("test", "with an id");
        var sent = await Flushed();
        Assert.Contains(id, sent, StringComparison.Ordinal);
        Assert.Contains("\"environment\":", sent, StringComparison.Ordinal);

        var reset = FailureReport.NewInstallId();
        CrashReporting.SetInstallId(reset);
        Assert.Equal(reset, CrashReporting.InstallId);
        FailureReport.Problem("test", "after the reset");
        Assert.Contains(reset, await Flushed(), StringComparison.Ordinal);
    }

    [Fact]
    public void ASettingsFileFromBeforeTheNewFieldsLoadsWithDefaults()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("user"));
        File.WriteAllText(data.Combine("settings.json"), "{ \"crashReports\": false, \"language\": \"ru\" }");
        using var store = new SettingsStore(data);
        Assert.False(store.Current.CrashReports);
        Assert.True(store.Current.UsageStats);
        Assert.Equal("", store.Current.InstallId);
        Assert.Equal("ru", store.Current.Language);
    }

    [Fact]
    public void ASettingsFileThatFinishedTheWelcomeBeforeUsageStatsExistedIsNotOptedIn()
    {
        using var dir = new TempDir();
        var data = new UserData(dir.File("user"));
        File.WriteAllText(data.Combine("settings.json"), "{ \"onboardingDone\": true, \"crashReports\": true }");
        using (var store = new SettingsStore(data)) Assert.False(store.Current.UsageStats);

        File.WriteAllText(data.Combine("settings.json"), "{ \"onboardingDone\": true, \"usageStats\": true }");
        using (var store = new SettingsStore(data)) Assert.True(store.Current.UsageStats);
    }

    [Fact]
    public void TheAboutTextShowsTheInstallId()
    {
        var id = FailureReport.NewInstallId();
        Assert.Contains("Install ID: " + id, AboutWindow.VersionInfo(id), StringComparison.Ordinal);
        Assert.DoesNotContain("Install ID", AboutWindow.VersionInfo(), StringComparison.Ordinal);
        Assert.Contains("Install ID: " + id, CrashReporting.DiagnosticsText(id, "no-such-folder"), StringComparison.Ordinal);
    }

    [Fact]
    public void ASessionIsSentAndEndsAsExited()
    {
        Start();
        CrashReporting.Shutdown();
        var sent = _transport.All;
        Assert.Contains("\"type\":\"session\"", sent, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"exited\"", sent, StringComparison.Ordinal);
        AssertClean(sent);
    }

    [Fact]
    public async Task SwitchingReportingOffSendsNothingMore()
    {
        Start();
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
        var before = _transport.Envelopes.Count;
        CrashReporting.Apply(false);
        Assert.Equal(before, _transport.Envelopes.Count);
        Assert.DoesNotContain("\"status\":\"exited\"", _transport.All, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppStartedIsLoggedOncePerTimeUsageGoesOn()
    {
        Start();
        CrashReporting.Apply(true, Key, _transport, true);
        Start(usage: false);
        Start();
        var sent = await Flushed();
        Assert.Equal(2, sent.Split("app started").Length - 1);
    }

    [Fact]
    public async Task TransactionsKeepTheirMeasurementsAndLoseEveryName()
    {
        Start();
        using (var span = Telemetry.Measure("file.open", "file"))
            span.Tag("editor", Path).Tag("file", Mail + " " + Ip + " " + Name).Value("parse_ms", 12, MeasurementUnit.Duration.Millisecond).Value("rooms", 3);
        var sent = await Flushed();
        Assert.Contains("\"type\":\"transaction\"", sent, StringComparison.Ordinal);
        Assert.Contains("parse_ms", sent, StringComparison.Ordinal);
        Assert.Contains("\"rooms\"", sent, StringComparison.Ordinal);
        AssertClean(sent);
    }

    [Fact]
    public async Task LogsAreCleanedWhereTheyAreMade()
    {
        Start();
        Telemetry.Log(SentryLogLevel.Info, "opened " + Path, ("file", Path), ("mail", Mail), ("ip", Ip), ("name", Name));
        Telemetry.Log(SentryLogLevel.Warning, "ui hang", ("state", "spinning"));
        var sent = await Flushed();
        Assert.Contains("\"type\":\"log\"", sent, StringComparison.Ordinal);
        Assert.Contains("ui hang", sent, StringComparison.Ordinal);
        AssertClean(sent);
    }

    [Fact]
    public async Task TheHangFileIsAttachedAsCleanedText()
    {
        Start();
        var text = $"The UI thread did not answer for 11 s.\nat Foo.Bar() in {Path}\nmail {Mail} from {Ip} for '{Name}'\n" + new string('x', 40000);
        FailureReport.ProblemWithText("ui.hang", "hang", new Dictionary<string, string> { ["hang"] = "spinning" }, "hang.txt", text);
        var sent = await Flushed();
        Assert.Contains("\"type\":\"attachment\"", sent, StringComparison.Ordinal);
        Assert.Contains("The UI thread did not answer", sent, StringComparison.Ordinal);
        Assert.Contains("hang.txt", sent, StringComparison.Ordinal);
        AssertClean(sent);
        Assert.True(CrashReporting.AttachmentText(text).Length <= 16000);
    }

    [Fact]
    public async Task BreadcrumbsAndTagsOfAnErrorAreCleaned()
    {
        Start();
        FailureReport.Trail("command", "open " + Path + " " + Mail);
        FailureReport.Problem("test", "failed in " + Path + " for " + Name + " " + Ip, new Dictionary<string, string> { ["file"] = Path });
        AssertClean(await Flushed());
    }

    [Fact]
    public async Task FeedbackIsCleanedAndTheContactGoesOnlyWhenGiven()
    {
        Start();
        var message = $"It broke in {Path} on {Name}, write to {Mail} or {Ip}";
        Assert.Equal(CaptureFeedbackResult.Success, CrashReporting.SendFeedback(message, null, true));
        var sent = await Flushed();
        Assert.Contains("\"type\":\"feedback\"", sent, StringComparison.Ordinal);
        Assert.Contains("It broke in", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("contact_email", sent, StringComparison.Ordinal);
        AssertClean(sent);

        Assert.Equal(CaptureFeedbackResult.Success, CrashReporting.SendFeedback("second message", "me@contact.example", false));
        sent = await Flushed();
        Assert.Contains("me@contact.example", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FeedbackWithoutDiagnosticsCarriesNoSystemTags()
    {
        var id = FailureReport.NewInstallId();
        Start(id: id);
        Assert.Equal(CaptureFeedbackResult.Success, CrashReporting.SendFeedback("plain note", null, false));
        var sent = await Flushed();
        Assert.Contains("plain note", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("os-arch", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("cpu-cores", sent, StringComparison.Ordinal);
        Assert.DoesNotContain(id, _transport.Envelopes.Single(e => e.Contains("\"type\":\"feedback\"", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingAtAllIsSentWhileReportingIsOff()
    {
        CrashReporting.Apply(false, Key, _transport);
        using (var span = Telemetry.Measure("file.save", "file")) span.Tag("x", "y");
        Telemetry.Log(SentryLogLevel.Info, "hello");
        Telemetry.Command("file.save");
        Telemetry.SendSummary();
        FailureReport.Problem("test", "off");
        Assert.Equal(CaptureFeedbackResult.DisabledHub, CrashReporting.SendFeedback("off", null, true));
        Assert.Empty(_transport.Envelopes);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task WithOnlyUsageStatsOffErrorsAndSessionsGoOutButNothingElse()
    {
        Start(usage: false);
        using (var span = Telemetry.Measure("file.save", "file")) span.Tag("x", "y");
        Telemetry.Log(SentryLogLevel.Info, "a log line");
        Telemetry.Command("file.save");
        Telemetry.SendSummary();
        FailureReport.Problem("test", "an error still goes");
        var sent = await Flushed();
        Assert.Contains("an error still goes", sent, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"session\"", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\":\"transaction\"", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\":\"log\"", sent, StringComparison.Ordinal);
        Assert.False(Telemetry.Measure("x", "y") is { } span2 && CrashReporting.UsageOn && span2 is null);
    }

    [Fact]
    public async Task TheSummaryCountsOnlyTheAppsOwnCommandsAndSettingsFacts()
    {
        Start();
        // The counters are static: what an earlier test or a window still alive from one counted before this point is sent and dropped first.
        Telemetry.SendSummary();
        await Flushed();
        _transport.Envelopes.Clear();
        Telemetry.KnowCommands(["file.save", "edit.undo"]);
        Telemetry.Command("file.save");
        Telemetry.Command("file.save");
        Telemetry.Command("plugin." + Name);
        Telemetry.Editor("sprites");
        Telemetry.Editor(Path);
        Telemetry.FileOpened();
        Telemetry.Note("language", "ru");
        Telemetry.Note("secret-key", Path);
        Telemetry.SendSummary();
        var sent = await Flushed();
        Assert.Contains("file.save=2", sent, StringComparison.Ordinal);
        Assert.Contains("sprites=1", sent, StringComparison.Ordinal);
        Assert.Contains("language", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", sent, StringComparison.Ordinal);
        AssertClean(sent);
    }

    [Fact]
    public void EveryNewCommandHasATitleInTheMenu()
    {
        foreach (var id in new[] { "help.sendFeedback", "help.reportProblem", "help.openLogsFolder", "help.copyDiagnostics" })
            Assert.Contains(AppCommands.Menus.SelectMany(m => m.Ids), x => x == id);
    }
}
