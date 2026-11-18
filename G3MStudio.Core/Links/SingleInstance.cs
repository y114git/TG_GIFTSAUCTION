using System.IO.Pipes;
using System.Text;

namespace G3MStudio.Core.Links;

/// <summary>
/// One running G3MStudio per user takes the files and links that later launches are given: a launch tries to hand its argument to the running program
/// through a named pipe and exits when that works, and the running program listens on the pipe. The pipe name carries the user name, and a pipe
/// is reachable only by the same user.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;

    /// <summary>A short name for the user: macOS limits the path of a domain socket to about 100 characters, and the temporary folder there is already long.</summary>
    public static string DefaultName => "g3ms-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + "|" + Environment.UserDomainName)))[..12];

    /// <summary>Sends the text to the running program. False when none answers within the time.</summary>
    public static bool TrySend(string text, string? name = null, int milliseconds = 500)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", name ?? DefaultName, PipeDirection.Out, PipeOptions.CurrentUserOnly); // only a server of the same user is accepted, so another user cannot take the name and read what is sent
            client.Connect(milliseconds);
            var bytes = Encoding.UTF8.GetBytes(text);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// Starts listening; each text that arrives is given to <paramref name="received"/> on a pool thread. Returns false when another program already
    /// listens under the name (then this one is not the first instance).
    /// </summary>
    public bool Listen(Action<string> received, string? name = null)
    {
        name ??= DefaultName;
#pragma warning disable CA2000 // the pipe belongs to the loop below, which disposes it
        var first = Create(name);
#pragma warning restore CA2000
        if (first is null) return false;
        _loop = Task.Run(async () =>
        {
            var server = first;
            while (!_stop.IsCancellationRequested && server is not null)
            {
                try
                {
                    await server.WaitForConnectionAsync(_stop.Token);
                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                    var text = await reader.ReadToEndAsync(_stop.Token);
                    if (text.Length is > 0 and < 32_768) received(text);
                }
                catch (OperationCanceledException)
                {
                    // Stopped: the loop ends below.
                }
                catch (IOException)
                {
                    // The sender went away before it finished: wait for the next one.
                }

                await server.DisposeAsync();
                server = _stop.IsCancellationRequested ? null : Create(name);
            }
        });
        return true;
    }

    /// <summary>A pipe that only this user can reach, or null when another program already owns the name.</summary>
    private static NamedPipeServerStream? Create(string name)
    {
        try
        {
            return new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _loop?.Wait(1000);
        }
        catch (AggregateException)
        {
            // The loop ended by cancellation.
        }

        _stop.Dispose();
    }
}
