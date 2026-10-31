using System.Diagnostics;

namespace G3MStudio.Core.Workspace;

/// <summary>A running game: its output lines as they come, and a way to stop it. The runner gets the temporary data file with <c>-game</c>.</summary>
public sealed class GameRun : IDisposable
{
    private readonly Process _process;

    private GameRun(Process process, string tempFile)
    {
        _process = process;
        TempFile = tempFile;
        Exited = WaitAsync();
    }

    public string TempFile { get; }

    /// <summary>Completes with the exit code once the process ended and its output was read to the end.</summary>
    public Task<int> Exited { get; }

    public bool IsRunning => !_process.HasExited;

    /// <summary>The runner next to the data file: the name in the general info plus .exe on Windows. Null when the file is not there.</summary>
    public static string? FindRunner(string dataFile, string? gameFileName)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(dataFile)) ?? ".";
        var name = string.IsNullOrWhiteSpace(gameFileName) ? Path.GetFileNameWithoutExtension(dataFile) : Path.GetFileNameWithoutExtension(gameFileName);
        foreach (var candidate in new[] { name + ".exe", name })
        {
            var path = Path.Combine(folder, candidate);
            if (File.Exists(path)) return path;
        }

        return null;
    }

    /// <summary>Starts the runner on <paramref name="tempFile"/>; <paramref name="line"/> gets each output line (true for the error stream).</summary>
    public static GameRun Start(string runner, string tempFile, Action<string, bool> line, string extraArguments = "", bool debugOutput = false)
    {
        // The game reads the files next to its data file, so it starts in that folder whichever runner is used.
        var info = new ProcessStartInfo(runner) { WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(tempFile)) ?? "." };
        info.ArgumentList.Add("-game");
        info.ArgumentList.Add(tempFile);
        if (debugOutput)
        {
            info.ArgumentList.Add("-debugoutput");
            info.ArgumentList.Add(tempFile + ".gamelog.txt");
        }

        foreach (var argument in SplitArguments(extraArguments)) info.ArgumentList.Add(argument);
        return Launch(info, tempFile, line);
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

    /// <summary>Starts any program with its own arguments the same way; the tests use it.</summary>
    internal static GameRun StartRaw(string program, IEnumerable<string> arguments, Action<string, bool> line)
    {
        var info = new ProcessStartInfo(program);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Launch(info, string.Empty, line);
    }

    private static GameRun Launch(ProcessStartInfo info, string tempFile, Action<string, bool> line)
    {
        info.RedirectStandardOutput = info.RedirectStandardError = true;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) line(e.Data, false); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) line(e.Data, true); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new GameRun(process, tempFile);
    }

    public void Stop()
    {
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
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
