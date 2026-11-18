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

    /// <summary>Rotating backups of the data file, 1 to <see cref="MaxBackups"/> (<c>files.backupCount</c>).</summary>
    public int BackupCount { get; init; } = 3;

    // each backup is a full copy of the data file, and every save renames each one
    public const int MaxBackups = 100;

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

    /// <summary>The most memory in MB that the undo steps may keep (pictures and audio); the oldest steps are dropped past it.</summary>
    public int UndoLimitMb { get; init; } = 256;

    /// <summary>Size cap of the base cache in MB (<c>modBase.maxMb</c>); the oldest copies go first.</summary>
    public int ModBaseMaxMb { get; init; } = 2048;

    /// <summary>Where the main window was when it was closed; null before the first close.</summary>
    public WindowPlacement? Window { get; init; }

    /// <summary>What the next start does (<c>startup.mode</c>): reopen where the last run ended, show Home, or ask. A restore that fails goes to Home.</summary>
    public StartupMode Startup { get; init; } = StartupMode.Restore;

    /// <summary>The data file open when Studio last closed (or opened a file), with its size and MD5 at that moment; null when Home was showing.</summary>
    public DataFileIdentity? LastSession { get; init; }

    /// <summary>When a remembered data file is lost, look for it (same hash) in its old folder, the folders around it and the folders of recent files (<c>lostFile.search</c>).</summary>
    public bool LostFileSearch { get; init; } = true;

    /// <summary>When the search finds exactly one file with the remembered hash, use it without asking (<c>lostFile.autoUse</c>).</summary>
    public bool LostFileAutoUse { get; init; } = true;

    /// <summary>What happens when the picked file has another hash than the remembered one (<c>lostFile.mismatch</c>).</summary>
    public MismatchPolicy HashMismatch { get; init; } = MismatchPolicy.Ask;

    /// <summary>Take the old entry out of the recent list when a replacement file was chosen for it (<c>lostFile.forgetOld</c>).</summary>
    public bool LostFileForgetOld { get; init; } = true;

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

    /// <summary>Sizes the user dragged, key to size in device-independent pixels: the height of the property list under an editor is <c>properties.</c> and the resource type.</summary>
    public IReadOnlyDictionary<string, double> PaneSizes { get; init; } = new Dictionary<string, double>();

    /// <summary>The first-launch welcome (language) was shown and answered or skipped.</summary>
    public bool OnboardingDone { get; init; }

    /// <summary>Send anonymous crash reports (releases with a key only); on by default, switched in the welcome window and Settings > Privacy.</summary>
    public bool CrashReports { get; init; } = true;

    /// <summary>Send anonymous usage statistics (session length, speed, which features are used); on by default like <see cref="CrashReports"/> and only sent while that one is on.</summary>
    public bool UsageStats { get; init; } = true;

    /// <summary>A random id made once on this PC; it only tells one install from another in the reports and is made from nothing about the machine or the person. Empty until the first start makes it.</summary>
    public string InstallId { get; init; } = "";

    /// <summary>The old Reduce motion checkbox, read only to migrate saved settings: when it is set and no level was chosen, the level is Off.</summary>
    public bool ReduceMotion { get; init; }

    /// <summary>Full, Medium, Minimal or Off (<see cref="MotionPolicy"/>); empty means not chosen: Medium, or Off when the OS asks for reduced motion.</summary>
    public string Animations { get; init; } = "";

    /// <summary>What <see cref="Animations"/> and the legacy checkbox together choose; empty when the user chose nothing.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ChosenAnimations => MotionPolicy.Normal(Animations).Length == 0 && ReduceMotion ? MotionPolicy.Off : Animations;

    /// <summary>Where the previous and next tab buttons sit (<c>ui.tabButtons</c>).</summary>
    public TabButtonsPlace TabButtons { get; init; } = TabButtonsPlace.MenuLeft;

    /// <summary>Which explanatory text the editors and dialogs show: All, Hints (how to use something), Details (facts about the current thing) or Off. Errors, warnings and questions always show.</summary>
    public HelpLevel HelpLevel { get; init; } = HelpLevel.All;

    /// <summary>Size of the text in code tabs, 8 to 32.</summary>
    public int CodeFontSize { get; init; } = 13;

    /// <summary>The font family of code views; empty uses the theme's code font.</summary>
    public string CodeFontFamily { get; init; } = "";

    public bool CodeWordWrap { get; init; }

    /// <summary>The rename dialog's "Also rename matching strings": code string literals equal to the old name follow a resource rename (the way renames always worked).</summary>
    public bool RenameStrings { get; init; } = true;

    /// <summary>Lists the null entries of resource lists in the Explorer (UTMT-SET-09).</summary>
    public bool ShowNullEntries { get; init; }

    /// <summary>Hovering a resource shows a card with its picture and ID, wherever a resource is named (item 40). A settings file from before the option has no value and gets the default.</summary>
    public bool HoverPreviews { get; init; } = true;

    /// <summary>The Explorer shows the user's folders as its main tree instead of grouping by type (a file can choose its own).</summary>
    public bool ExplorerFolderView { get; init; }

    public bool ExplorerShowCounts { get; init; } = true;

    /// <summary>The Explorer leaves out code (and the scripts using it) that has no text of its own: child entries and what the decompiler fails on.</summary>
    public bool ExplorerHideUndecompilable { get; init; }

    /// <summary>The Explorer leaves out resources that point at nothing, such as a sprite frame without its picture.</summary>
    public bool ExplorerHideBroken { get; init; }

    /// <summary>Folders and collections start open in the Explorer.</summary>
    public bool ExplorerFoldersOpen { get; init; } = true;

    /// <summary>The bar under the Explorer list (count, thumbnails, zoom) is folded to a strip.</summary>
    public bool ExplorerOptionsFolded { get; init; }

    /// <summary>The size in pixels of a row's picture in the Explorer at 100 percent zoom (16 to 96); the zoom slider under the list scales it with the text. A settings file from before the option gets the default.</summary>
    public int ExplorerThumbnailSize { get; init; } = DefaultThumbnailSize;

    public const int DefaultThumbnailSize = 22;

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

    /// <summary>Milliseconds the pointer rests on a name before a tooltip or the info card shows; 300 to 3000.</summary>
    public int CodeHoverDelayMs { get; init; } = 1000;

    /// <summary>Names of the functions starred in the Function Reference and the completion list.</summary>
    public IReadOnlyList<string> FavouriteFunctions { get; init; } = [];

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
            UndoLimitMb = Math.Clamp(UndoLimitMb, 16, 1_048_576),
            BackupCount = Math.Clamp(BackupCount, 1, MaxBackups),
            RunStopSeconds = Math.Clamp(RunStopSeconds, 1, 60),
            UiScale = UiScale <= 0 ? 0 : Math.Clamp(UiScale, UiSizing.MinManual, UiSizing.MaxScale),
            Density = UiSizing.NormalDensity(Density),
            HelpLevel = Enum.IsDefined(HelpLevel) ? HelpLevel : HelpLevel.All,
            Animations = MotionPolicy.Normal(Animations),
            DialogSizes = (DialogSizes ?? new Dictionary<string, DialogSize>()).Where(p => p.Value is not null).ToDictionary(p => p.Key, p => new DialogSize(Math.Clamp(p.Value.Width, 200, 10000), Math.Clamp(p.Value.Height, 100, 10000))),
            MemoryLimitMb = MemoryLimitMb <= 0 ? 0 : Math.Clamp(MemoryLimitMb, 256, 1_048_576),
            CodeFontSize = Math.Clamp(CodeFontSize, 8, 32),
            ExplorerThumbnailSize = Math.Clamp(ExplorerThumbnailSize, 16, 96),
            Image = (Image ?? new()).Clamped(),
            Audio = (Audio ?? new()).Clamped(),
            AutoTile = (AutoTile ?? new()).Clamped(),
            Diff = (Diff ?? new()).Clamped(),
            Project = project with { AutoSaveSeconds = Math.Clamp(project.AutoSaveSeconds, 1, 3600), AutoSyncSeconds = Math.Clamp(project.AutoSyncSeconds, 1, 3600) },
        };
    }
}

/// <summary>The explanatory text a page shows: hints say how to use something ("Drag on the waveform to select a region"), details tell about the current thing ("Storage: Regular, volume 1").</summary>
public enum HelpLevel
{
    All,
    Hints,
    Details,
    Off,
}

public enum TabButtonsPlace
{
    MenuLeft,
    MenuRight,
    Hidden,
}

/// <summary>The start behaviour: reopen the last session, always show Home, or ask each time.</summary>
public enum StartupMode
{
    Restore,
    Home,
    Ask,
}

/// <summary>What to do with a data file whose hash differs from the one remembered: ask, use it and keep the saved layout, use it with a fresh layout, or refuse it.</summary>
public enum MismatchPolicy
{
    Ask,
    KeepLayout,
    FreshLayout,
    Refuse,
}

public enum RunOptionsLocation
{
    StudioFolder,
    BesideDataFile,
}

public sealed record RunProfile(string Name, string Runner, string Arguments, bool DebugOutput, string WorkingDir = "", string Environment = "");

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
