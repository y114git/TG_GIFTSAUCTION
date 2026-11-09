namespace G3MStudio.Core.Workspace;

public enum RunnerCheck { Known, Unknown, Missing }

/// <summary>Finds the game executable of a data file on Windows, Linux and macOS, and tells a known runner from any other program.</summary>
public static class RunLocator
{
    /// <summary>The runner for a data file: next to it, one folder up (Steam keeps game.unx in assets/), or in the macOS bundle's MacOS folder. Null when none is there.</summary>
    public static string? Find(string dataFile, string? gameFileName)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(dataFile)) ?? ".";
        var names = Names(dataFile, gameFileName);
        var parent = Directory.GetParent(folder)?.FullName;
        string?[] folders = [folder, parent, parent is null ? null : Path.Combine(parent, "MacOS")];
        foreach (var where in folders.OfType<string>())
        {
            foreach (var name in names.Where(n => OperatingSystem.IsWindows() || !n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                var path = Path.GetFullPath(Path.Combine(where, name));
                if (File.Exists(path)) return path;
            }
        }

        return null;
    }

    /// <summary>Is the program a runner Studio knows (the game name, or a file called runner)? Anything else is allowed with a warning.</summary>
    public static RunnerCheck Check(string runner, string dataFile, string? gameFileName, bool fromProject = false)
    {
        var resolved = Resolve(runner, fromProject ? dataFile : null);
        if (resolved is null) return RunnerCheck.Missing;
        // A relative path from a global setting that lands inside the data folder could be a planted program: a matching name proves nothing.
        if (!fromProject && !Path.IsPathRooted(runner) && runner.IndexOfAny(['/', '\\']) >= 0 && IsInside(Path.GetDirectoryName(Path.GetFullPath(dataFile)), resolved)) return RunnerCheck.Unknown;
        var name = Path.GetFileName(resolved);
        return Names(dataFile, gameFileName).Contains(name, StringComparer.OrdinalIgnoreCase) || name.Contains("runner", StringComparison.OrdinalIgnoreCase)
            ? RunnerCheck.Known
            : RunnerCheck.Unknown;
    }

    private static bool IsInside(string? folder, string path) =>
        folder is not null && path.StartsWith(folder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>A runner that <see cref="Find"/> scanned for: Known only when it is the game file in the folder of the data file, else Unknown (a generic "runner" file one folder up could be anything).</summary>
    public static RunnerCheck CheckFound(string runner, string dataFile, string? gameFileName)
    {
        if (!File.Exists(runner)) return RunnerCheck.Missing;
        var folder = Path.GetDirectoryName(Path.GetFullPath(dataFile));
        var sameFolder = string.Equals(Path.GetDirectoryName(Path.GetFullPath(runner)), folder, StringComparison.OrdinalIgnoreCase);
        return sameFolder && GameNames(dataFile, gameFileName).Contains(Path.GetFileName(runner), StringComparer.OrdinalIgnoreCase) ? RunnerCheck.Known : RunnerCheck.Unknown;
    }

    /// <summary>The full path of a program given by path or by a bare name looked up on PATH; null when it is nowhere. A relative path with a separator is taken from the folder of <paramref name="dataFile"/>; without a data file, from the current directory.</summary>
    public static string? Resolve(string runner, string? dataFile = null)
    {
        if (string.IsNullOrWhiteSpace(runner)) return null;
        if (runner.IndexOfAny(['/', '\\']) >= 0 || Path.IsPathRooted(runner))
        {
            var full = Path.IsPathRooted(runner) || dataFile is null ? Path.GetFullPath(runner) : Path.GetFullPath(runner, Path.GetDirectoryName(Path.GetFullPath(dataFile)) ?? ".");
            return File.Exists(full) ? full : null;
        }

        var extensions = OperatingSystem.IsWindows() ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';').Prepend("") : [""];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                try
                {
                    var path = Path.Combine(dir, runner + extension);
                    if (File.Exists(path)) return path;
                }
                catch (ArgumentException)
                {
                    // a malformed PATH entry
                }
            }
        }

        return null;
    }

    private static List<string> GameNames(string dataFile, string? gameFileName)
    {
        var game = string.IsNullOrWhiteSpace(gameFileName) ? Path.GetFileNameWithoutExtension(dataFile) : Path.GetFileNameWithoutExtension(gameFileName);
        return [game + ".exe", game, game + ".x86_64", game + ".AppImage"];
    }

    // Windows: Game.exe. Linux: the runner file or Game.x86_64. macOS: Mac_Runner inside the bundle.
    private static List<string> Names(string dataFile, string? gameFileName) =>
        [.. GameNames(dataFile, gameFileName), "runner", "Runner", "runner.exe", "Runner.exe", "Mac_Runner", "game.x86_64"];
}
