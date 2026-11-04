namespace G3MStudio.Core.Workspace;

/// <summary>A GameMaker runtime found on disk: its version text and the path of its Runner.exe.</summary>
public sealed record RuntimeInfo(string Version, string Runner);

/// <summary>Finds the GameMaker runtimes that can play a data file (UTMT-RUN-03). Each folder holds <c>runtime-VERSION</c> folders with the runner in <c>windows/x64</c> or <c>windows</c>.</summary>
public static class RuntimeFinder
{
    private static readonly string[] Editions = ["GameMakerStudio2", "GameMakerStudio2-LTS", "GameMakerStudio2-Beta"];

    /// <summary>The folders GameMaker keeps its runtimes in on this machine.</summary>
    public static IReadOnlyList<string> DefaultFolders()
    {
        var data = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return data.Length == 0 ? [] : Editions.Select(n => Path.Combine(data, n, "Cache", "runtimes")).ToList();
    }

    /// <summary>The runner of GameMaker Studio 1.4 (one Runner.exe in its install folder), or null when it is not installed.</summary>
    public static RuntimeInfo? Gms1(string? installFolder = null)
    {
        installFolder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GameMaker Studio");
        var runner = Path.Combine(installFolder, "Runner.exe");
        return File.Exists(runner) ? new RuntimeInfo("1.4", runner) : null;
    }

    /// <summary>The runtimes under the folders, newest version first.</summary>
    public static IReadOnlyList<RuntimeInfo> Find(IEnumerable<string> folders)
    {
        var found = new List<RuntimeInfo>();
        foreach (var folder in folders.Where(f => f.Length > 0 && Directory.Exists(f)))
        {
            foreach (var runtime in Directory.EnumerateDirectories(folder, "runtime-*"))
            {
                var runner = new[] { Path.Combine(runtime, "windows", "x64", "Runner.exe"), Path.Combine(runtime, "windows", "Runner.exe") }.FirstOrDefault(File.Exists);
                if (runner is not null) found.Add(new RuntimeInfo(Path.GetFileName(runtime)["runtime-".Length..], runner));
            }
        }

        return [.. found.OrderByDescending(r => Version.TryParse(r.Version, out var v) ? v : new Version(0, 0)).ThenBy(r => r.Version, StringComparer.Ordinal)];
    }
}
