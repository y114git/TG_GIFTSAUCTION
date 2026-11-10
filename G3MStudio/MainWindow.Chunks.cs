using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Raw edits of the data file on disk: byte patches from the hex view and whole chunks exported or replaced. Each one writes the file and loads it again.</summary>
public partial class MainWindow
{
    public void ExportChunk() => _ = GuardedAsync(ExportChunkAsync);

    public void ImportChunk() => _ = GuardedAsync(ImportChunkAsync);

    // These run without an awaiting caller: an escaped exception would end up unobserved, so it is reported here.
    private async Task GuardedAsync(Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    private async Task ExportChunkAsync()
    {
        if (Workspaces.Current is not { } workspace || await AskChunkAsync(workspace, "chunks.exportTitle") is not { } name) return;
        if (await PickPatchFile(Loc.Current["chunks.pickOut"], true, ".bin") is not { } output) return;
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var target = Path.GetFullPath(output);
        if (string.Equals(target, Path.GetFullPath(workspace.Path), comparison) || string.Equals(target, Path.GetFullPath(workspace.Path + ".bak"), comparison))
        {
            Notify(NotificationLevel.Warning, Loc.Current["chunks.sameFile"]);
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                // Written beside the target first, so a failed export leaves an existing file untouched.
                var temp = target + ".tmp";
                try
                {
                    using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write)) ChunkPatch.Export(workspace.Path, name, file);
                    File.Move(temp, target, true);
                }
                finally
                {
                    File.Delete(temp);
                }
            });
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
            var data = await File.ReadAllBytesAsync(input);
            if (!StillCurrent(workspace)) return; // the dialogs and the read let the user switch the open file
            ChunkPatch.Replace(workspace.Path, name, data);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, e.Message);
            return;
        }

        Notify(NotificationLevel.Info, Loc.Current.Format("chunks.imported", ("name", name), ("file", Path.GetFileName(input))));
        await ReloadFromDiskAsync(workspace);
    }

    private bool StillCurrent(Workspace workspace)
    {
        if (ReferenceEquals(Workspaces.Current, workspace)) return true;
        Notify(NotificationLevel.Warning, Loc.Current["chunks.otherFile"]);
        return false;
    }

    /// <summary>Called by the hex view: warns, writes the patched bytes into the file and loads it again. True when the file was written. The open data file is looked up by path now, since the view can outlive the one it was opened for.</summary>
    internal async Task<bool> ApplyHexPatchAsync(string path, HexSource source)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (Workspaces.Current is not { } workspace || !string.Equals(Path.GetFullPath(workspace.Path), Path.GetFullPath(path), comparison))
        {
            Notify(NotificationLevel.Warning, Loc.Current["chunks.otherFile"]);
            return false;
        }

        if (!await RawWriteOkAsync(workspace, "risky.hexPatchTitle", "risky.hexPatch", Path.GetFileName(workspace.Path))) return false;
        if (!StillCurrent(workspace)) return false;
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
        try
        {
            var path = workspace.Path;
            Workspaces.Close();
            // Set aside, not deleted: the recorded edits belong to the old bytes but may still be wanted.
            var journal = Journal.FileFor(Workspaces.UserData, path);
            if (File.Exists(journal)) File.Move(journal, journal + ".stale", true);
            await OpenAsync(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }
}
