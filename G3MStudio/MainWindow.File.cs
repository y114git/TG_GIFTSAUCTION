using Avalonia.Platform.Storage;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Save As, Save a Copy and the file picker behind them.</summary>
public sealed partial class MainWindow
{
    public void SaveAs() => _ = PickAndSaveAsync(move: true);

    public void SaveCopy() => _ = PickAndSaveAsync(move: false);

    private async Task PickAndSaveAsync(bool move)
    {
        if (Workspaces.Current is not { } workspace || !StorageProvider.CanSave) return;
        var picked = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = Path.GetFileName(workspace.Path),
            FileTypeChoices = [new FilePickerFileType("GameMaker data") { Patterns = ["*.win", "*.unx", "*.ios", "*.droid", "audiogroup*.dat"] }],
        });
        if (picked?.TryGetLocalPath() is { } target) await SaveToAsync(target, move);
    }

    /// <summary>
    /// Writes the data file to <paramref name="target"/> through the usual save pipeline. With <paramref name="move"/> the workspace then
    /// opens the new file and the old file keeps what it had (design/shell.md 14.2 item 6). A workspace with a project moves through
    /// Project > Data File Location instead.
    /// </summary>
    public async Task<bool> SaveToAsync(string target, bool move)
    {
        if (Workspaces.Current is not { } workspace) return false;
        if (move && workspace.Project is not null)
        {
            Notify(NotificationLevel.Info, Loc.Current["save.moveWithProject"]);
            return false;
        }

        if (!ApplyCodeEditors()) return false;
        var saved = await SaveOrSyncAsync(workspace, async () => await Pipeline.SaveAsync(workspace.Data, target), Path.GetFileName(target));
        if (!saved || !move) return saved;
        var old = workspace.Path;
        await OpenAsync(target);
        // Only when the new file is open do the edits of the old one stop waiting for recovery: they live in the new file now.
        if (Workspaces.Current is not { } current || current.Path != Path.GetFullPath(target)) return false;
        File.Delete(Journal.FileFor(Workspaces.UserData, old));
        return true;
    }
}
