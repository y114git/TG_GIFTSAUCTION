using System.Globalization;
using G3MStudio.Core.Settings;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace G3MStudio;

/// <summary>
/// <c>G3MStudio.exe --sentry-test result.txt</c>: sends one test crash, one marked transaction, one log line and one session through the app's own reporting path with the key
/// built into this exe, writes the answer of the Sentry server to the file and exits. It shows at once whether a build reports at all, and nothing but made-up facts leaves the machine.
/// </summary>
internal static class SentryCheck
{
    private sealed class Forward(Uri url, string auth) : ITransport
    {
        public List<string> Answers { get; } = [];

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(stream.ToArray()) };
            request.Headers.Add("X-Sentry-Auth", auth);
            using var response = await http.SendAsync(request, cancellationToken);
            Answers.Add(((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
        }
    }

    public static int Run(string resultFile)
    {
        if (CrashReporting.Dsn is not { } dsn)
        {
            File.WriteAllText(resultFile, "No crash report key is built into this exe, so it never sends crash reports. Build it with -p:SentryFromEnv=true or -p:SentryDsn=<key>.\n");
            return 2;
        }

        var uri = new Uri(dsn);
        var path = uri.AbsolutePath.Trim('/');
        var cut = path.LastIndexOf('/'); // the last segment is the project, what precedes it is the host's own prefix
        var url = new Uri($"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : ":" + uri.Port)}/{(cut < 0 ? "" : path[..(cut + 1)])}api/{path[(cut + 1)..]}/envelope/");
        var forward = new Forward(url, $"Sentry sentry_version=7, sentry_client=g3mstudio-check/1, sentry_key={uri.UserInfo.Split(':')[0]}");
        CrashReporting.Apply(true, dsn, forward, installId: FailureReport.NewInstallId());
        var marker = "live-check-" + Guid.NewGuid().ToString("N")[..8];
        CrashReporting.TabChanged("sprites");
        FailureReport.Trail("command", "file.save");
        try
        {
            throw new InvalidDataException("Cannot read spr_fake from C:/Users/FakePerson/game/data.win for fake@example.org " + marker);
        }
        catch (InvalidDataException e)
        {
            FailureReport.Report(e, "g3mlib.read", FailureReport.FileTags("C:/Users/FakePerson/game/data.win"));
        }

        using (var span = Telemetry.Measure("sentry.check", "check")) span.Tag("marker", marker).Value("check", 1);
        Telemetry.Log(SentryLogLevel.Info, "sentry check " + marker);
        SentrySdk.Flush(TimeSpan.FromSeconds(15));
        CrashReporting.Apply(false); // ends the session, which goes out as the last envelope
        var ok = forward.Answers.Count > 0 && forward.Answers.All(a => a == "200");
        var answer = forward.Answers.Count == 0 ? "none (no connection?)" : string.Join(", ", forward.Answers);
        File.WriteAllText(resultFile, $"{marker}\nServer answer: {answer}\n{(ok ? "Crash, transaction, log and session arrived. Search the Sentry dashboard for the text above." : "The test data did NOT arrive.")}\nInit failure: {CrashReporting.InitFailure?.Message ?? "none"}\n");
        return ok ? 0 : 1;
    }
}
