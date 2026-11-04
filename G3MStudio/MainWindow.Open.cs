using Avalonia.Controls;
using Avalonia.Platform.Storage;
using G3MLib.DataFile;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

public sealed partial class MainWindow
{
    private async Task PickAndOpenAsync()
    {
        if (!StorageProvider.CanOpen) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("GameMaker data") { Patterns = ["*.win", "*.unx", "*.ios", "*.droid", "audiogroup*.dat"] }, FilePickerFileTypes.All],
        });
        if (files is [{ } file, ..] && file.TryGetLocalPath() is { } path) await OpenAsync(path);
    }

    private string? FrontTabKey() => Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null;

    /// <summary>Pictures, sounds and GML files dropped on an open file become resources; otherwise the first file opens and the others are ignored.</summary>
    public void OpenDropped(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        if (Workspaces.Current is { } open && list is [var one] && FrontTabKey() is { } front && ResourceTypes.SplitKey(front) is var (frontType, frontName))
        {
            // One picture on a sprite tab goes onto the selected frame, one sound file on a sound tab replaces its audio; anywhere else a file makes a new resource.
            if (frontType == "sprites" && FileImport.TypeOf(one) == "sprites" && Docking.ActiveTabId is { } sid && _frameViews.TryGetValue(sid, out var frames))
            {
                _ = ReplaceFrameFromAsync(open, frontName, frames.SelectedFrame, one);
                return;
            }

            if (frontType == "sounds" && FileImport.TypeOf(one) == "sounds")
            {
                _ = ReplaceAudioFromAsync(open, frontName, one);
                return;
            }
        }

        if (Workspaces.Current is not null && list.Any(p => FileImport.TypeOf(p) is not null)) ImportFilePaths(list);
        else if (list.FirstOrDefault() is { } path) _ = OpenAsync(path);
    }

    public async Task OpenAsync(string path, bool askAboutUnsaved = true)
    {
        // Opening another file closes the current one: unsaved edits get the same question as on close.
        if (askAboutUnsaved && Workspaces.Current is { IsDirty: true } current && current.Path != Path.GetFullPath(path) && !await ConfirmCloseAsync()) return;
        var previous = _opening;
        var source = _opening = new CancellationTokenSource(); // takes over first, so a request that comes in meanwhile cancels this one
        if (previous is not null) await previous.CancelAsync();
        (_openingName, _openingFraction) = (Path.GetFileName(path), 0);
        ShowProgress();
        try
        {
            await Workspaces.OpenAsync(path, new Progress<GameMakerProgress>(p => { _openingFraction = p.Fraction; ShowProgress(); }), source.Token);
            if (Workspaces.Current is { } opened)
            {
                ApplyDecompiler(Workspaces.Settings.Current.Decompiler);
                foreach (var (key, count) in LoadChecks.Find(opened.Data)) Notify(NotificationLevel.Warning, Loc.Current.Format(key, ("count", count)));
            }
        }
        catch (OperationCanceledException)
        {
            // The user (or a newer open) cancelled; nothing to report.
        }
        catch (Exception e)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("app.openFailed", ("name", Path.GetFileName(path)), ("reason", e.Message)));
            _ = OfferRebuildAsync(path);
        }
        finally
        {
            if (_opening == source)
            {
                _opening = null;
                Progress.IsVisible = false;
            }
        }
    }

    private void ShowProgress()
    {
        // A progress report can arrive after its operation ended (Progress<T> posts to the UI thread); it must not bring the overlay back.
        if (_opening is null && _exportName is null) return;
        Progress.IsVisible = true;
        ProgressBar.IsIndeterminate = _exportName is not null && _exportTotal == 0;
        if (_exportName is not null)
        {
            ProgressBar.Value = _exportTotal == 0 ? 0 : (double)_exportDone / _exportTotal;
            ProgressText.Text = _exportTotal == 0 ? Loc.Current.Format("project.working", ("name", _exportName)) : Loc.Current.Format("project.exporting", ("name", _exportName), ("done", _exportDone), ("total", _exportTotal));
            return;
        }

        ProgressBar.Value = _openingFraction;
        ProgressText.Text = Loc.Current.Format("explorer.opening", ("name", _openingName), ("percent", (int)(_openingFraction * 100)));
    }

    private async Task<bool> SaveAsync()
    {
        if (Workspaces.Current is not { } workspace) return true;
        // Save commits the text typed in code editors first (design/shell.md 14.2 item 1); one that does not compile stops it.
        if (!ApplyCodeEditors()) return false;
        // Writing and checking a large file takes seconds: the progress overlay with its Cancel button shows unless an export already owns it.
        using var source = new CancellationTokenSource();
        var overlay = _exporting is null;
        if (overlay)
        {
            _exporting = source;
            (_exportName, _exportDone, _exportTotal) = (workspace.Name, 0, 0);
            ShowProgress();
        }

        try
        {
            return await SaveOrSyncAsync(workspace, async () => workspace.Project is { } link ? await link.SaveAsync(Pipeline, null, source.Token) : await workspace.SaveAsync(Pipeline, null, source.Token));
        }
        finally
        {
            if (overlay)
            {
                _exporting = null;
                _exportName = null;
                Progress.IsVisible = false;
            }
        }
    }

    /// <summary>Runs a save or a sync and tells the user how it went. In Project mode Save may only write the project (null result).</summary>
    private async Task<bool> SaveOrSyncAsync(Workspace workspace, Func<Task<SaveResult?>> save, string? fileName = null)
    {
        var name = fileName ?? Path.GetFileName(workspace.Path);
        try
        {
            var result = await save();
            if (result is null) return true;
            var (level, text) = result switch
            {
                { Saved: true } => (NotificationLevel.Info, Loc.Current.Format("save.done", ("name", name))),
                { Failure: SaveFailure.Locked, Holder: { } holder } => (NotificationLevel.Error, Loc.Current.Format("save.locked", ("name", name), ("holder", holder))),
                { Failure: SaveFailure.Locked } => (NotificationLevel.Error, Loc.Current.Format("save.lockedUnknown", ("name", name))),
                _ => (NotificationLevel.Error, Loc.Current.Format("save.invalid", ("name", name), ("detail", result.Message ?? ""))),
            };
            Notify(level, text);
            return result.Saved;
        }
        catch (OperationCanceledException)
        {
            // Cancelled from the progress overlay: the file on disk is untouched and the edits stay in memory and in the journal.
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or G3MLib.DataFile.GameMakerSerializationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("save.failed", ("name", name), ("reason", e.Message)));
            return false;
        }
    }

    /// <summary>Command line: the first argument that is not a flag is a data file to open.</summary>
    public void OpenStartupFile(IEnumerable<string> args)
    {
        var given = args.ToList();
        if (given.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) is not { } path) return;
        if (path.StartsWith(Core.Links.StudioLink.Scheme, StringComparison.OrdinalIgnoreCase)) OpenLink(path);
        else if (given.Contains("--run")) _ = OpenAndRunAsync(path, false);
        else if (given.Contains("--run-with-runtime")) _ = OpenAndRunAsync(path, true);
        else _ = OpenAsync(path);
    }

    /// <summary>The Explorer verb "Run game with G3MStudio": opens the file, then runs the game once it is open.</summary>
    private async Task OpenAndRunAsync(string path, bool pickRuntime)
    {
        await OpenAsync(path);
        if (Workspaces.Current is null) return;
        if (pickRuntime) await RunWithRuntimeAsync();
        else await RunGameAsync();
    }
}
