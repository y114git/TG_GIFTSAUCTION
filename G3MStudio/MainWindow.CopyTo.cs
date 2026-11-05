using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Copy to Other Data File: the selected resources, with what they use, go into a second data file as one undoable edit of that file.</summary>
public sealed partial class MainWindow
{
    /// <summary>Replaces the choice of the target file (gets the source; returns a workspace the copy owns and disposes, or null). Tests set it.</summary>
    public Func<Workspace, Task<Workspace?>>? CopyTargetChooser { get; set; }

    /// <summary>Replaces the preview dialog (source, target, items; returns the plan to apply, or null). Tests set it.</summary>
    public Func<Workspace, Workspace, IReadOnlyList<(string Type, string Name)>, Task<CopyPlan?>>? CopyReviewer { get; set; }

    public void CopyToOtherFile() => _ = CopyToOtherFileAsync();

    /// <summary>Picks the target, shows what would happen, copies, and then saves the target or drops the copy: the target is open only for this.</summary>
    public async Task CopyToOtherFileAsync()
    {
        if (Workspaces.Current is not { } source) return;
        var items = CopySelection();
        if (items.Count == 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current["copyTo.noSelection"]);
            return;
        }

        using var target = CopyTargetChooser is { } choose ? await choose(source) : await OpenCopyTargetAsync(source);
        if (target is null) return;
        if (ResourceCopier.TargetRefusal(source, target) is { } refusal)
        {
            Notify(NotificationLevel.Error, refusal);
            return;
        }

        var plan = CopyReviewer is { } review ? await review(source, target, items) : await new CopyToWindow(source, target, items).AskAsync(this);
        if (plan is null || !await RunCopyAsync(source, target, plan)) return;
        await SettleCopyTargetAsync(target);
    }

    private IReadOnlyList<(string Type, string Name)> CopySelection()
    {
        var items = BulkResources.Split(Explorer.SelectedKeys);
        if (items.Count > 0) return items;
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        return key is not null && ResourceTypes.SplitKey(key) is var (type, name) ? [(type, name)] : [];
    }

    private async Task<Workspace?> OpenCopyTargetAsync(Workspace source)
    {
        if (await PickPatchFile(Loc.Current["copyTo.pickTarget"], false) is not { } path) return null;
        if (string.Equals(Path.GetFullPath(path), source.Path, StringComparison.OrdinalIgnoreCase))
        {
            Notify(NotificationLevel.Warning, Loc.Current["copyTo.sameFile"]);
            return null;
        }

        try
        {
            return await Workspaces.OpenDetachedAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or G3MLib.DataFile.GameMakerSerializationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("app.openFailed", ("name", Path.GetFileName(path)), ("reason", e.Message)));
            return null;
        }
    }

    private async Task<bool> RunCopyAsync(Workspace source, Workspace target, CopyPlan plan, bool allowLarge = false)
    {
        using var cancel = _exporting = new CancellationTokenSource();
        (_exportName, _exportDone, _exportTotal) = (Loc.Current.Format("copyTo.working", ("target", target.Name)), 0, 0);
        ShowProgress();
        RiskyRefusalException large;
        try
        {
            var progress = new Progress<G3MLib.Modding.Resources.BatchProgress>(p => { (_exportDone, _exportTotal) = (p.Done, p.Total); ShowProgress(); });
            var command = await ResourceCopier.CopyAsync(source, target, plan, progress, cancel.Token, allowLarge);
            foreach (var problem in command.Results.Where(r => r.Kind is G3MLib.Modding.Resources.ResultKind.Failed or G3MLib.Modding.Resources.ResultKind.Warning))
                Notifications.Post(problem.Kind == G3MLib.Modding.Resources.ResultKind.Failed ? NotificationLevel.Error : NotificationLevel.Warning, $"{problem.Name}: {problem.Message}");
            Notify(NotificationLevel.Info, Loc.Current.Format("copyTo.done", ("count", plan.Applied.Count()), ("target", target.Name)));
            return true;
        }
        catch (OperationCanceledException)
        {
            Notify(NotificationLevel.Info, Loc.Current["copyTo.cancelled"]);
            return false;
        }
        catch (RiskyRefusalException e) when (!allowLarge)
        {
            large = e;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or CodeCompileException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("copyTo.failed", ("reason", e.Message)));
            return false;
        }
        finally
        {
            _exporting = null;
            _exportName = null;
            Progress.IsVisible = false;
            UpdateTexts();
        }

        // The package was too large; the progress and the token are gone, so ask here and run the copy again.
        return await AskRiskyAsync(large, target.Name) && await RunCopyAsync(source, target, plan, true);
    }

    // The target was opened for this copy alone, so its edit cannot stay on an undo stack nobody sees: save it or take the copy out again.
    private async Task SettleCopyTargetAsync(Workspace target)
    {
        if (await Confirm(Loc.Current["copyTo.saveTitle"], Loc.Current.Format("copyTo.saveAsk", ("target", target.Name))))
        {
            await SaveOrSyncAsync(target, async () => await target.SaveAsync(Pipeline), Path.GetFileName(target.Path));
            return;
        }

        target.Undo();
        Notify(NotificationLevel.Info, Loc.Current.Format("copyTo.dropped", ("target", target.Name)));
    }
}
