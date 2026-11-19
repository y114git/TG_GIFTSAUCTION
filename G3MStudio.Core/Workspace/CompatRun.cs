using System.Text.RegularExpressions;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

/// <summary>What <see cref="CompatRun"/> needs from the machine, so tests can fake a Linux or macOS layout. Paths use '/' and are full paths.</summary>
public interface ICompatProbe
{
    bool IsWindows { get; }

    bool IsMacOS { get; }

    string Home { get; }

    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>Full paths of the files in a folder; empty when the folder is not there.</summary>
    IReadOnlyList<string> Files(string folder);

    /// <summary>Full paths of the folders in a folder; empty when the folder is not there.</summary>
    IReadOnlyList<string> Folders(string folder);

    string? ReadText(string path);

    string? Env(string name);

    /// <summary>The full path of a program found on PATH; null when it is not there.</summary>
    string? OnPath(string name);
}

/// <summary>The real machine.</summary>
public sealed class SystemProbe : ICompatProbe
{
    public static SystemProbe Instance { get; } = new();

    public bool IsWindows => OperatingSystem.IsWindows();

    public bool IsMacOS => OperatingSystem.IsMacOS();

    public string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IReadOnlyList<string> Files(string folder) => List(() => Directory.GetFiles(folder));

    public IReadOnlyList<string> Folders(string folder) => List(() => Directory.GetDirectories(folder));

    public string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) && new FileInfo(path).Length <= 4 * 1024 * 1024 ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string? Env(string name) => Environment.GetEnvironmentVariable(name);

    public string? OnPath(string name) => RunLocator.Resolve(name);

    private static string[] List(Func<string[]> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>What the user set for compatibility, as <see cref="RunPlan"/> read it from the settings or a run options file.</summary>
public sealed record CompatOptions(CompatMode Mode, string Program, string Prefix, string Command, string UserEnvironment, string WorkingDir);

/// <summary>
/// How a Windows game is started: the program and its leading arguments, the environment of the tool, and what Studio tells the user.
/// <see cref="Leading"/> comes before the game arguments; with <see cref="InlineArgs"/> (a custom command) it holds the whole command and the token {args} stands for the game arguments.
/// </summary>
public sealed record CompatLaunch(
    CompatMode Tool,
    string Program,
    IReadOnlyList<string> Leading,
    IReadOnlyDictionary<string, string> Environment,
    string Exe,
    string? Detail = null,
    string? Prefix = null,
    bool InlineArgs = false,
    string? SteamSaves = null,
    string? NewPrefix = null);

public enum CompatKind
{
    /// <summary>Nothing to run.</summary>
    None,

    /// <summary>A program of this system.</summary>
    Native,

    /// <summary>A Windows program with a compatibility tool.</summary>
    Compat,

    /// <summary>A Windows program, but the tool is missing.</summary>
    NoTool,
}

public sealed record CompatDecision(CompatKind Kind, string? Runner = null, RunnerCheck Check = RunnerCheck.Unknown, CompatLaunch? Launch = null);

/// <summary>
/// Decides how a game starts on Linux and macOS: a native runner, a macOS bundle, or a Windows .exe with Wine, Proton, umu, PortProton or a custom command.
/// Pure: everything it reads comes from the <see cref="ICompatProbe"/>, and all paths are Unix paths.
/// </summary>
public static partial class CompatRun
{
    private const string PortProtonApp = "ru.linux_gaming.PortProton";

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InstallDir();

    [GeneratedRegex("\"appid\"\\s+\"(\\d+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AppId();

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LibraryPath();

    [GeneratedRegex("<key>CFBundleExecutable</key>\\s*<string>([^<]+)</string>", RegexOptions.CultureInvariant)]
    private static partial Regex BundleExecutable();

    [GeneratedRegex("(\\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();

    /// <summary>The first decision that holds: a native runner, a macOS bundle, then a Windows .exe with the tool of the options. An explicit runner is taken as given.</summary>
    public static CompatDecision Decide(ICompatProbe probe, CompatOptions options, string dataFile, string? gameFileName, string? explicitRunner = null)
    {
        var exe = explicitRunner is not null && options.Mode != CompatMode.Native && IsExe(explicitRunner) ? explicitRunner : null;
        if (explicitRunner is not null && exe is null) return new(CompatKind.Native, explicitRunner);
        if (exe is null && options.Mode is CompatMode.Auto or CompatMode.Native)
        {
            if (FindNative(probe, dataFile, gameFileName) is { } native) return new(CompatKind.Native, native, NativeCheck(native, dataFile, gameFileName));
            if (probe.IsMacOS && FindBundle(probe, dataFile) is { } bundle) return new(CompatKind.Native, bundle, RunnerCheck.Known);
            if (options.Mode == CompatMode.Native) return new(CompatKind.None);
        }

        exe ??= FindExe(probe, dataFile, gameFileName);
        if (exe is null && options.Mode == CompatMode.Custom) exe = FindNative(probe, dataFile, gameFileName); // a custom command may wrap a native runner too
        if (exe is null) return new(CompatKind.None);
        return Launch(probe, options, exe, dataFile, gameFileName) is { } launch
            ? new(CompatKind.Compat, exe, RunnerCheck.Known, launch)
            : new(CompatKind.NoTool, exe);
    }

    /// <summary>What Auto would use right now, for the Settings page; null when no tool is found. Without a data file only the tools of the machine count.</summary>
    public static CompatLaunch? Detect(ICompatProbe probe, CompatOptions options, string? dataFile)
    {
        var data = dataFile ?? "/game/data.win";
        var exe = FindExe(probe, data, null) ?? Join(Parent(data), "game.exe");
        return Launch(probe, options with { Mode = CompatMode.Auto }, exe, data, null);
    }

    /// <summary>The path of a data file as a Windows program under Wine sees it: its name when it lies in the working folder, else Z: plus the Unix path with backslashes (Wine maps Z: to /).</summary>
    public static string WinePath(string path, string workingDir)
    {
        if (!path.StartsWith('/')) return path;
        return Parent(path) == workingDir.TrimEnd('/') ? Name(path) : "Z:" + path.Replace('/', '\\');
    }

    public static bool IsExe(string path) => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    // ---- finding the game ----

    private static string? FindNative(ICompatProbe probe, string dataFile, string? gameFileName)
    {
        var folder = Parent(dataFile);
        var parent = Parent(folder);
        var names = RunLocator.Names(dataFile, gameFileName, unix: true).Where(n => !IsExe(n)).ToList();
        string[] folders = folder == parent ? [folder] : [folder, parent, Join(parent, "MacOS")];
        return folders.SelectMany(f => names.Select(n => Join(f, n))).FirstOrDefault(probe.FileExists);
    }

    private static RunnerCheck NativeCheck(string runner, string dataFile, string? gameFileName) =>
        Parent(runner) == Parent(dataFile) && RunLocator.GameNames(dataFile, gameFileName, unix: true).Contains(Name(runner), StringComparer.OrdinalIgnoreCase) ? RunnerCheck.Known : RunnerCheck.Unknown;

    /// <summary>The Windows runner next to the data file or one folder up: the game name, then runner.exe. Names match in any letter case, since Linux file systems care.</summary>
    internal static string? FindExe(ICompatProbe probe, string dataFile, string? gameFileName)
    {
        var folder = Parent(dataFile);
        var names = RunLocator.Names(dataFile, gameFileName).Where(IsExe).ToList();
        foreach (var where in new[] { folder, Parent(folder) }.Distinct())
        {
            var files = probe.Files(where);
            foreach (var name in names)
            {
                if (files.FirstOrDefault(f => Name(f).Equals(name, StringComparison.OrdinalIgnoreCase)) is { } found) return found;
            }
        }

        return null;
    }

    /// <summary>A macOS .app bundle next to the data file or one folder up: its Contents/MacOS program named by Info.plist, else the only file there.</summary>
    internal static string? FindBundle(ICompatProbe probe, string dataFile)
    {
        var folder = Parent(dataFile);
        foreach (var where in new[] { folder, Parent(folder) }.Distinct())
        {
            foreach (var app in probe.Folders(where).Where(f => Name(f).EndsWith(".app", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
            {
                var macos = Join(app, "Contents/MacOS");
                // shortcut: only a text Info.plist is read; a binary one falls back to the only file in MacOS
                var named = probe.ReadText(Join(app, "Contents/Info.plist")) is { } plist && BundleExecutable().Match(plist) is { Success: true } m ? Join(macos, m.Groups[1].Value.Trim()) : null;
                if (named is not null && probe.FileExists(named)) return named;
                if (probe.Files(macos) is [var only]) return only;
            }
        }

        return null;
    }

    // ---- Steam ----

    private sealed record SteamGame(string Steamapps, string AppId)
    {
        public string CompatData => Join(Steamapps, "compatdata/" + AppId);
    }

    /// <summary>The Steam game a path belongs to: it lies in steamapps/common/&lt;dir&gt; and an appmanifest of that steamapps names the dir as its installdir.</summary>
    private static SteamGame? FindSteamGame(ICompatProbe probe, string dataFile)
    {
        const string marker = "/steamapps/common/";
        var at = dataFile.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;
        var steamapps = dataFile[..(at + "/steamapps".Length)];
        var dir = dataFile[(at + marker.Length)..].Split('/')[0];
        foreach (var manifest in probe.Files(steamapps).Where(f => Name(f).StartsWith("appmanifest_", StringComparison.Ordinal) && f.EndsWith(".acf", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            if (probe.ReadText(manifest) is not { } text || InstallDir().Match(text) is not { Success: true } install || !install.Groups[1].Value.Equals(dir, StringComparison.OrdinalIgnoreCase)) continue;
            var id = AppId().Match(text) is { Success: true } a ? a.Groups[1].Value : Name(manifest)["appmanifest_".Length..^".acf".Length];
            return new SteamGame(steamapps, id);
        }

        return null;
    }

    private static string? SteamRoot(ICompatProbe probe)
    {
        string?[] candidates =
        [
            probe.Env("STEAM_COMPAT_CLIENT_INSTALL_PATH"),
            Join(probe.Home, ".local/share/Steam"),
            Join(probe.Home, ".steam/steam"),
            Join(probe.Home, ".steam/root"),
            Join(probe.Home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"),
            Join(probe.Home, "snap/steam/common/.local/share/Steam"),
        ];
        return candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && probe.DirectoryExists(c));
    }

    private static List<string> Libraries(ICompatProbe probe, string? root)
    {
        if (root is null) return [];
        var libraries = new List<string> { root };
        if (probe.ReadText(Join(root, "steamapps/libraryfolders.vdf")) is { } vdf)
        {
            libraries.AddRange(LibraryPath().Matches(vdf).Select(m => m.Groups[1].Value.Replace("\\\\", "\\", StringComparison.Ordinal)).Where(p => p.StartsWith('/')));
        }

        return [.. libraries.Distinct()];
    }

    /// <summary>The proton file: the one the setting names (the file or its folder), else the newest of the Steam root's compatibilitytools.d and of every library's Proton folders. Experimental counts as newest.</summary>
    private static (string File, string Name)? FindProton(ICompatProbe probe, string setting, string? root, SteamGame? steam)
    {
        if (setting.Length > 0) return ProtonOf(probe, setting) is { } named ? (named, Name(Parent(named))) : null;
        var libraries = Libraries(probe, root);
        if (steam is not null && Parent(steam.Steamapps) is var own && !libraries.Contains(own)) libraries.Add(own);
        var folders = new List<string>();
        if (root is not null) folders.AddRange(probe.Folders(Join(root, "compatibilitytools.d")));
        foreach (var library in libraries) folders.AddRange(probe.Folders(Join(library, "steamapps/common")).Where(f => Name(f).StartsWith("Proton", StringComparison.OrdinalIgnoreCase)));
        var best = folders.Distinct()
            .Where(f => probe.FileExists(Join(f, "proton")))
            .OrderByDescending(f => Name(f).Contains("Experimental", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(Name, Comparer<string>.Create(Natural))
            .FirstOrDefault();
        return best is null ? null : (Join(best, "proton"), Name(best));
    }

    private static string? ProtonOf(ICompatProbe probe, string path)
    {
        path = Expand(probe, path);
        if (Name(path) == "proton" && probe.FileExists(path)) return path;
        return probe.FileExists(Join(path, "proton")) ? Join(path, "proton") : null;
    }

    /// <summary>Compares names with the numbers inside as numbers: Proton 9.0 comes before Proton 10.0.</summary>
    internal static int Natural(string a, string b)
    {
        var x = Digits().Split(a);
        var y = Digits().Split(b);
        for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
        {
            var c = i % 2 == 1 ? CompareNumbers(x[i], y[i]) : string.Compare(x[i], y[i], StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }

        return x.Length.CompareTo(y.Length);
    }

    private static int CompareNumbers(string a, string b)
    {
        a = a.TrimStart('0');
        b = b.TrimStart('0');
        return a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
    }

    // ---- the tools ----

    private static CompatLaunch? Launch(ICompatProbe probe, CompatOptions o, string exe, string dataFile, string? gameFileName)
    {
        var steam = FindSteamGame(probe, dataFile);
        var game = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(gameFileName) ? Name(exe) : gameFileName);
        var ctx = new Context(probe, o, exe, dataFile, game, steam);
        CompatMode[] order = o.Mode == CompatMode.Auto ? [CompatMode.Proton, CompatMode.Umu, CompatMode.Wine, CompatMode.PortProton] : [o.Mode];
        foreach (var tool in order)
        {
            var launch = tool switch
            {
                CompatMode.Proton when o.Mode == CompatMode.Auto && steam is null => null, // Auto uses Proton only for a Steam game
                CompatMode.Proton => ctx.Proton(),
                CompatMode.Umu => ctx.Umu(),
                CompatMode.Wine => ctx.Wine(),
                CompatMode.PortProton => ctx.PortProton(),
                CompatMode.Custom => ctx.Custom(),
                _ => null,
            };
            if (launch is not null) return launch;
        }

        return null;
    }

    private sealed class Context(ICompatProbe probe, CompatOptions o, string exe, string dataFile, string game, SteamGame? steam)
    {
        private string ExplicitPrefix => Expand(probe, o.Prefix.Trim());

        private string? SteamPfx => steam is not null && probe.DirectoryExists(steam.CompatData + "/pfx") ? steam.CompatData + "/pfx" : null;

        // The tool's own default is used when nothing is set; a Steam prefix counts only when it is already there, so Wine never makes a half prefix in Steam's folder.
        private string? WinePrefix => ExplicitPrefix.Length > 0 ? ExplicitPrefix : SteamPfx;

        private string? Saves => steam is not null && !probe.IsMacOS ? steam.CompatData + "/pfx/drive_c/users/steamuser/AppData/Local/" + game : null;

        private string? Forced(CompatMode tool) => o.Mode == tool && o.Program.Trim().Length > 0 ? o.Program.Trim() : null;

        private Dictionary<string, string> Env(string? prefix)
        {
            var env = new Dictionary<string, string>();
            if (prefix is not null) env["WINEPREFIX"] = prefix;
            if (probe.Env("WINEDEBUG") is null && !GameRun.ParseEnvironment(o.UserEnvironment).Any(p => p.Name == "WINEDEBUG")) env["WINEDEBUG"] = "-all";
            return env;
        }

        public CompatLaunch? Wine()
        {
            var program = Forced(CompatMode.Wine) is { } set ? FindProgram(probe, set) : DetectWine();
            if (program is null) return null;
            var prefix = WinePrefix;
            var shown = prefix ?? probe.Env("WINEPREFIX") ?? Join(probe.Home, ".wine");
            // Apple's Game Porting Toolkit script takes the prefix as its first argument
            IReadOnlyList<string> leading = Name(program) == "gameportingtoolkit" ? [shown, exe] : [exe];
            return new(CompatMode.Wine, program, leading, Env(prefix), exe, Prefix: shown, SteamSaves: Saves);
        }

        private string? DetectWine()
        {
            foreach (var name in new[] { "wine", "wine64" })
            {
                if (FindTool(probe, name) is { } found) return found;
            }

            if (!probe.IsMacOS) return null;
            if (FindTool(probe, "wine-stable") is { } stable) return stable;
            foreach (var app in probe.Folders("/Applications").Where(f => Name(f).StartsWith("Wine", StringComparison.OrdinalIgnoreCase) && f.EndsWith(".app", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
            {
                foreach (var tail in new[] { "Contents/Resources/start/bin/wine", "Contents/Resources/wine/bin/wine" })
                {
                    if (probe.FileExists(Join(app, tail))) return Join(app, tail);
                }
            }

            const string crossOver = "/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine";
            return probe.FileExists(crossOver) ? crossOver : FindTool(probe, "gameportingtoolkit");
        }

        public CompatLaunch? Proton()
        {
            var root = SteamRoot(probe) ?? (steam is null ? null : Parent(steam.Steamapps));
            if (FindProton(probe, Forced(CompatMode.Proton) ?? "", root, steam) is not var (file, version)) return null;
            string data;
            if (ExplicitPrefix.Length > 0) data = Name(ExplicitPrefix) == "pfx" ? Parent(ExplicitPrefix) : ExplicitPrefix;
            else if (steam is not null) data = steam.CompatData;
            else data = Join(probe.Env("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Join(probe.Home, ".local/share"), "G3MStudio/proton/" + game);
            var env = Env(null);
            env["STEAM_COMPAT_DATA_PATH"] = data;
            if (root is not null) env["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = root;
            return new(CompatMode.Proton, file, ["run", exe], env, exe, version, data + "/pfx", SteamSaves: Saves, NewPrefix: probe.DirectoryExists(data) ? null : data);
        }

        public CompatLaunch? Umu()
        {
            var set = Forced(CompatMode.Umu);
            var proton = set is null ? null : ProtonOf(probe, set);
            var program = set is not null && proton is null ? FindProgram(probe, set) : FindTool(probe, "umu-run");
            if (program is null) return null;
            var id = "umu-" + (steam?.AppId ?? "0");
            var prefix = WinePrefix;
            var env = Env(prefix);
            env["GAMEID"] = id;
            if (proton is not null) env["PROTONPATH"] = Parent(proton);
            return new(CompatMode.Umu, program, [exe], env, exe, Prefix: prefix ?? Join(probe.Home, "Games/umu/" + id), SteamSaves: Saves);
        }

        public CompatLaunch? PortProton()
        {
            var prefix = ExplicitPrefix.Length > 0 ? ExplicitPrefix : null; // PortProton keeps its own prefixes
            var env = Env(prefix);
            if (Forced(CompatMode.PortProton) is { } set) return FindProgram(probe, set) is { } p ? Make(p, [exe]) : null;
            if (FindTool(probe, "portproton") is { } tool) return Make(tool, [exe]);
            var flatpak = new[] { Join(probe.Home, ".local/share/flatpak/app/" + PortProtonApp), "/var/lib/flatpak/app/" + PortProtonApp }.Any(probe.DirectoryExists);
            if (flatpak && FindTool(probe, "flatpak") is { } fp) return Make(fp, ["run", PortProtonApp, exe]);
            var script = Join(probe.Home, "PortProton/data/scripts/start.sh");
            return probe.FileExists(script) ? Make(script, [exe]) : null;

            CompatLaunch Make(string program, IReadOnlyList<string> leading) => new(CompatMode.PortProton, program, leading, env, exe, Prefix: prefix, SteamSaves: Saves);
        }

        public CompatLaunch? Custom()
        {
            var parts = GameRun.SplitArguments(o.Command);
            if (parts.Count == 0) return null;
            var prefix = WinePrefix;
            var dir = o.WorkingDir.Length > 0 ? o.WorkingDir : Parent(dataFile);
            var tokens = parts.Select(p => p.Replace("{exe}", exe, StringComparison.Ordinal).Replace("{dir}", dir, StringComparison.Ordinal).Replace("{prefix}", prefix ?? "", StringComparison.Ordinal)).ToList();
            return FindProgram(probe, tokens[0]) is { } program ? new(CompatMode.Custom, program, tokens[1..], Env(prefix), exe, Prefix: prefix, InlineArgs: true, SteamSaves: Saves) : null;
        }
    }

    // ---- small helpers ----

    /// <summary>A program given as a full path (or ~/path), or a bare name looked up like a tool. Null when it is not there.</summary>
    private static string? FindProgram(ICompatProbe probe, string text)
    {
        text = Expand(probe, text);
        if (text.StartsWith('/')) return probe.FileExists(text) ? text : null;
        return text.Contains('/', StringComparison.Ordinal) ? null : FindTool(probe, text);
    }

    // A program launched from a desktop icon has a short PATH, so the usual install folders are searched too.
    private static string? FindTool(ICompatProbe probe, string name) =>
        probe.OnPath(name) ?? new[] { "/usr/local/bin", "/opt/homebrew/bin", "/usr/bin", Join(probe.Home, ".local/bin") }.Select(d => Join(d, name)).FirstOrDefault(probe.FileExists);

    private static string Expand(ICompatProbe probe, string path) => path == "~" ? probe.Home : path.StartsWith("~/", StringComparison.Ordinal) ? Join(probe.Home, path[2..]) : path;

    internal static string Join(string folder, string name) => folder.TrimEnd('/') + "/" + name;

    internal static string Parent(string path)
    {
        path = path.TrimEnd('/');
        var at = path.LastIndexOf('/');
        return at < 0 ? "." : at == 0 ? "/" : path[..at];
    }

    internal static string Name(string path) => path.TrimEnd('/')[(path.TrimEnd('/').LastIndexOf('/') + 1)..];
}
