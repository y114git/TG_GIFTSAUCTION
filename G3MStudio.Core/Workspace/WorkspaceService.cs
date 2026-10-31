using G3MLib.DataFile;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Settings;

namespace G3MStudio.Core.Workspace;

/// <summary>Opens and closes the single workspace of a window and keeps the recent list.</summary>
public sealed class WorkspaceService(UserData userData, RecentFiles recent)
{
    public UserData UserData { get; } = userData;

    public RecentFiles Recent { get; } = recent;

    public ProjectService Projects { get; } = new(userData);

    public SettingsStore Settings => Projects.Settings;

    public Workspace? Current { get; private set; }

    public event Action<Workspace?>? CurrentChanged;

    private string? _lastActive;

    /// <summary>The data file remembers the run profile picked while it is open (F-UI-6, the per-file part of the settings).</summary>
    private void OnSettingsChanged()
    {
        var active = Settings.Current.ActiveRunProfile;
        if (active == _lastActive) return;
        _lastActive = active;
        if (Current is not { } workspace || active.Length == 0 || Settings.Current.FileRunProfiles.GetValueOrDefault(workspace.Path) == active) return;
        Settings.Update(x => x with { FileRunProfiles = new Dictionary<string, string>(x.FileRunProfiles) { [workspace.Path] = active } });
    }

    /// <summary>Picks the profile the file used last, when that profile still exists.</summary>
    private void ApplyRunProfile(Workspace workspace)
    {
        if (_lastActive is null)
        {
            _lastActive = Settings.Current.ActiveRunProfile;
            Settings.Changed += OnSettingsChanged;
        }

        var settings = Settings.Current;
        if (!settings.FileRunProfiles.TryGetValue(workspace.Path, out var name) || settings.RunProfiles.FirstOrDefault(p => p.Name == name) is not { } profile) return;
        _lastActive = name;
        Settings.Update(x => x with { ActiveRunProfile = name, RunRunner = profile.Runner, RunArguments = profile.Arguments, RunDebugOutput = profile.DebugOutput });
    }

    /// <summary>Reads the file with progress. The previous workspace is closed only after the new one has loaded.</summary>
    /// <exception cref="OperationCanceledException">Cancelled; nothing changes.</exception>
    public async Task<Workspace> OpenAsync(string path, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("Data file not found.", full);

        var warnings = new List<string>();
        var read = GameMakerIO.ReadAsync(full, progress ?? NoProgress.Instance, cancellationToken, (warning, _) => { lock (warnings) warnings.Add(warning); });
        var identity = DataFileIdentity.ComputeAsync(full, cancellationToken);
        await Task.WhenAll(read, identity);
        var data = await read;

        // Closed first: the same file reopened would find its own journal still held.
        Close();
        var workspace = new Workspace(full, await identity, data, warnings);
        workspace.StartJournal(UserData);
        Projects.Attach(workspace);
        Current = workspace;
        ApplyRunProfile(workspace);
        Recent.Add(full, workspace.Name, workspace.Version, DateTimeOffset.Now);
        CurrentChanged?.Invoke(workspace);
        return workspace;
    }

    public void Close()
    {
        var old = Current;
        if (old is null) return;
        Current = null;
        old.Dispose();
        CurrentChanged?.Invoke(null);
    }
}
