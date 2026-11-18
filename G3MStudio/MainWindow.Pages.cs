using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Saving and replacing the picture of a whole embedded texture page.</summary>
public sealed partial class MainWindow
{
    public void ExportPage() => _ = ExportPageAsync();

    public void ReplacePage() => _ = ReplacePageAsync();

    public void ResizePage() => _ = ResizePageAsync();

    /// <summary>Asks for the new size with a live line of what happens to the pictures on the page, then resizes it as one undoable edit.</summary>
    public async Task ResizePageAsync()
    {
        if (FrontPage() is not var (workspace, page)) return;
        (int Width, int Height) size;
        List<string> pages;
        int automatic;
        using (workspace.Lock.Read())
        {
            var texture = PageResizeCommand.Find(workspace.Data, page);
            if (texture.TextureData?.Image is not { } image) return;
            size = (image.Width, image.Height);
            pages = [.. workspace.Data.EmbeddedTextures.Where(t => !t.TextureExternal && t.TextureData?.Image is not null).Select(t => t.Name?.Content ?? "")];
            automatic = TextureFit.AutomaticMaxSide(workspace.Data);
        }

        string Plan((int Width, int Height) to, FitOptions fit)
        {
            using (workspace.Lock.Read()) return Editor.FitText.Describe(TextureFit.Plan(workspace.Data, [], PageResizeCommand.Find(workspace.Data, page), to, fit));
        }

        if (await new Editor.ResizePageWindow(page, size, Plan, pages, automatic).AskAsync(this) is not { } chosen || (chosen.Width, chosen.Height) == size) return;
        try
        {
            if (workspace.Edit(new PageResizeCommand(page, chosen.Width, chosen.Height, chosen.Fit))) Notify(NotificationLevel.Info, Loc.Current.Format("pages.resized", ("page", page), ("width", chosen.Width), ("height", chosen.Height)));
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("pages.failed", ("name", page), ("reason", e.Message)));
        }
    }

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
