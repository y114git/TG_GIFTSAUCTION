using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Saving and replacing the picture of a whole embedded texture page.</summary>
public sealed partial class MainWindow
{
    public void ExportPage() => _ = ExportPageAsync();

    public void ReplacePage() => _ = ReplacePageAsync();

    private (Workspace Workspace, string Name)? FrontPage()
    {
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (Workspaces.Current is { } workspace && key is not null && ResourceTypes.SplitKey(key) is var (type, name) && type == "embeddedTextures") return (workspace, name);
        Notify(NotificationLevel.Info, Loc.Current["pages.noPage"]);
        return null;
    }

    public async Task ExportPageAsync()
    {
        if (FrontPage() is not var (workspace, page)) return;
        if (await PickPatchFile(Loc.Current["pages.pickSave"], true, ".png") is not { } path) return;
        try
        {
            var png = await Task.Run(() =>
            {
                using (workspace.Lock.Read()) return ReplacePageCommand.ReadPng(workspace.Data, page);
            });
            await File.WriteAllBytesAsync(path, png);
            Notify(NotificationLevel.Info, Loc.Current.Format("pages.exported", ("page", page), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("pages.failed", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }

    public async Task ReplacePageAsync()
    {
        if (FrontPage() is not var (workspace, page)) return;
        if (await PickPatchFile(Loc.Current["pages.pick"], false) is not { } path) return;
        try
        {
            var picture = await File.ReadAllBytesAsync(path);
            if (await EditOrForceAsync(workspace, force => new ReplacePageCommand(page, picture, force), page)) Notify(NotificationLevel.Info, Loc.Current.Format("pages.replaced", ("page", page)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or ImageMagick.MagickException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("pages.failed", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }
}
