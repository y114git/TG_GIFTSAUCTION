using G3MLib.DataFile.Util;
using G3MStudio.Core.Notifications;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Tools that work on the texture pages of the whole data file.</summary>
public sealed partial class MainWindow
{
    public void RepackTextures() => _ = RepackTexturesAsync();

    /// <summary>
    /// Plans a repack of every texture group, asks with the page counts before and after, and applies it. Like an import it is a bulk change:
    /// it needs a file without unsaved edits, cannot be undone, and Revert to Saved is the way back.
    /// </summary>
    public async Task RepackTexturesAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (workspace.History.IsDirty)
        {
            Notify(NotificationLevel.Warning, Loc.Current["transfer.needsSave"]);
            return;
        }

        await RunPatchAsync("textures.repacking", async token =>
        {
            var repacker = new TexturePageRepacker(workspace.Data);
            var plan = await Task.Run(() =>
            {
                using (workspace.Lock.Read()) return repacker.Plan(new RepackOptions(), null, token);
            }, token);
            var (before, after) = plan.PageCount;
            if (!await Confirm(Loc.Current["textures.title"], Loc.Current.Format("textures.confirm", ("before", before), ("after", after)))) return (true, Loc.Current["mods.cancelled"]);
            await workspace.ApplyBulkAsync(() => repacker.Apply(plan));
            return (true, Loc.Current.Format("textures.done", ("before", before), ("after", after)));
        });
    }
}
