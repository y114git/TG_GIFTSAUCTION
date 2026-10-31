using G3MLib.DataFile;
using G3MLib.DataFile.Project;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>
/// The project of one open data file (ADR-0010 Project mode). It writes changed resources into the project folder (autosave), syncs the
/// data file (autosync, Sync, Save) and takes a snapshot on every sync. Both writes read the model on a worker thread under the model
/// read lock while edits wait in the workspace queue.
/// </summary>
public sealed partial class ProjectLink
{
    private readonly Workspace.Workspace _workspace;
    private readonly ProjectRegistry _registry;
    private readonly UserData _userData;
    private readonly HashSet<(string Type, string? Name)> _touched = [];
    private bool _full, _syncing;
    private DateTimeOffset? _lastChange;
    private DateTimeOffset? _lastSyncStart;

    internal ProjectLink(Workspace.Workspace workspace, ProjectRecord record, ProjectRegistry registry, UserData userData, bool dataFileChanged)
    {
        _workspace = workspace;
        Record = record;
        _registry = registry;
        _userData = userData;
        DataFileChanged = dataFileChanged;
        // A crash may have left the project behind the model (recovered edits) or half written.
        _full = workspace.RecoveredEdits > 0 || record.Writing;
        workspace.History.Recorded += OnRecorded;
        workspace.BulkApplied += OnBulkApplied;
        workspace.History.Changed += () => Changed?.Invoke();
    }

    public ProjectRecord Record { get; private set; }

    public string Folder => Record.Folder;

    public string ProjectFile => Path.Combine(Folder, Record.Name + G3MProjectService.FileExtension);

    public ProjectSettings Settings => Record.Settings;

    /// <summary>The data file differs from the one the project was last synced with (patched by another tool, game update).</summary>
    public bool DataFileChanged { get; private set; }

    /// <summary>An export or sync is running; edits wait.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>Edits that are not in the data file yet.</summary>
    public int NotSynced => _workspace.History.UnsavedCount;

    /// <summary>Changes that are not in the project folder yet.</summary>
    public bool HasPendingWrite => _full || _touched.Count > 0;

    public ProjectState State =>
        _syncing ? ProjectState.Syncing : IsBusy ? ProjectState.Exporting : DataFileChanged ? ProjectState.DataFileChanged : NotSynced > 0 ? ProjectState.NotSynced : ProjectState.Synced;

    public event Action? Changed;

    public SnapshotStore Snapshots => new(_userData.Combine("projects", Record.Id));

    private void OnBulkApplied()
    {
        _full = true;
        _lastChange = DateTimeOffset.Now;
    }

    private void OnRecorded(string op, EditCommand command)
    {
        if (command.Touches is { } touches)
            foreach (var touch in touches) _touched.Add((touch.Type, touch.Name));
        else
            _full = true;
        _lastChange = DateTimeOffset.Now;
    }

    /// <summary>Problems the library reported while writing the project (a resource that could not be exported is left out).</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>The first export of a new project: every resource, in the background, with progress and cancel.</summary>
    internal async Task ExportAllAsync(IProgress<BatchProgress>? progress, CancellationToken cancellationToken) =>
        await Exclusive(() =>
        {
            SetWriting(true);
            var result = G3MProjectService.Export(_workspace.Data, Folder, _workspace.Path, new G3MProjectOptions { Name = Record.Name }, progress, cancellationToken);
            Warnings.AddRange(result.Warnings);
            SetWriting(false);
            SaveBase();
            return 0;
        }, cancellationToken);

    /// <summary>Autosave: writes the resources changed since the last write. Returns how many resources were written.</summary>
    public async Task<int> SaveToProjectAsync(CancellationToken cancellationToken = default)
    {
        if (!HasPendingWrite) return 0;
        var full = _full;
        var touches = _touched.ToList();
        _full = false;
        _touched.Clear();
        try
        {
            return await Exclusive(() => WriteChanges(full, touches, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The library leaves the folder as it was, so the same changes are written next time.
            _full |= full;
            _touched.UnionWith(touches);
            throw;
        }
        catch
        {
            // Unknown state: the next write redoes everything.
            _full = true;
            throw;
        }
    }

    private int WriteChanges(bool full, List<(string Type, string? Name)> touches, CancellationToken cancellationToken)
    {
        SetWriting(true);
        var result = full
            ? G3MProjectService.Export(_workspace.Data, Folder, _workspace.Path, new G3MProjectOptions { Name = Record.Name }, null, cancellationToken)
            : G3MProjectService.Update(_workspace.Data, Folder, null, UpdateOptions(touches), null, cancellationToken);
        Warnings.AddRange(result.Warnings);
        SetWriting(false);
        return result.Resources;
    }

    private G3MProjectOptions UpdateOptions(IEnumerable<(string Type, string? Name)> touches) => new()
    {
        Name = Record.Name,
        Only = touches.GroupBy(t => t.Type).ToDictionary(g => g.Key, g => (IReadOnlySet<string>)g.Select(t => t.Name).OfType<string>().ToHashSet()),
    };

    /// <summary>Saves to the project, then writes the data file from memory and takes a snapshot when that worked.</summary>
    public async Task<SaveResult> SyncAsync(SavePipeline pipeline, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsBusy) throw new InvalidOperationException("The project is busy.");
        if (DataFileChanged) throw new InvalidOperationException("The data file changed outside. Choose how to bring in or replace its changes first.");
        await SaveToProjectAsync(cancellationToken);
        _syncing = true;
        _lastSyncStart = DateTimeOffset.Now;
        Changed?.Invoke();
        try
        {
            var result = await _workspace.SaveAsync(pipeline, progress, cancellationToken);
            if (!result.Saved) return result;
            Record = Record with { Synced = _workspace.Identity };
            _registry.Put(Record);
            DataFileChanged = false;
            var identity = _workspace.Identity;
            await Task.Run(() =>
            {
                // Nothing to write: the call only records the new data file as the base file.
                using (_workspace.Lock.Read()) G3MProjectService.Update(_workspace.Data, Folder, identity.Path, UpdateOptions([]));
                RecordSync("sync");
            }, CancellationToken.None);
            return result;
        }
        finally
        {
            _syncing = false;
            Changed?.Invoke();
        }
    }

    /// <summary>The project as it is now becomes the base of the next three-way merge, and one snapshot is taken.</summary>
    private void RecordSync(string label)
    {
        Snapshots.Create(G3MProjectService.OpenAsync(ProjectFile).GetAwaiter().GetResult(), label, DateTimeOffset.Now);
        SaveBase();
    }

    private string BaseFile => _userData.Combine("projects", Record.Id, "base.g3mproj");

    private void SaveBase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BaseFile)!);
        File.Copy(ProjectFile, BaseFile, true);
    }

    /// <summary>Save in Project mode: the project is written; the data file too when autosync is set to On Save.</summary>
    public async Task<SaveResult?> SaveAsync(SavePipeline pipeline, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (Settings.AutoSync == Trigger.OnSave) return await SyncAsync(pipeline, progress, cancellationToken);
        await SaveToProjectAsync(cancellationToken);
        return null;
    }

    /// <summary>Call about once a second: runs the writes whose timer ran out.</summary>
    public async Task TickAsync(DateTimeOffset now, SavePipeline pipeline)
    {
        if (IsBusy || _syncing) return;
        if (Settings is { AutoSave: Trigger.AfterDelay } && HasPendingWrite && _lastChange is { } changed && now - changed >= TimeSpan.FromSeconds(Settings.AutoSaveSeconds))
            await SaveToProjectAsync();
        if (Settings is { AutoSync: Trigger.AfterDelay } && !DataFileChanged && NotSynced > 0 && (_lastSyncStart is null || now - _lastSyncStart >= TimeSpan.FromSeconds(Settings.AutoSyncSeconds)) && _lastChange is { } last && now - last >= TimeSpan.FromSeconds(Settings.AutoSyncSeconds))
            await SyncAsync(pipeline);
    }

    /// <summary>The window lost focus: writes what the autosave or autosync setting ties to that event.</summary>
    public async Task FocusLostAsync(SavePipeline pipeline)
    {
        if (IsBusy || _syncing) return;
        if (Settings.AutoSync == Trigger.OnFocusLoss && NotSynced > 0 && !DataFileChanged) await SyncAsync(pipeline);
        else if (Settings.AutoSave == Trigger.OnFocusLoss) await SaveToProjectAsync();
    }

    public void UpdateSettings(ProjectSettings settings)
    {
        Record = Record with { Settings = settings };
        _registry.Put(Record);
        Changed?.Invoke();
    }

    /// <summary>
    /// Q50: the data file moves to <paramref name="newPath"/>. Autosave and autosync run first. Move copies, checks the size and removes the
    /// original; Copy leaves it; Recreate builds the file from the project and leaves the original alone. Afterwards the workspace,
    /// the project record and the journal point at the new path.
    /// </summary>
    public async Task RelocateAsync(string newPath, RelocateMode mode, SavePipeline pipeline, CancellationToken cancellationToken = default)
    {
        newPath = Path.GetFullPath(newPath);
        if (File.Exists(newPath)) throw new IOException($"'{newPath}' already exists.");
        if (_workspace.IsDirty || HasPendingWrite)
        {
            var result = await SyncAsync(pipeline, null, cancellationToken);
            if (!result.Saved) throw new IOException(result.Message ?? "The data file could not be written before the move.");
        }

        var old = _workspace.Path;
        Directory.CreateDirectory(Path.GetDirectoryName(newPath) ?? ".");
        await Task.Run(() =>
        {
            if (mode == RelocateMode.Recreate) G3MProjectService.Build(ProjectFile, old, newPath);
            else File.Copy(old, newPath);
        }, cancellationToken);
        if (mode == RelocateMode.Move)
        {
            if (new FileInfo(newPath).Length != new FileInfo(old).Length) throw new IOException("The copy has another size than the original; the original was kept.");
            File.Delete(old);
        }

        await _workspace.RelocateAsync(newPath, _userData);
        Record = Record with { Synced = _workspace.Identity };
        _registry.Put(Record);
        DataFileChanged = false;
        Changed?.Invoke();
    }

    private async Task<T> Exclusive<T>(Func<T> work, CancellationToken cancellationToken)
    {
        if (IsBusy) throw new InvalidOperationException("The project is busy.");
        IsBusy = true;
        _workspace.HoldEdits();
        Changed?.Invoke();
        try
        {
            return await Task.Run(() =>
            {
                using (_workspace.Lock.Read()) return work();
            }, cancellationToken);
        }
        finally
        {
            IsBusy = false;
            _workspace.ReleaseEdits();
            Changed?.Invoke();
        }
    }

    // A write that died half way is redone in full at the next open.
    private void SetWriting(bool writing)
    {
        Record = Record with { Writing = writing };
        _registry.Put(Record);
    }
}
