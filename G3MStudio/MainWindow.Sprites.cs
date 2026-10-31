using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using ImageMagick;

namespace G3MStudio;

/// <summary>Editing the pictures of a sprite.</summary>
public sealed partial class MainWindow
{
    public void ReplaceFrame() => _ = ReplaceFrameAsync();

    public void DuplicateFrame() => ChangeFrames(FrameListCommand.Duplicate);

    public void RemoveFrame() => ChangeFrames(FrameListCommand.Remove);

    /// <summary>Rebuilds the collision masks of the sprite in the front tab from its frames (a pixel is solid above alpha 0); one undoable step.</summary>
    public void RecalculateMask()
    {
        if (Workspaces.Current is not { } workspace || Docking.ActiveTabId is not { } id || !_frameViews.ContainsKey(id)
            || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, sprite))
        {
            Notify(NotificationLevel.Info, Loc.Current["sprites.noSprite"]);
            return;
        }

        workspace.Edit(new RecalcMaskCommand(sprite));
        Notify(NotificationLevel.Info, Loc.Current.Format("sprites.maskDone", ("sprite", sprite)));
    }

    /// <summary>The sprite and mask of the sprite tab in front: the mask of the clicked frame, or the last one when the sprite shares masks.</summary>
    private (Workspace Workspace, string Sprite, int Index, int Width, int Height, byte[] Data)? FrontMask()
    {
        if (Workspaces.Current is not { } workspace || Docking.ActiveTabId is not { } id || !_frameViews.TryGetValue(id, out var view)
            || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, name))
        {
            Notify(NotificationLevel.Info, Loc.Current["sprites.noSprite"]);
            return null;
        }

        var sprite = workspace.Data.Sprites.FirstOrDefault(s => s.Name.Content == name);
        if (sprite is null || sprite.CollisionMasks.Count == 0)
        {
            Notify(NotificationLevel.Info, Loc.Current["sprites.noMask"]);
            return null;
        }

        var index = Math.Clamp(view.SelectedFrame, 0, sprite.CollisionMasks.Count - 1);
        var entry = sprite.CollisionMasks[index];
        return (workspace, name, index, entry.Width, entry.Height, entry.Data);
    }

    public void ExportMask() => _ = ExportMaskAsync();

    public async Task ExportMaskAsync()
    {
        if (FrontMask() is not var (_, sprite, index, width, height, data)) return;
        if (await PickPatchFile(Loc.Current["sprites.pickMaskSave"], true, ".png") is not { } path) return;
        try
        {
            await File.WriteAllBytesAsync(path, MaskPicture.ToPng(data, width, height));
            Notify(NotificationLevel.Info, Loc.Current.Format("sprites.maskExported", ("index", index), ("sprite", sprite), ("path", path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sprites.badPicture", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }

    public void ImportMask() => _ = ImportMaskAsync();

    public async Task ImportMaskAsync()
    {
        if (FrontMask() is not var (workspace, sprite, index, width, height, _)) return;
        if (await PickPatchFile(Loc.Current["sprites.pickMask"], false) is not { } path) return;
        try
        {
            workspace.Edit(new ImportMaskCommand(sprite, index, MaskPicture.FromPicture(await File.ReadAllBytesAsync(path), width, height)));
            Notify(NotificationLevel.Info, Loc.Current.Format("sprites.maskImported", ("index", index), ("sprite", sprite)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or MagickException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sprites.badPicture", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }

    /// <summary>Duplicates or removes the clicked frame of the sprite in the front tab; one undoable step.</summary>
    public void ChangeFrames(string operation)
    {
        if (Workspaces.Current is not { } workspace || Docking.ActiveTabId is not { } id || !_frameViews.TryGetValue(id, out var view)
            || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, sprite))
        {
            Notify(NotificationLevel.Info, Loc.Current["sprites.noSprite"]);
            return;
        }

        if (operation == FrameListCommand.Remove && view.FrameCount < 2)
        {
            Notify(NotificationLevel.Warning, Loc.Current["sprites.lastFrame"]);
            return;
        }

        workspace.Edit(new FrameListCommand(sprite, operation, view.SelectedFrame));
    }

    /// <summary>Replaces the clicked frame of the sprite in the front tab with a picture file; one undoable step.</summary>
    public async Task ReplaceFrameAsync()
    {
        if (Workspaces.Current is not { } workspace || Docking.ActiveTabId is not { } id || !_frameViews.TryGetValue(id, out var view)
            || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, sprite))
        {
            Notify(NotificationLevel.Info, Loc.Current["sprites.noSprite"]);
            return;
        }

        if (await PickPatchFile(Loc.Current["sprites.pickPicture"], false) is not { } path) return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            using (ReplaceFrameCommand.ReadPicture(bytes)) { } // refuses what cannot be read before it reaches the history
            workspace.Edit(new ReplaceFrameCommand(sprite, view.SelectedFrame, bytes));
            Notify(NotificationLevel.Info, Loc.Current.Format("sprites.replaced", ("frame", view.SelectedFrame), ("sprite", sprite)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or MagickException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("sprites.badPicture", ("name", Path.GetFileName(path)), ("reason", e.Message)));
        }
    }
}
