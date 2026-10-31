using System.Diagnostics;
using System.Text;

namespace G3MStudio.Core.Projects;

/// <param name="Path">Path relative to the project folder, with forward slashes.</param>
/// <param name="Index">Staged state letter of git's short status, space when none.</param>
/// <param name="Work">Working tree state letter, <c>?</c> for an untracked file.</param>
public sealed record GitChange(string Path, char Index, char Work)
{
    public bool Staged => Index is not (' ' or '?');
}

public sealed record GitCommitInfo(string Hash, string Author, DateTimeOffset When, string Subject);

public sealed record GitResult(int ExitCode, string Output, string Error)
{
    public bool Ok => ExitCode == 0;
}

/// <summary>
/// Git on the project folder through the <c>git</c> program (Q36, Q54). Arguments are passed as a list, never through a shell,
/// and git never prompts. Files G3MStudio writes are kept byte for byte: no line ending conversion, binary assets.
/// </summary>
public sealed partial class GitService(string folder, IReadOnlyDictionary<string, string>? environment = null)
{
    public string Folder { get; } = folder;

    public static async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        return GitProcess.Exe.Value is not null && (await GitProcess.RunAsync(Environment.CurrentDirectory, ["--version"], null, cancellationToken)).Ok;
    }

    // A linked worktree has a .git file instead of a folder.
    public bool IsRepository => Path.Combine(Folder, ".git") is var marker && (Directory.Exists(marker) || File.Exists(marker));

    private Task<GitResult> RunAsync(string[] args, CancellationToken cancellationToken) => GitProcess.RunAsync(Folder, args, environment, cancellationToken);

    /// <summary>Creates the repository and, when missing, the ignore and attribute files. The user's own files are never replaced.</summary>
    public async Task<GitResult> InitAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["init"], cancellationToken);
        if (!result.Ok) return result;
        foreach (var (name, text) in new[] { (".gitignore", GitFiles.Ignore), (".gitattributes", GitFiles.Attributes) })
        {
            var path = Path.Combine(Folder, name);
            if (!File.Exists(path)) await File.WriteAllTextAsync(path, text, new UTF8Encoding(false), cancellationToken);
        }

        return result;
    }

    public async Task<IReadOnlyList<GitChange>> StatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["status", "--porcelain=v1", "-z", "--untracked-files=all"], cancellationToken);
        if (!result.Ok) throw new IOException(result.Error);
        var changes = new List<GitChange>();
        var parts = result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            changes.Add(new GitChange(part[3..], part[0], part[1]));
            if (part[0] is 'R' or 'C' || part[1] is 'R' or 'C') i++; // a rename or copy lists the old path next
        }

        return changes;
    }

    public Task<GitResult> StageAllAsync(CancellationToken cancellationToken = default) => RunAsync(["add", "--all"], cancellationToken);

    public async Task<GitResult> UnstageAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["restore", "--staged", "--", "."], cancellationToken);
        // Before the first commit there is no HEAD to restore from: drop the files from the index instead.
        return result.Ok ? result : await RunAsync(["rm", "-r", "--cached", "--quiet", "--ignore-unmatch", "--", "."], cancellationToken);
    }

    public Task<GitResult> CommitAsync(string message, CancellationToken cancellationToken = default) => RunAsync(["commit", "--message", message], cancellationToken);

    public async Task<IReadOnlyList<GitCommitInfo>> LogAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["log", $"--max-count={count}", "--format=%H%x1f%an%x1f%aI%x1f%s%x1e"], cancellationToken);
        if (!result.Ok) return []; // no commits yet
        return [.. result.Output.Split('\x1e').Select(r => r.Trim('\r', '\n')).Where(r => r.Length > 0).Select(r => r.Split('\x1f')).Select(f => new GitCommitInfo(f[0], f[1], DateTimeOffset.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture), f[3]))];
    }

    public async Task<string> DiffAsync(string path, bool staged, CancellationToken cancellationToken = default) =>
        (await RunAsync(staged ? ["diff", "--no-ext-diff", "--no-textconv", "--cached", "--", path] : ["diff", "--no-ext-diff", "--no-textconv", "--", path], cancellationToken)).Output;
}

/// <summary>What the project folder gets for Git (Q54): ignore and attribute files, and the files worth putting in Git LFS.</summary>
public static class GitFiles
{
    public const string Ignore = ".g3mstudio/\nbuild/\n*.tmp\n";

    // -text turns the end-of-line conversion off (a clone with core.autocrlf=true must rebuild the same data file); binary also stops diffs.
    public const string Attributes = """
        *.g3mproj -text
        *.json -text
        *.gml -text
        *.vsh -text
        *.fsh -text
        *.png binary
        *.ogg binary
        *.wav binary
        *.mp3 binary
        *.ttf binary
        *.otf binary
        """ + "\n";

    private static readonly HashSet<string> LfsExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".ogg", ".wav", ".mp3" };

    /// <summary>Audio and image files at or above <paramref name="minBytes"/>: the ones to suggest Git LFS for.</summary>
    public static IReadOnlyList<string> LargeAssets(string folder, long minBytes = 10 << 20) =>
        [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => LfsExtensions.Contains(Path.GetExtension(f)) && !f.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.Ordinal) && new FileInfo(f).Length >= minBytes)
            .Select(f => Path.GetRelativePath(folder, f).Replace(Path.DirectorySeparatorChar, '/'))];
}

/// <summary>Runs git with an argument list: no shell, no prompts, UTF-8 output.</summary>
internal static class GitProcess
{
    /// <summary>The full path of git from the PATH entries; relative entries and the current folder are skipped so a planted program there is never run.</summary>
    public static readonly Lazy<string?> Exe = new(() =>
    {
        var name = OperatingSystem.IsWindows() ? "git.exe" : "git";
        return (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(Path.IsPathRooted).Select(dir => Path.Combine(dir, name)).FirstOrDefault(File.Exists);
    });

    public static async Task<GitResult> RunAsync(string folder, string[] args, IReadOnlyDictionary<string, string>? environment, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(Exe.Value ?? throw new FileNotFoundException("git is not installed"))
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // fsmonitor off: a project folder that came from somebody else must not start a program of its config when it is only read.
        foreach (var arg in (string[])["-c", "core.quotepath=false", "-c", "core.fsmonitor=false", .. args]) info.ArgumentList.Add(arg);
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var (key, value) in environment ?? new Dictionary<string, string>()) info.Environment[key] = value;

        using var process = Process.Start(info) ?? throw new IOException("git did not start");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new GitResult(process.ExitCode, await output, (await error).Trim());
    }
}
