using Avalonia.Platform.Storage;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Export and import of resources as files, with progress and cancel, and Revert to Saved.</summary>
public sealed partial class MainWindow
{
    /// <summary>Replaces the folder dialog (the argument is its title; null when cancelled). Tests set it.</summary>
    public Func<string, Task<string?>>? FolderPicker { get; set; }

    /// <summary>Replaces the yes or no dialog (title, message). Tests set it.</summary>
    public Func<string, string, Task<bool>>? Confirmer { get; set; }

    private Task<string?> PickFolder(string title) => FolderPicker?.Invoke(title) ?? PickFolderAsync(title);

    private Task<bool> Confirm(string title, string message) => Confirmer?.Invoke(title, message) ?? ProjectDialogs.AskConfirmAsync(this, title, message);

    private async Task<string?> PickFolderAsync(string title)
    {
        if (!StorageProvider.CanPickFolder) return null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        return folders is [{ } folder, ..] ? folder.TryGetLocalPath() : null;
    }

    public void ExportSelection() => _ = ExportSelectionAsync();

    public void ExportAll() => _ = ExportAllAsync();

    public void ImportResources() => _ = ImportAsync();

    public void RevertToSaved() => _ = RevertAsync();

    /// <summary>Exports the entry selected in the Explorer, or the one in the tab in front, to a folder.</summary>
    public async Task ExportSelectionAsync()
    {
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (!ResourceTransfer.Supports(type))
        {
            Notify(NotificationLevel.Info, Loc.Current.Format("transfer.unsupported", ("type", Loc.Current["explorer.type." + type])));
            return;
        }

        if (await PickFolder(Loc.Current["transfer.pickExport"]) is not { } folder) return;
        // Extensions and the general info are single entries; the others export the one named entry.
        ISet<string>? names = type is "extensions" or "general" ? null : new HashSet<string> { name };
        await RunOperationAsync(name, (progress, token) => ResourceTransfer.ExportAsync(workspace, folder, type, names, progress, token), folder, export: true);
    }

    public async Task ExportAllAsync()
    {
        if (Workspaces.Current is not { } workspace || await PickFolder(Loc.Current["transfer.pickExport"]) is not { } folder) return;
        await RunOperationAsync(workspace.Name, (progress, token) => ResourceTransfer.ExportAllAsync(workspace, folder, progress, token), folder, export: true);
    }

    /// <summary>Imports the resource folders of a chosen directory. It cannot be undone, so it needs a workspace without unsaved changes.</summary>
    public async Task ImportAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["transfer.needsSave"]);
            return;
        }

        if (await PickFolder(Loc.Current["transfer.pickImport"]) is not { } folder) return;
        var folders = ResourceTransfer.FoldersIn(folder);
        if (folders.Count == 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current.Format("transfer.noFolders", ("folder", folder)));
            return;
        }

        if (!await Confirm(Loc.Current["transfer.confirmTitle"], Loc.Current.Format("transfer.confirmImport", ("folders", string.Join(", ", folders)), ("folder", folder)))) return;
        await RunOperationAsync(workspace.Name, async (progress, token) =>
        {
            IReadOnlyList<ItemResult> results = [];
            try
            {
                // The write lock is held on a pool thread; the change cannot be undone, so it ends in a bulk change whatever happens.
                await workspace.ApplyBulkAsync(() => results = ResourceTransfer.Import(workspace, folder, folders, progress, token));
            }
            catch (OperationCanceledException)
            {
                Notify(NotificationLevel.Warning, Loc.Current["transfer.cancelledImport"]);
                throw;
            }

            return results;
        }, folder, export: false);
    }

    /// <summary>Drops the unsaved changes and loads the file again, after the user agrees.</summary>
    public async Task RevertAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (!workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Info, Loc.Current["transfer.nothingToRevert"]);
            return;
        }

        if (!await Confirm(Loc.Current["transfer.revertTitle"], Loc.Current.Format("transfer.confirmRevert", ("name", Path.GetFileName(workspace.Path)), ("count", workspace.History.UnsavedCount)))) return;
        var path = workspace.Path;
        Workspaces.Close();
        File.Delete(Journal.FileFor(Workspaces.UserData, path));
        await OpenAsync(path);
    }

    /// <summary>Runs a batch with the progress overlay and its Cancel button, then reports one summary and lists what did not go well.</summary>
    private async Task RunOperationAsync(string name, Func<IProgress<BatchProgress>, CancellationToken, Task<IReadOnlyList<ItemResult>>> run, string folder, bool export)
    {
        using var source = _exporting = new CancellationTokenSource();
        (_exportName, _exportDone, _exportTotal) = (name, 0, 0);
        ShowProgress();
        try
        {
            var results = await run(new Progress<BatchProgress>(p => { (_exportDone, _exportTotal) = (p.Done, p.Total); ShowProgress(); }), source.Token);
            foreach (var problem in results.Where(r => r.Kind is ResultKind.Failed or ResultKind.Warning))
                Notifications.Post(problem.Kind == ResultKind.Failed ? NotificationLevel.Error : NotificationLevel.Warning, $"{problem.Name}: {problem.Message}");
            var summary = Loc.Current.Format("transfer.summary", ("written", results.Count(r => r.Kind == ResultKind.Written)), ("skipped", results.Count(r => r.Kind == ResultKind.Skipped)),
                ("warnings", results.Count(r => r.Kind == ResultKind.Warning)), ("failed", results.Count(r => r.Kind == ResultKind.Failed)));
            var level = results.Any(r => r.Kind == ResultKind.Failed) ? NotificationLevel.Warning : NotificationLevel.Info;
            Notify(level, Loc.Current.Format(export ? "transfer.exportDone" : "transfer.importDone", ("folder", folder)) + " " + summary);
        }
        catch (OperationCanceledException)
        {
            if (export) Notify(NotificationLevel.Info, Loc.Current["transfer.cancelledExport"]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("transfer.failed", ("reason", e.Message)));
        }
        finally
        {
            _exporting = null;
            _exportName = null;
            Progress.IsVisible = false;
            UpdateTexts();
        }
    }
}
