using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Raw edits of the data file on disk: byte patches from the hex view and whole chunks exported or replaced. Each one writes the file and loads it again.</summary>
public partial class MainWindow
{
    public void ExportChunk() => _ = ExportChunkAsync();

    public void ImportChunk() => _ = ImportChunkAsync();

    private async Task ExportChunkAsync()
    {
        if (Workspaces.Current is not { } workspace || await AskChunkAsync(workspace, "chunks.exportTitle") is not { } name) return;
        if (await PickPatchFile(Loc.Current["chunks.pickOut"], true, ".bin") is not { } output) return;
        try
        {
            await File.WriteAllBytesAsync(output, ChunkPatch.Export(workspace.Path, name));
            Notify(NotificationLevel.Info, Loc.Current.Format("chunks.exported", ("name", name), ("file", Path.GetFileName(output))));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    private async Task ImportChunkAsync()
    {
        if (Workspaces.Current is not { } workspace || await AskChunkAsync(workspace, "chunks.importTitle") is not { } name) return;
        if (await PickPatchFile(Loc.Current["chunks.pickIn"], false, ".bin") is not { } input) return;
        if (!await RawWriteOkAsync(workspace, "risky.chunkImportTitle", "risky.chunkImport", name)) return;
        try
        {
            ChunkPatch.Replace(workspace.Path, name, await File.ReadAllBytesAsync(input));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, e.Message);
            return;
        }

        Notify(NotificationLevel.Info, Loc.Current.Format("chunks.imported", ("name", name), ("file", Path.GetFileName(input))));
        await ReloadFromDiskAsync(workspace);
    }

    /// <summary>Called by the hex view: warns, writes the patched bytes into the file and loads it again. True when the file was written.</summary>
    internal async Task<bool> ApplyHexPatchAsync(Workspace workspace, HexSource source)
    {
        if (!await RawWriteOkAsync(workspace, "risky.hexPatchTitle", "risky.hexPatch", Path.GetFileName(workspace.Path))) return false;
        try
        {
            source.Commit(workspace.Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, e.Message);
            return false;
        }

        await ReloadFromDiskAsync(workspace);
        return true;
    }

    private async Task<string?> AskChunkAsync(Workspace workspace, string titleKey)
    {
        try
        {
            var names = ChunkPatch.Names(workspace.Path);
            if (names.Count == 0)
            {
                Notify(NotificationLevel.Warning, Loc.Current["chunks.none"]);
                return null;
            }

            return await ProjectDialogs.AskChoiceAsync(this, Loc.Current[titleKey], Loc.Current["chunks.pick"], names) is { } index ? names[index] : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, e.Message);
            return null;
        }
    }

    // The file on disk changes under the open data, so unsaved edits are lost: the owner is asked, or the risky-action setting decides.
    private async Task<bool> RawWriteOkAsync(Workspace workspace, string titleKey, string bodyKey, string name)
    {
        if (!await UnsavedOkAsync(workspace, "chunks.unsavedReload")) return false;
        return await AskRiskyAsync(new RiskyRefusalException(titleKey, bodyKey, Loc.Current["chunks.reason"]), name);
    }

    private async Task ReloadFromDiskAsync(Workspace workspace)
    {
        var path = workspace.Path;
        Workspaces.Close();
        File.Delete(Journal.FileFor(Workspaces.UserData, path));
        await OpenAsync(path);
    }
}
