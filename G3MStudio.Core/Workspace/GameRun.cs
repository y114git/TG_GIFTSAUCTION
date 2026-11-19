using System.Diagnostics;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// What to start: the program, the data file it gets, and the options of the run. <c>{data}</c> in the arguments stands for the data file; without it the runner gets <c>-game</c> and the file first.
/// <paramref name="Compat"/> says the program is a compatibility tool that starts a Windows game; <paramref name="Notes"/> are lines for the run log before the start.
/// </summary>
public sealed record RunSpec(string Runner, string DataFile, string Arguments = "", string WorkingDir = "", string Environment = "", bool DebugOutput = false, CompatLaunch? Compat = null, IReadOnlyList<string>? Notes = null);

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
    public static GameRun Start(RunSpec spec, Action<string, bool> line) => Launch(Prepare(spec), spec.DataFile, line);

    /// <summary>The program, arguments, folder and environment of a run, ready to start.</summary>
    public static ProcessStartInfo Prepare(RunSpec spec)
    {
        // The game reads the files next to its data file, so it starts in that folder unless the run says otherwise.
        var folder = spec.WorkingDir.Length > 0 ? spec.WorkingDir : Path.GetDirectoryName(Path.GetFullPath(spec.DataFile)) ?? ".";
        var info = new ProcessStartInfo(spec.Runner) { WorkingDirectory = folder };
        var extra = SplitArguments(spec.Arguments);
        // A Windows game under a compatibility tool gets its files as Windows paths.
        var compat = spec.Compat;
        string Shown(string path) => compat is null || !CompatRun.IsExe(compat.Exe) ? path : CompatRun.WinePath(path, folder);
        var data = Shown(spec.DataFile);
        var game = new List<string>();
        if (!extra.Any(a => a.Contains("{data}", StringComparison.Ordinal)))
        {
            game.Add("-game");
            game.Add(data);
        }

        if (spec.DebugOutput)
        {
            game.Add("-debugoutput");
            game.Add(Shown(spec.DataFile + ".gamelog.txt"));
        }

        game.AddRange(extra.Select(a => a.Replace("{data}", data, StringComparison.Ordinal)));
        if (compat is null) game.ForEach(info.ArgumentList.Add);
        else if (!compat.InlineArgs) foreach (var a in compat.Leading.Concat(game)) info.ArgumentList.Add(a);
        else
        {
            // The token alone stands for the arguments as separate ones; inside a larger token (a shell string) each is quoted so a path with spaces stays whole.
            // A command with neither {args} nor {data} gets the arguments at its end; one that places {data} itself decides what the game opens.
            var placed = compat.Leading.Any(p => p.Contains("{args}", StringComparison.Ordinal) || p.Contains("{data}", StringComparison.Ordinal));
            foreach (var part in compat.Leading.Select(p => p.Replace("{data}", data, StringComparison.Ordinal)))
            {
                if (part == "{args}") game.ForEach(info.ArgumentList.Add);
                else info.ArgumentList.Add(part.Replace("{args}", JoinArguments(game), StringComparison.Ordinal));
            }

            if (!placed) game.ForEach(info.ArgumentList.Add);
        }

        if (compat is not null) foreach (var (name, value) in compat.Environment) info.Environment[name] = value;
        foreach (var (name, value) in ParseEnvironment(spec.Environment)) info.Environment[name] = value; // the user's environment comes last, so it can override anything
        return info;
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

    /// <summary>The inverse of <see cref="SplitArguments"/>: spaces separate, and an argument with white space or none at all is put in double quotes.</summary>
    public static string JoinArguments(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(a => a.Length == 0 || a.Any(char.IsWhiteSpace) ? $"\"{a}\"" : a));

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
        // A launcher script that ended while its game lives on keeps the output pipe open, so the whole tree gets the signals, not only the process Studio started.
        var tree = OperatingSystem.IsWindows() ? [] : Tree();
        try
        {
            if (OperatingSystem.IsWindows()) _process.CloseMainWindow();
            else Signal("-TERM", tree);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // no window to close or no kill program: the kill below stops it
        }

        using var wait = new CancellationTokenSource();
        var delay = Task.Delay(grace, wait.Token);
        if (await Task.WhenAny(Exited, delay) != Exited)
        {
            if (!OperatingSystem.IsWindows()) Signal("-KILL", tree);
            Stop();
        }

        await wait.CancelAsync(); // the timer must not run on after the game ended
        await Exited;
    }

    /// <summary>The process and everything it started (Linux and macOS), read from one <c>ps</c> listing.</summary>
    private List<int> Tree()
    {
        var pids = new List<int> { _process.Id };
        try
        {
            using var ps = Process.Start(new ProcessStartInfo("ps") { ArgumentList = { "-A", "-o", "pid=,ppid=" }, RedirectStandardOutput = true, UseShellExecute = false });
            if (ps is null) return pids;
            var rows = new List<(int Pid, int Parent)>();
            foreach (var line in ps.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var part = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (part.Length == 2 && int.TryParse(part[0], out var pid) && int.TryParse(part[1], out var parent)) rows.Add((pid, parent));
            }

            var queue = new Queue<int>(pids);
            while (queue.TryDequeue(out var parent))
            {
                foreach (var (pid, _) in rows.Where(r => r.Parent == parent))
                {
                    pids.Add(pid);
                    queue.Enqueue(pid);
                }
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // without ps only the process itself is signalled
        }

        return pids;
    }

    private static void Signal(string signal, List<int> pids)
    {
        var info = new ProcessStartInfo("kill") { UseShellExecute = false };
        info.ArgumentList.Add(signal);
        foreach (var pid in pids) info.ArgumentList.Add(pid.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using (Process.Start(info)) { }
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
