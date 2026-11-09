using System.Text.Json;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

/// <summary>A run options file that was read: the options, the hash of the bytes they came from, where the file is, and whether it is in Studio's own folder.</summary>
public sealed record RunOptionsFile(RunProfile Profile, string Hash, string Path, bool Own);

/// <summary>
/// Picks the program and options of a run from the settings, the saved run options of the data file, and an explicit runner.
/// The options live in Studio's folder (keyed by the data file path) or, by setting, next to the data file as <c>.g3mrun.json</c>.
/// A file in Studio's folder was written by Studio; a file next to the data file can name any program, so it counts only after the owner approved its hash.
/// </summary>
public static class RunPlan
{
    private const long MaxProjectBytes = 256 * 1024;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>The legacy place: beside the data file, so the options travel with the game folder.</summary>
    public static string ProjectFile(string dataFile) => dataFile + ".g3mrun.json";

    /// <summary>The place in Studio's own folder, keyed by the full path of the data file.</summary>
    public static string StudioFile(UserData home, string dataFile) => home.Combine("runoptions", UserData.KeyFor(dataFile) + ".json");

    /// <summary>Where a save goes under the setting.</summary>
    public static string SavePath(AppSettings settings, UserData home, string dataFile) =>
        settings.RunOptionsLocation == RunOptionsLocation.BesideDataFile ? ProjectFile(dataFile) : StudioFile(home, dataFile);

    // One read gives both the profile and the hash of the very bytes it came from, so the approved content is the content that runs.
    private static RunOptionsFile? Read(string path, bool own)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxProjectBytes) return null;
            var bytes = File.ReadAllBytes(path);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            return JsonSerializer.Deserialize<RunProfile>(bytes) is { } p
                ? new RunOptionsFile(p with { Name = p.Name ?? "", Runner = p.Runner ?? "", Arguments = p.Arguments ?? "", WorkingDir = p.WorkingDir ?? "", Environment = p.Environment ?? "" }, hash, path, own)
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null; // a broken file is the same as none; Settings still apply
        }
    }

    /// <summary>The file in the preferred place of the setting, else the one in the other place.</summary>
    public static RunOptionsFile? Find(AppSettings settings, string dataFile, UserData? home)
    {
        RunOptionsFile? Studio() => home is null ? null : Read(StudioFile(home, dataFile), true);
        RunOptionsFile? Beside() => Read(ProjectFile(dataFile), false);
        return settings.RunOptionsLocation == RunOptionsLocation.BesideDataFile ? Beside() ?? Studio() : Studio() ?? Beside();
    }

    /// <summary>The legacy file next to the data file, read without any approval check.</summary>
    public static RunProfile? LoadProject(string dataFile) => Read(ProjectFile(dataFile), false)?.Profile;

    /// <summary>Writes the options to the place of the setting and returns the path. A file next to the data file is approved by its hash, since the owner wrote it here.</summary>
    public static string SaveProject(AppSettings settings, UserData home, string dataFile, RunProfile profile)
    {
        var path = SavePath(settings, home, dataFile);
        UserData.WriteAllTextAtomic(path, JsonSerializer.Serialize(profile, Indented));
        return path;
    }

    /// <summary>SHA-256 of the file next to the data file, or null when there is none; the owner approves a file by this hash.</summary>
    public static string? ProjectHash(string dataFile) => Read(ProjectFile(dataFile), false)?.Hash;

    /// <summary>True when the file may be used: it is in Studio's folder, or the owner approved its hash.</summary>
    public static bool Trusted(AppSettings settings, RunOptionsFile file) => file.Own || settings.TrustedRunFiles.Contains(file.Hash);

    /// <summary>A run options file that is not approved yet, with its hash; null when there is none or it may be used.</summary>
    public static (RunProfile Profile, string Hash)? Unapproved(AppSettings settings, string dataFile, UserData? home = null) =>
        Find(settings, dataFile, home) is { } file && !Trusted(settings, file) ? (file.Profile, file.Hash) : null;

    /// <summary>Copies the file next to the data file into Studio's folder; false when there is none to copy or the owner has not approved its hash (files in Studio's folder count as trusted). The original stays, and is ignored while the setting says Studio's folder.</summary>
    public static bool Migrate(AppSettings settings, UserData home, string dataFile)
    {
        if (Read(ProjectFile(dataFile), false) is not { } legacy || !Trusted(settings, legacy)) return false;
        UserData.WriteAllTextAtomic(StudioFile(home, dataFile), JsonSerializer.Serialize(legacy.Profile, Indented));
        return true;
    }

    /// <summary>The spec and how much Studio knows the program. Spec is null when there is nothing to start.</summary>
    public static (RunSpec? Spec, RunnerCheck Check, string? Runner) Resolve(AppSettings settings, string dataFile, string? gameFileName, string runDataFile, string? runnerOverride = null, UserData? home = null)
    {
        // A file next to the data file can name any program and environment, so it counts only once the owner approved this exact content.
        var project = Find(settings, dataFile, home) is { } read && Trusted(settings, read) ? read.Profile : null;
        var runner = runnerOverride;
        // A relative path is taken from the data folder only when the trusted run file of this project named it; a global setting or an override must not find a program planted there.
        var fromProject = false;
        if (string.IsNullOrEmpty(runner) && project is { Runner.Length: > 0 }) (runner, fromProject) = (project.Runner, true);
        if (string.IsNullOrEmpty(runner) && settings.RunRunner.Length > 0) runner = settings.RunRunner;
        var configured = runner is not null;
        runner ??= RunLocator.Find(dataFile, gameFileName);
        if (runner is null) return (null, RunnerCheck.Missing, null);
        // A scanned runner is Known only when it is the game file beside the data file; a named one gets the usual check.
        var check = configured ? RunLocator.Check(runner, dataFile, gameFileName, fromProject) : RunLocator.CheckFound(runner, dataFile, gameFileName);
        if (check == RunnerCheck.Missing) return (null, check, runner);
        var profile = project ?? new RunProfile("", "", settings.RunArguments, settings.RunDebugOutput, settings.RunWorkingDir, settings.RunEnvironment);
        return (new RunSpec(RunLocator.Resolve(runner, fromProject ? dataFile : null) ?? runner, runDataFile, profile.Arguments, profile.WorkingDir, profile.Environment, profile.DebugOutput), check, runner);
    }
}
