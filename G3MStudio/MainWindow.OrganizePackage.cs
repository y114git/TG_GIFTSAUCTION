using G3MLib.Modding.Resources;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Export of a whole virtual folder, collection or tag as one package.</summary>
public sealed partial class MainWindow
{
    public void ExportPackage() => _ = ExportPackageAsync();

    /// <summary>Packs the entries of the selected folder, collection or tag (else the selected entries) into a ZIP of the copy package layout.</summary>
    public async Task ExportPackageAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        var row = Explorer.SelectedRow;
        var container = row is not null && IsContainer(row) ? row : null;
        var keys = Explorer.SelectedKeys.Count == 0 && container is not null ? Explorer.Model.KeysOf(container) : ActionKeys();
        var items = FolderPackage.Packable(keys);
        if (items.Count == 0)
        {
            Notify(NotificationLevel.Warning, Loc.Current["organize.packageNothing"]);
            return;
        }

        var name = container?.Text ?? workspace.Name;
        if (await PickPatchFile(Loc.Current.Format("organize.packagePick", ("name", name)), true, ".zip") is not { } zip) return;
        var path = Path.HasExtension(zip) ? zip : zip + ".zip";
        // The system dialog confirmed the name it was given; a ".zip" added here may name a file it did not see.
        if (path != zip && File.Exists(path))
        {
            var (title, body) = (Loc.Current["organize.packageOverwriteTitle"], Loc.Current.Format("organize.packageOverwrite", ("file", path)));
            if (!await (Confirmer?.Invoke(title, body) ?? ProjectDialogs.AskConfirmAsync(this, title, body))) return;
        }

        await RunOperationAsync(name, async (progress, token) =>
        {
            await FolderPackage.ExportAsync(workspace, items, path, progress, token);
            return [new ItemResult(name, ResultKind.Written, null)];
        }, path, export: true);
    }
}
