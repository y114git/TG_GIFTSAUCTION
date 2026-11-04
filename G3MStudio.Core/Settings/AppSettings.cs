using System.Text.Json;
using System.Text.Json.Serialization;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Settings;

/// <summary>
/// The settings the Settings page offers (design/settings.md, user scope only). <see cref="Project"/> holds the autosave and autosync
/// defaults for new projects; changing them also changes the open project.
/// </summary>
public sealed record AppSettings
{
    public ProjectSettings Project { get; init; } = new();

    /// <summary>Rotating backups of the data file, 1 to 3 (<c>files.backupCount</c>).</summary>
    public int BackupCount { get; init; } = 3;

    /// <summary>Open the window where it was closed (<c>ui.rememberWindow</c>); UTMT-SET-06.</summary>
    public bool RememberWindow { get; init; } = true;

    /// <summary>Tell once per session that a run uses a temporary copy and keeps no changes in the data file (<c>warnTempRun</c>, UTMT-SET-08).</summary>
    public bool WarnTempRun { get; init; } = true;

    /// <summary>Where the main window was when it was closed; null before the first close.</summary>
    public WindowPlacement? Window { get; init; }

    /// <summary>Ask before closing with unsaved changes (<c>files.closeUnsaved</c> = ask); off keeps them in the journal without asking.</summary>
    public bool AskOnClose { get; init; } = true;

    /// <summary>Empty means the theme font.</summary>
    public string FontFamily { get; init; } = "";

    /// <summary>A font weight name, or empty for the theme weight.</summary>
    public string FontWeight { get; init; } = "";

    /// <summary>Percent, 50 to 200 (<c>ui.zoom</c>).</summary>
    public int UiScale { get; init; } = 100;

    /// <summary>Decompiler settings changed by the user, setting name to its value (<c>decompiler</c>); a missing name keeps the library's default. See DecompilerOptions.</summary>
    public IReadOnlyDictionary<string, string> Decompiler { get; init; } = new Dictionary<string, string>();

    public string Language { get; init; } = "en";

    /// <summary>Memory budget in MB; 0 means no limit (as much as the system has free). The low-memory system runs either way.</summary>
    public int MemoryLimitMb { get; init; }

    /// <summary>A runner to start instead of the game executable next to the data file (for example a GameMaker runtime's Runner.exe); empty means the game executable.</summary>
    public string RunRunner { get; init; } = "";

    /// <summary>Folders with GameMaker runtimes besides the ones GameMaker uses, separated by semicolons (UTMT-SET-02).</summary>
    public string RuntimeFolders { get; init; } = "";

    /// <summary>Extra arguments for the runner, split like a shell would (quotes group words).</summary>
    public string RunArguments { get; init; } = "";

    /// <summary>Adds <c>-debugoutput</c> so the game writes its log next to the temporary file.</summary>
    public bool RunDebugOutput { get; init; }

    /// <summary>Named sets of the three run options above; picking one in Settings copies its values into them.</summary>
    public IReadOnlyList<RunProfile> RunProfiles { get; init; } = [];

    /// <summary>The profile picked last, for the Settings page to show; empty when none.</summary>
    public string ActiveRunProfile { get; init; } = "";

    /// <summary>The run profile each data file used last, full path to profile name; opening the file picks it again.</summary>
    public IReadOnlyDictionary<string, string> FileRunProfiles { get; init; } = new Dictionary<string, string>();

    /// <summary>Shortcuts changed by the user, command id to gesture; an empty gesture means the command has none (<c>keybindings</c>).</summary>
    public IReadOnlyDictionary<string, string> Shortcuts { get; init; } = new Dictionary<string, string>();

    /// <summary>Tighter rows, buttons and boxes, for more on screen.</summary>
    public bool CompactDensity { get; init; }

    /// <summary>Layouts the user saved, name to the dock layout as JSON (Window > Layout > Saved Layouts).</summary>
    public IReadOnlyDictionary<string, string> SavedLayouts { get; init; } = new Dictionary<string, string>();

    /// <summary>The first-launch welcome (language) was shown and answered or skipped.</summary>
    public bool OnboardingDone { get; init; }

    /// <summary>Send anonymous crash reports (releases with a key only); on by default, switched in the welcome window and Settings > Privacy.</summary>
    public bool CrashReports { get; init; } = true;

    /// <summary>Previews stand still instead of moving.</summary>
    public bool ReduceMotion { get; init; }

    /// <summary>Size of the text in code tabs, 8 to 32.</summary>
    public int CodeFontSize { get; init; } = 13;

    /// <summary>The font family of code views; empty uses the theme's code font.</summary>
    public string CodeFontFamily { get; init; } = "";

    public bool CodeWordWrap { get; init; }

    /// <summary>Lists the null entries of resource lists in the Explorer (UTMT-SET-09).</summary>
    public bool ShowNullEntries { get; init; }

    /// <summary>One grid size for every room instead of each room's own (UTMT-SET-13); 1 to 1024.</summary>
    public bool RoomGridOverride { get; init; }

    public int RoomGridWidth { get; init; } = 16;

    public int RoomGridHeight { get; init; } = 16;

    public bool CodeLineNumbers { get; init; } = true;

    /// <summary>The completion list opens while typing in code; Ctrl+Space opens it either way.</summary>
    public bool CodeAutoComplete { get; init; } = true;

    /// <summary>Colors changed by the user, <see cref="ThemeColors.Editable"/> key to #RRGGBB; a missing key keeps the theme color.</summary>
    public IReadOnlyDictionary<string, string> Colors { get; init; } = new Dictionary<string, string>();

    /// <summary>Sizes changed by the user, <see cref="ThemeSizes.All"/> key to value; a missing key keeps the theme size.</summary>
    public IReadOnlyDictionary<string, int> Sizes { get; init; } = new Dictionary<string, int>();

    /// <summary>Advanced: any theme token to a value of its own kind, written in settings.json (see ThemeOverrides); unknown tokens are ignored when applied.</summary>
    public IReadOnlyDictionary<string, string> Overrides { get; init; } = new Dictionary<string, string>();

    internal AppSettings Clamped()
    {
        var project = Project ?? new(); // a hand-edited file may hold null
        return this with
        {
            SavedLayouts = SavedLayouts is null ? [] : SavedLayouts.Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value is not null).ToDictionary(),
            FileRunProfiles = FileRunProfiles is null ? [] : FileRunProfiles.Where(p => p.Key is not null && p.Value is not null).ToDictionary(),
            RunProfiles = RunProfiles is null ? [] : [.. RunProfiles.Where(p => p is { Name.Length: > 0 }).DistinctBy(p => p.Name)],
            Shortcuts = Shortcuts is null ? new Dictionary<string, string>() : Shortcuts.Values.Any(v => v is null) ? Shortcuts.Where(p => p.Value is not null).ToDictionary() : Shortcuts, // a hand-edited file may hold null
            Colors = (Colors ?? new Dictionary<string, string>()).Where(p => ThemeColors.Editable.Contains(p.Key) && ThemeColors.IsValid(p.Value)).ToDictionary(),
            Sizes = ThemeSizes.Clamp(Sizes),
            Overrides = (Overrides ?? new Dictionary<string, string>()).Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value is not null).ToDictionary(),
            BackupCount = Math.Clamp(BackupCount, 1, 3),
            UiScale = Math.Clamp(UiScale, 50, 200),
            MemoryLimitMb = MemoryLimitMb <= 0 ? 0 : Math.Clamp(MemoryLimitMb, 256, 1_048_576),
            CodeFontSize = Math.Clamp(CodeFontSize, 8, 32),
            Project = project with { AutoSaveSeconds = Math.Clamp(project.AutoSaveSeconds, 1, 3600), AutoSyncSeconds = Math.Clamp(project.AutoSyncSeconds, 1, 3600) },
        };
    }
}

public sealed record RunProfile(string Name, string Runner, string Arguments, bool DebugOutput);

/// <summary>Reads and writes <c>settings.json</c> in the user data folder; every change is saved at once and announced.</summary>
public sealed class SettingsStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    /// <summary>The settings file, for a command that shows it to the user.</summary>
    public string FilePath { get; }

    public SettingsStore(UserData userData)
    {
        FilePath = userData.Combine("settings.json");
        if (!File.Exists(FilePath)) return;
        try
        {
            Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json)?.Clamped() ?? Current;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A file that does not parse is kept for the user and the defaults load (design/settings.md 3.1).
            KeepInvalid();
        }
    }

    /// <summary>False when the file could not be moved aside; the next change then overwrites it.</summary>
    private bool KeepInvalid()
    {
        try
        {
            File.Move(FilePath, FilePath + ".invalid", true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public AppSettings Current { get; private set; } = new();

    public event Action? Changed;

    private string _written = "";
    private FileSystemWatcher? _watcher;
    private SynchronizationContext? _context;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var next = change(Current).Clamped();
        if (next == Current) return;
        _written = JsonSerializer.Serialize(next, Json) + "\n";
        UserData.WriteAllTextAtomic(FilePath, _written); // a failed write leaves the old settings in place
        Current = next;
        Changed?.Invoke();
    }

    /// <summary>Starts hot reload: an edit of settings.json from outside (an editor, a copied theme) is read and applied at once, on the thread that called this. A file that does not parse is ignored until it does.</summary>
    public void Watch()
    {
        if (_watcher is not null || Path.GetDirectoryName(FilePath) is not { } folder || !Directory.Exists(folder)) return;
        _context = SynchronizationContext.Current;
        _written = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
        try
        {
            _watcher = new FileSystemWatcher(folder, Path.GetFileName(FilePath)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName };
            _watcher.Changed += Reloaded;
            _watcher.Created += Reloaded;
            _watcher.Renamed += Reloaded;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Unwatch(); // the system refused another watcher (Linux limits them): settings still work, only the live reload is off
        }
    }

    private void Reloaded(object? sender, FileSystemEventArgs e)
    {
        if (_context is { } context) context.Post(_ => Reload(), null);
        else Reload();
    }

    /// <summary>Stops hot reload and gives the watcher back to the system.</summary>
    public void Unwatch()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public void Dispose() => Unwatch();

    /// <summary>Reads settings.json again; applies it when it differs from what this program last wrote.</summary>
    public bool Reload()
    {
        try
        {
            var text = File.ReadAllText(FilePath);
            if (text == _written) return false;
            var next = JsonSerializer.Deserialize<AppSettings>(text, Json)?.Clamped();
            if (next is null) return false;
            _written = text;
            Current = next;
            Changed?.Invoke();
            return true;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return false; // half written or not valid yet: the next write of the file tries again
        }
    }
}

/// <summary>Position and size of the main window in device pixels, and whether it was maximized; the numbers are those of the normal window.</summary>
public sealed record WindowPlacement(int X, int Y, int Width, int Height, bool Maximized);
