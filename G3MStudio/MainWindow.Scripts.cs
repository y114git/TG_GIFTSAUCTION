using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Running .csx scripts on the open data file.</summary>
public sealed partial class MainWindow
{
    public void RunScript() => _ = RunScriptAsync();

    /// <summary>
    /// Asks for a .csx file, warns what a script can do, and runs it as a bulk change (it cannot be undone; Revert to Saved is the way back).
    /// It needs a file without unsaved edits, like an import.
    /// </summary>
    public async Task RunScriptAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["transfer.needsSave"]);
            return;
        }

        if (await PickPatchFile(Loc.Current["scripts.pick"], false) is not { } path) return;
        var name = Path.GetFileName(path);
        if (!await Confirm(Loc.Current["scripts.title"], Loc.Current.Format("scripts.confirm", ("name", name)))) return;
        Notify(NotificationLevel.Info, Loc.Current.Format("scripts.running", ("name", name)));
        var host = NewScriptHost(name);
        // The script holds the data lock and waits for the UI thread when it asks something, so nothing on the window may take that lock meanwhile.
        IsEnabled = false;
        try
        {
            var error = await ScriptRunner.RunAsync(workspace, path,
                text => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Info, text)),
                text => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, text)),
                host);
            if (error is not null) Notify(NotificationLevel.Error, Loc.Current.Format("scripts.failed", ("name", name), ("reason", error)));
            else if (host.FinishedMessage) Notify(NotificationLevel.Info, Loc.Current.Format("scripts.done", ("name", name)));
            if (host.Selections.Count > 0 && UsageSearch.TypeKeyOf(host.Selections[^1]) is { } type) OpenEntry(ResourceTypes.KeyOf(type, ResourceTypes.NameOf(host.Selections[^1])));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or OperationCanceledException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("scripts.failed", ("name", name), ("reason", e.Message)));
        }
        finally
        {
            IsEnabled = true;
            _exportName = null;
            Progress.IsVisible = false;
        }
    }

    // The script runs on a pool thread and holds the write lock, so a prompt is shown on the UI thread and the script waits for the answer. The answer never touches the data.
    private ScriptHost NewScriptHost(string name)
    {
        static T OnUi<T>(Func<Task<T>> work) => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(work).GetAwaiter().GetResult();
        return new ScriptHost
        {
            Question = text => OnUi(() => Confirm(Loc.Current["scripts.title"], text)),
            Input = (title, label, current, multiline) => OnUi(() => ProjectDialogs.AskTextAsync(this, title, label, current, multiline)),
            ChooseDirectory = () => OnUi(() => PickFolderAsync(Loc.Current["scripts.pickFolder"])),
            LoadFile = (extension, _) => OnUi(() => PickPatchFile(Loc.Current["scripts.pickFile"], false, extension.Length > 0 ? extension : null)),
            SaveFile = (extension, _) => OnUi(() => PickPatchFile(Loc.Current["scripts.pickSave"], true, extension.Length > 0 ? extension : null)),
            Progress = (label, status, value, maximum) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _exportName = label ?? _exportName ?? name;
                (_exportDone, _exportTotal) = (value, (int)Math.Max(maximum, 0));
                ShowProgress();
            }),
        };
    }
}
