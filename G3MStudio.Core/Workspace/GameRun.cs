using System.Diagnostics;

namespace G3MStudio.Core.Workspace;

/// <summary>What to start: the program, the data file it gets, and the options of the run. <c>{data}</c> in the arguments stands for the data file; without it the runner gets <c>-game</c> and the file first.</summary>
public sealed record RunSpec(string Runner, string DataFile, string Arguments = "", string WorkingDir = "", string Environment = "", bool DebugOutput = false);

/// <summary>A running game: its output lines as they come, and a way to stop it. Only the process Studio started is ever touched.</summary>
public sealed class GameRun : IDisposable
{
    private const int TailSize = 50;

    private readonly Process _process;
    private readonly Queue<string> _tail;

    private GameRun(Process process, string dataFile, Queue<string> tail)
    {
        _process = process;
        _tail = tail;
        DataFile = dataFile;
        Exited = WaitAsync();
    }

    public string DataFile { get; }

    /// <summary>True once a stop was asked for, so a kill is not taken for a crash.</summary>
    public bool StopRequested { get; private set; }

    /// <summary>Completes with the exit code once the process ended and its output was read to the end.</summary>
    public Task<int> Exited { get; }

    public bool IsRunning => !Exited.IsCompleted && !_process.HasExited;

    /// <summary>The last output lines, oldest first.</summary>
    public IReadOnlyList<string> Tail(int count = 10)
    {
        lock (_tail) return [.. _tail.Skip(Math.Max(0, _tail.Count - count))];
    }

    /// <summary>The runner next to the data file; null when it is not there. See <see cref="RunLocator"/>.</summary>
    public static string? FindRunner(string dataFile, string? gameFileName) => RunLocator.Find(dataFile, gameFileName);

    /// <summary>Starts the spec; <paramref name="line"/> gets each output line (true for the error stream).</summary>
    public static GameRun Start(RunSpec spec, Action<string, bool> line)
    {
        // The game reads the files next to its data file, so it starts in that folder unless the run says otherwise.
        var folder = spec.WorkingDir.Length > 0 ? spec.WorkingDir : Path.GetDirectoryName(Path.GetFullPath(spec.DataFile)) ?? ".";
        var info = new ProcessStartInfo(spec.Runner) { WorkingDirectory = folder };
        var extra = SplitArguments(spec.Arguments);
        if (!extra.Any(a => a.Contains("{data}", StringComparison.Ordinal)))
        {
            info.ArgumentList.Add("-game");
            info.ArgumentList.Add(spec.DataFile);
        }

        if (spec.DebugOutput)
        {
            info.ArgumentList.Add("-debugoutput");
            info.ArgumentList.Add(spec.DataFile + ".gamelog.txt");
        }

        foreach (var argument in extra) info.ArgumentList.Add(argument.Replace("{data}", spec.DataFile, StringComparison.Ordinal));
        foreach (var (name, value) in ParseEnvironment(spec.Environment)) info.Environment[name] = value;
        return Launch(info, spec.DataFile, line);
    }

    /// <summary>Splits a command line the way a shell does for plain cases: spaces separate, double quotes group.</summary>
    public static List<string> SplitArguments(string text)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var any = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                any = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (any) result.Add(current.ToString());
                current.Clear();
                any = false;
            }
            else
            {
                current.Append(c);
                any = true;
            }
        }

        if (any) result.Add(current.ToString());
        return result;
    }

    /// <summary><c>NAME=value</c> pairs separated by semicolons or line breaks; a part without a name or an equals sign is skipped.</summary>
    public static List<(string Name, string Value)> ParseEnvironment(string text) =>
        [.. text.Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[0].Trim().Length > 0)
            .Select(pair => (pair[0].Trim(), pair[1]))];

    private static GameRun Launch(ProcessStartInfo info, string dataFile, Action<string, bool> line)
    {
        info.RedirectStandardOutput = info.RedirectStandardError = true;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var tail = new Queue<string>();
        void Got(string text, bool error)
        {
            lock (tail)
            {
                tail.Enqueue(text);
                while (tail.Count > TailSize) tail.Dequeue();
            }

            line(text, error);
        }

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Got(e.Data, false); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Got(e.Data, true); };
        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new GameRun(process, dataFile, tail);
    }

    /// <summary>Kills the process and the ones it started.</summary>
    public void Stop()
    {
        StopRequested = true;
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // it ended between the check and the kill
        }
    }

    /// <summary>Asks the game to close (the window on Windows, SIGTERM elsewhere) and kills it when it is still there after <paramref name="grace"/>.</summary>
    public async Task StopAsync(TimeSpan grace)
    {
        if (!IsRunning) return;
        StopRequested = true;
        try
        {
            if (OperatingSystem.IsWindows()) _process.CloseMainWindow();
            else using (Process.Start(new ProcessStartInfo("kill") { ArgumentList = { "-TERM", _process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) }, UseShellExecute = false })) { }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // no window to close or no kill program: the kill below stops it
        }

        if (await Task.WhenAny(Exited, Task.Delay(grace)) != Exited) Stop();
        await Exited;
    }

    private async Task<int> WaitAsync()
    {
        await _process.WaitForExitAsync();
        return _process.ExitCode;
    }

    public void Dispose()
    {
        Stop();
        _process.Dispose();
    }
}
