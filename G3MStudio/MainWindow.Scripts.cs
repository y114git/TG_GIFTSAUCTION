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
        try
        {
            var error = await ScriptRunner.RunAsync(workspace, path,
                text => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Info, text)),
                text => Avalonia.Threading.Dispatcher.UIThread.Post(() => Notify(NotificationLevel.Warning, text)));
            Notify(error is null ? NotificationLevel.Info : NotificationLevel.Error,
                error is null ? Loc.Current.Format("scripts.done", ("name", name)) : Loc.Current.Format("scripts.failed", ("name", name), ("reason", error)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("scripts.failed", ("name", name), ("reason", e.Message)));
        }
    }
}
