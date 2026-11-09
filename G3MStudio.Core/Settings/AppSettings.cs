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

    /// <summary>Offer "do it anyway" with a warning where Studio would refuse a risky action; off keeps the plain refusal (<c>safety.askRisky</c>).</summary>
    public bool AskRiskyActions { get; init; } = true;

    /// <summary>Do a risky action without the warning (<c>safety.unsafeSilently</c>); for owners who know the risk.</summary>
    public bool AllowUnsafeSilently { get; init; }

    /// <summary>The answer chosen at a prompt with "Don't ask again", prompt id to <c>Yes</c> or <c>No</c>; a missing id asks every time. See <see cref="Settings.Prompts"/>.</summary>
    public IReadOnlyDictionary<string, string> Prompts { get; init; } = new Dictionary<string, string>();

    /// <summary>Where run options are saved (<c>run.optionsLocation</c>): Studio's own folder, or next to the data file.</summary>
    public RunOptionsLocation RunOptionsLocation { get; init; } = RunOptionsLocation.StudioFolder;

    /// <summary>Keep a compressed copy of each data file when it is first opened, as the base for Export Mod Pack (<c>modBase.enabled</c>).</summary>
    public bool ModBaseEnabled { get; init; } = true;

    /// <summary>Size cap of the base cache in MB (<c>modBase.maxMb</c>); the oldest copies go first.</summary>
    public int ModBaseMaxMb { get; init; } = 2048;

    /// <summary>Where the main window was when it was closed; null before the first close.</summary>
    public WindowPlacement? Window { get; init; }

    /// <summary>Ask before closing with unsaved changes (<c>files.closeUnsaved</c> = ask); off keeps them in the journal without asking.</summary>
    public bool AskOnClose { get; init; } = true;

    /// <summary>Empty means the theme font.</summary>
    public string FontFamily { get; init; } = "";

    /// <summary>A font weight name, or empty for the theme weight.</summary>
    public string FontWeight { get; init; } = "";

    /// <summary>Percent, 50 to 200; 0 means automatic from the screen (<c>ui.zoom</c>).</summary>
    public int UiScale { get; init; }

    /// <summary>Decompiler settings changed by the user, setting name to its value (<c>decompiler</c>); a missing name keeps the library's default. See DecompilerOptions.</summary>
    public IReadOnlyDictionary<string, string> Decompiler { get; init; } = new Dictionary<string, string>();

    /// <summary>Defaults of the data file comparison (<see cref="DiffOptions"/>).</summary>
    public DiffOptions Diff { get; init; } = new();

    /// <summary>Defaults of the image editor (<see cref="ImageOptions"/>).</summary>
    public ImageOptions Image { get; init; } = new();

    /// <summary>Playback of sounds (<see cref="AudioOptions"/>).</summary>
    public AudioOptions Audio { get; init; } = new();
    /// <summary>Defaults of the auto-tile brush (<see cref="AutoTileOptions"/>).</summary>
    public AutoTileOptions AutoTile { get; init; } = new();

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

    /// <summary>Working folder of the run; empty means the folder of the data file.</summary>
    public string RunWorkingDir { get; init; } = "";

    /// <summary>Environment variables of the run as <c>NAME=value</c> pairs separated by semicolons or line breaks.</summary>
    public string RunEnvironment { get; init; } = "";

    /// <summary>Run on the data file itself after saving it (with the usual backup) instead of on a working copy.</summary>
    public bool RunSaveInPlace { get; init; }

    /// <summary>Restart the running game by itself after an edit (<c>run.autoRestart</c>).</summary>
    public bool RunAutoRestart { get; init; }

    /// <summary>Seconds a stop waits for the game to close before it is killed.</summary>
    public int RunStopSeconds { get; init; } = 5;

    /// <summary>Named sets of the run options above; picking one in Settings copies its values into them.</summary>
    public IReadOnlyList<RunProfile> RunProfiles { get; init; } = [];

    /// <summary>SHA-256 of the project run files (<c>.g3mrun.json</c>) the owner has approved; a project file that is new or changed is ignored until approved, since it can name any program.</summary>
    public IReadOnlyList<string> TrustedRunFiles { get; init; } = [];

    /// <summary>The profile picked last, for the Settings page to show; empty when none.</summary>
    public string ActiveRunProfile { get; init; } = "";

    /// <summary>The run profile each data file used last, full path to profile name; opening the file picks it again.</summary>
    public IReadOnlyDictionary<string, string> FileRunProfiles { get; init; } = new Dictionary<string, string>();

    /// <summary>Shortcuts changed by the user, command id to gesture; an empty gesture means the command has none (<c>keybindings</c>).</summary>
    public IReadOnlyDictionary<string, string> Shortcuts { get; init; } = new Dictionary<string, string>();

    /// <summary>Extra gestures per command id; a listed command uses exactly this list (empty for none), an unlisted one keeps its default aliases.</summary>
    public IReadOnlyDictionary<string, string[]> ShortcutAliases { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Compact, Comfortable or Spacious: sets the control, row and tab heights, spacing and text sizes together; own sizes win over it.</summary>
    public string Density { get; init; } = UiSizing.Comfortable;

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

    /// <summary>The Explorer shows the user's folders as its main tree instead of grouping by type (a file can choose its own).</summary>
    public bool ExplorerFolderView { get; init; }

    public bool ExplorerShowCounts { get; init; } = true;

    /// <summary>Folders and collections start open in the Explorer.</summary>
    public bool ExplorerFoldersOpen { get; init; } = true;

    /// <summary>Applies to a data file that has no saved Explorer state yet.</summary>
    public ExplorerSort ExplorerSortOrder { get; init; } = ExplorerSort.ListOrder;

    /// <summary>One grid size for every room instead of each room's own (UTMT-SET-13); 1 to 1024.</summary>
    public bool RoomGridOverride { get; init; }

    public int RoomGridWidth { get; init; } = 16;

    public int RoomGridHeight { get; init; } = 16;

    public bool CodeLineNumbers { get; init; } = true;

    /// <summary>The completion list opens while typing in code; Ctrl+Space opens it either way.</summary>
    public bool CodeAutoComplete { get; init; } = true;

    /// <summary>Thin vertical lines at each indent level of the code text.</summary>
    public bool CodeIndentGuides { get; init; } = true;

    /// <summary>Columns of one tab and one indent step in the code text; 2 to 8.</summary>
    public int CodeTabWidth { get; init; } = 4;

    /// <summary>Brackets of the code text take a color by nesting level.</summary>
    public bool CodeBracketColors { get; init; } = true;

    /// <summary>Font ligatures in code and diff views; off by default.</summary>
    public bool CodeLigatures { get; init; }

    /// <summary>Squiggles for syntax problems while typing, after a pause of about 400 ms.</summary>
    public bool CodeLiveCheck { get; init; } = true;

    /// <summary>Colors changed by the user, <see cref="ThemeColors.Editable"/> key to #RRGGBB; a missing key keeps the theme color.</summary>
    public IReadOnlyDictionary<string, string> Colors { get; init; } = new Dictionary<string, string>();

    /// <summary>Sizes changed by the user, <see cref="ThemeSizes.All"/> key to value; a missing key keeps the theme size.</summary>
    public IReadOnlyDictionary<string, int> Sizes { get; init; } = new Dictionary<string, int>();

    /// <summary>Advanced: any theme token to a value of its own kind, written in settings.json (see ThemeOverrides); unknown tokens are ignored when applied.</summary>
    public IReadOnlyDictionary<string, string> Overrides { get; init; } = new Dictionary<string, string>();

    /// <summary>Dialog sizes the user gave by resizing, window type name to size; the dialog opens at that size again.</summary>
    public IReadOnlyDictionary<string, DialogSize> DialogSizes { get; init; } = new Dictionary<string, DialogSize>();

    internal AppSettings Clamped()
    {
        var project = Project ?? new(); // a hand-edited file may hold null
        return this with
        {
            SavedLayouts = SavedLayouts is null ? [] : SavedLayouts.Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value is not null).ToDictionary(),
            FileRunProfiles = FileRunProfiles is null ? [] : FileRunProfiles.Where(p => p.Key is not null && p.Value is not null).ToDictionary(),
            TrustedRunFiles = TrustedRunFiles is null ? [] : [.. TrustedRunFiles.Where(h => h is not null).Distinct()],
            RunProfiles = RunProfiles is null ? [] : [.. RunProfiles.Where(p => p is { Name.Length: > 0 }).DistinctBy(p => p.Name)],
            Shortcuts = Shortcuts is null ? new Dictionary<string, string>() : Shortcuts.Values.Any(v => v is null) ? Shortcuts.Where(p => p.Value is not null).ToDictionary() : Shortcuts, // a hand-edited file may hold null
            ShortcutAliases = ShortcutAliases is null ? [] : ShortcutAliases.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value.Where(a => a is not null).ToArray()),
            Colors = (Colors ?? new Dictionary<string, string>()).Where(p => ThemeColors.Editable.Contains(p.Key) && ThemeColors.IsValid(p.Value)).ToDictionary(),
            Sizes = ThemeSizes.Clamp(Sizes),
            Overrides = (Overrides ?? new Dictionary<string, string>()).Where(p => !string.IsNullOrWhiteSpace(p.Key) && p.Value is not null).ToDictionary(),
            Prompts = (Prompts ?? new Dictionary<string, string>()).Where(p => Enum.TryParse<PromptChoice>(p.Value, true, out var c) && c != PromptChoice.Ask && Settings.Prompts.IsKnown(p.Key, c)).ToDictionary(),
            ModBaseMaxMb = Math.Clamp(ModBaseMaxMb, 64, 1_048_576),
            BackupCount = Math.Clamp(BackupCount, 1, 3),
            RunStopSeconds = Math.Clamp(RunStopSeconds, 1, 60),
            UiScale = UiScale <= 0 ? 0 : Math.Clamp(UiScale, UiSizing.MinManual, UiSizing.MaxScale),
            Density = UiSizing.NormalDensity(Density),
            DialogSizes = (DialogSizes ?? new Dictionary<string, DialogSize>()).Where(p => p.Value is not null).ToDictionary(p => p.Key, p => new DialogSize(Math.Clamp(p.Value.Width, 200, 10000), Math.Clamp(p.Value.Height, 100, 10000))),
            MemoryLimitMb = MemoryLimitMb <= 0 ? 0 : Math.Clamp(MemoryLimitMb, 256, 1_048_576),
            CodeFontSize = Math.Clamp(CodeFontSize, 8, 32),
            Image = (Image ?? new()).Clamped(),
            Audio = (Audio ?? new()).Clamped(),
            AutoTile = (AutoTile ?? new()).Clamped(),
            Diff = (Diff ?? new()).Clamped(),
            Project = project with { AutoSaveSeconds = Math.Clamp(project.AutoSaveSeconds, 1, 3600), AutoSyncSeconds = Math.Clamp(project.AutoSyncSeconds, 1, 3600) },
        };
    }
}

public enum RunOptionsLocation
{
    StudioFolder,
    BesideDataFile,
}

public sealed record RunProfile(string Name, string Runner, string Arguments, bool DebugOutput, string WorkingDir = "", string Environment = "");

/// <summary>Reads and writes <c>settings.json</c> in the user data folder; every change is saved at once and announced.</summary>
public sealed class SettingsStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    /// <summary>The settings file, for a command that shows it to the user.</summary>
    public string FilePath { get; }

    /// <summary>Studio's own data folder, for files kept per data file (run options, the mod base cache).</summary>
    public UserData UserData { get; }

    public SettingsStore(UserData userData)
    {
        UserData = userData;
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

/// <summary>The size of a dialog window in device-independent units, remembered by window type.</summary>
public sealed record DialogSize(int Width, int Height);

/// <summary>What Studio does where it would refuse a risky action: refuse, warn and let the owner go on, or go on without asking.</summary>
public enum RiskChoice
{
    Refuse,
    Ask,
    Allow,
}

public static class RiskPolicy
{
    public static RiskChoice Of(AppSettings s) => s.AllowUnsafeSilently ? RiskChoice.Allow : s.AskRiskyActions ? RiskChoice.Ask : RiskChoice.Refuse;
}
