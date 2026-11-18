using Avalonia.Controls;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

// Closing, new file, undo and links.
public sealed partial class MainWindow : Window, IShell, IDisposable
{
    private async Task ExitAsync()
    {
        if (!await ConfirmCloseAsync()) return;
        _closeConfirmed = true;
        Close();
    }

    private async Task<bool> SaveForCloseAsync(Workspace workspace) => await SaveAsync();

    public void Notify(NotificationLevel level, string text) => Notifications.Post(level, Workspaces.Current?.Nicknames.Decorate(text) ?? text);

    public void OpenDataFile() => _ = PickAndOpenAsync();

    public void NewDataFile() => _ = NewDataFileAsync();

    /// <summary>Asks where to save, writes an empty GameMaker LTS data file there (UTMT-FILE-01) and opens it. An existing file is never replaced without the picker's own question.</summary>
    public async Task NewDataFileAsync()
    {
        if (await PickPatchFile(Loc.Current["cmd.file.newDataPick"], true, ".win") is not { } path) return;
        try
        {
            // Written beside the target and moved over it at the end, so a failed write leaves an existing file as it was.
            var temp = $"{Path.GetFullPath(path)}.{Guid.NewGuid():N}.new";
            try
            {
                using (var data = G3MLib.DataFile.GameMakerData.CreateNew2026LTS())
                await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                    await G3MLib.DataFile.GameMakerIO.WriteAsync(stream, data);
                File.Move(temp, path, true);
            }
            catch
            {
                try { File.Delete(temp); }
                catch (Exception) { /* the write error below is the one to report */ }
                throw;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            FailureReport.Report(e, "g3mlib.write", new Dictionary<string, string> { ["g3mlib.version"] = FailureReport.G3MLibVersion });
            Notify(NotificationLevel.Error, Loc.Current.Format("cmd.file.newDataFailed", ("reason", e.Message)));
            return;
        }

        await OpenAsync(path);
    }

    public void CloseWorkspace() => _ = CloseWorkspaceAsync();

    public async Task CloseWorkspaceAsync()
    {
        if (!await ConfirmCloseAsync()) return;
        Workspaces.Close();
        Workspaces.Settings.Update(x => x with { LastSession = null }); // closed on purpose: the next start shows Home
    }

    public void Undo()
    {
        if (Workspaces.Current is { History.CanUndo: true } workspace) workspace.Undo();
    }

    public void Redo()
    {
        if (Workspaces.Current is { History.CanRedo: true } workspace) workspace.Redo();
    }

    /// <summary>Opens what a g3mstudio:// link names: a local data file, or one of the actions. A web address is not downloaded (it would need the confirmation and the cache of the mod import).</summary>
    public void OpenLink(string text)
    {
        switch (Core.Links.StudioLink.Parse(text))
        {
            case { Kind: Core.Links.StudioLinkKind.File, Target: var file }:
                _ = OpenAsync(file);
                break;
            case { Kind: Core.Links.StudioLinkKind.Action, Target: "open" }:
                _ = Registry.Execute("file.open");
                break;
            case { Kind: Core.Links.StudioLinkKind.Action, Target: "patch-tools" }:
                _ = Registry.Execute("mods.applyPatch");
                break;
            case { Kind: Core.Links.StudioLinkKind.Action, Target: "new-mod" }:
                // A new mod starts from the changes of the open game: the Export Mod command, which needs a game open.
                if (!Registry.Execute("mods.exportMod")) Notify(NotificationLevel.Info, Loc.Current["links.needsGame"]);
                break;
            case { Kind: Core.Links.StudioLinkKind.Download, Target: var address }:
                _ = DownloadLinkAsync(address);
                break;
            default:
                Notify(NotificationLevel.Warning, Loc.Current["links.unknown"]);
                break;
        }
    }

}
