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

    /// <summary>Ask before closing with unsaved changes (<c>files.closeUnsaved</c> = ask); off keeps them in the journal without asking.</summary>
    public bool AskOnClose { get; init; } = true;

    /// <summary>Empty means the theme font.</summary>
    public string FontFamily { get; init; } = "";

    /// <summary>A font weight name, or empty for the theme weight.</summary>
    public string FontWeight { get; init; } = "";

    /// <summary>Percent, 50 to 200 (<c>ui.zoom</c>).</summary>
    public int UiScale { get; init; } = 100;

    public string Language { get; init; } = "en";

    /// <summary>A runner to start instead of the game executable next to the data file (for example a GameMaker runtime's Runner.exe); empty means the game executable.</summary>
    public string RunRunner { get; init; } = "";

    /// <summary>Extra arguments for the runner, split like a shell would (quotes group words).</summary>
    public string RunArguments { get; init; } = "";

    /// <summary>Adds <c>-debugoutput</c> so the game writes its log next to the temporary file.</summary>
    public bool RunDebugOutput { get; init; }

    /// <summary>Named sets of the three run options above; picking one in Settings copies its values into them.</summary>
    public IReadOnlyList<RunProfile> RunProfiles { get; init; } = [];

    /// <summary>The profile picked last, for the Settings page to show; empty when none.</summary>
    public string ActiveRunProfile { get; init; } = "";

    /// <summary>Shortcuts changed by the user, command id to gesture; an empty gesture means the command has none (<c>keybindings</c>).</summary>
    public IReadOnlyDictionary<string, string> Shortcuts { get; init; } = new Dictionary<string, string>();

    /// <summary>Tighter rows, buttons and boxes, for more on screen.</summary>
    public bool CompactDensity { get; init; }

    /// <summary>Size of the text in code tabs, 8 to 32.</summary>
    public int CodeFontSize { get; init; } = 13;

    public bool CodeWordWrap { get; init; }

    public bool CodeLineNumbers { get; init; } = true;

    /// <summary>Colors changed by the user, <see cref="ThemeColors.Editable"/> key to #RRGGBB; a missing key keeps the theme color.</summary>
    public IReadOnlyDictionary<string, string> Colors { get; init; } = new Dictionary<string, string>();

    internal AppSettings Clamped()
    {
        var project = Project ?? new(); // a hand-edited file may hold null
        return this with
        {
            RunProfiles = RunProfiles is null ? [] : [.. RunProfiles.Where(p => p is { Name.Length: > 0 }).DistinctBy(p => p.Name)],
            Shortcuts = Shortcuts is null ? new Dictionary<string, string>() : Shortcuts.Values.Any(v => v is null) ? Shortcuts.Where(p => p.Value is not null).ToDictionary() : Shortcuts, // a hand-edited file may hold null
            Colors = (Colors ?? new Dictionary<string, string>()).Where(p => ThemeColors.Editable.Contains(p.Key) && ThemeColors.IsValid(p.Value)).ToDictionary(),
            BackupCount = Math.Clamp(BackupCount, 1, 3),
            UiScale = Math.Clamp(UiScale, 50, 200),
            CodeFontSize = Math.Clamp(CodeFontSize, 8, 32),
            Project = project with { AutoSaveSeconds = Math.Clamp(project.AutoSaveSeconds, 1, 3600), AutoSyncSeconds = Math.Clamp(project.AutoSyncSeconds, 1, 3600) },
        };
    }
}

/// <summary>A saved set of run options.</summary>
public sealed record RunProfile(string Name, string Runner, string Arguments, bool DebugOutput);

/// <summary>Reads and writes <c>settings.json</c> in the user data folder; every change is saved at once and announced.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    private readonly string _file;

    public SettingsStore(UserData userData)
    {
        _file = userData.Combine("settings.json");
        if (!File.Exists(_file)) return;
        try
        {
            Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), Json)?.Clamped() ?? Current;
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
            File.Move(_file, _file + ".invalid", true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public AppSettings Current { get; private set; } = new();

    public event Action? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var next = change(Current).Clamped();
        if (next == Current) return;
        UserData.WriteAllTextAtomic(_file, JsonSerializer.Serialize(next, Json) + "\n"); // a failed write leaves the old settings in place
        Current = next;
        Changed?.Invoke();
    }
}
