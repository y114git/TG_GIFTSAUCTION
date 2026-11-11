using Avalonia.Threading;
using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Opens the image editor on a sprite frame, a page item or a texture page; each tab owns its session and frees it when the tab closes.</summary>
public sealed partial class MainWindow
{
    private int _imageFrame;

    private readonly Dictionary<string, (Workspace Workspace, TargetImageSession Session)> _pictureSessions = [];

    /// <summary>Opens the image editor tab for what is in front: the sprite frame picked in its strip, or the picture of a font, tile set, page item or texture page.</summary>
    public void EditImage()
    {
        var id = Docking.ActiveTabId;
        // The tab in front, else the row selected in the Explorer.
        var key = id is not null && Docking.Tabs.Get(id)?.Key is { } tabKey && PictureKey(tabKey) ? tabKey : Explorer.SelectedKey;
        if (key is null || !PictureKey(key) || ResourceTypes.SplitKey(key) is not var (type, _))
        {
            Notify(NotificationLevel.Info, Loc.Current["image.noPicture"]);
            return;
        }

        if (type == "sprites" && id is not null && _frameViews.TryGetValue(id, out var view) && Docking.Tabs.Get(id)?.Key == key) _imageFrame = view.SelectedFrame;
        Docking.OpenTab("image", key, false);
    }

    /// <summary>True when the key names something the image editor can open: a sprite, font, tile set, page item, embedded image, texture page, mask or image file.</summary>
    private bool PictureKey(string key) => ResourceTypes.SplitKey(key) is var (type, name) && PictureTargets.For(type, name, 0, Workspaces.Current?.Path) is not null;

    /// <summary>Opens the image editor on the picture of one entry (the button of the font, tile set, page item and page views).</summary>
    public void EditImageOf(string type, string name) => Docking.OpenTab("image", ResourceTypes.KeyOf(type, name), false);

    /// <summary>Opens the image editor on the entry an Explorer key names (a reference field, a room layer, an object's sprite); a key with no picture says so.</summary>
    public void EditImageKey(string key)
    {
        if (!PictureKey(key)) Notify(NotificationLevel.Info, Loc.Current["image.noPicture"]);
        else Docking.OpenTab("image", key, false);
    }

    /// <summary>The Edit Image buttons of an object tab: its sprite and, when it has one, its mask sprite. The sprite is looked up when the button is pressed, so a changed Sprite field is followed.</summary>
    private WrapPanel ObjectImageBar(GameMakerGameObject obj)
    {
        Button Make(string name, string key, Func<string?> picture)
        {
            var button = new Button { Name = name, Content = Loc.Current[key] };
            button.Click += (_, _) => EditImageKey(picture() ?? "");
            return button;
        }

        return new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { Make("ObjectEditImage", "textureItem.edit", () => PictureRefs.KeyOf(obj)), Make("ObjectEditMaskImage", "object.editMaskImage", () => PictureRefs.MaskKeyOf(obj)) } };
    }

    private Control ImageTab(TabInfo tab, Workspace workspace, string kind, string sprite)
    {
        if (kind != "sprites") return PictureTab(tab, workspace, kind, sprite);
        if (workspace.Data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) is not { Textures.Count: > 0 })
            return new TextBlock { Text = Loc.Current["image.noFrames"], Classes = { "muted" } };
#pragma warning disable CA2000 // the view owns its session and disposes it
        var view = new ImageEditorView(new WorkspaceImageSession(workspace, sprite), _imageFrame, Workspaces.Settings.Current.Image) { Bindings = id => Registry.Find(id)?.Shortcut, AliasBindings = id => Registry.Find(id)?.Aliases ?? [] };
#pragma warning restore CA2000
        view.Canvas.Edited += _ => Docking.KeepOpen(tab.Id);
        _imageViews[tab.Id] = view;
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded); // the tab was made after the dock told its listeners, so the menus look again
        return view;
    }

    /// <summary>The editor on a page item or a whole page: edits stay in the tab until Apply writes them through one undoable command.</summary>
    private Control PictureTab(TabInfo tab, Workspace workspace, string kind, string entry)
    {
        PixelBuffer picture;
        var target = PictureTargets.For(kind, entry, 0, workspace.Path);
        try
        {
            if (target is null) throw new InvalidOperationException();
            using (workspace.Lock.Read()) picture = target.Read(workspace.Data);
        }
        catch (Exception e) when (e is InvalidOperationException or ImageMagick.MagickException)
        {
            return new TextBlock { Text = Loc.Current["image.noPicture"], Classes = { "muted" } };
        }

        var session = new TargetImageSession(target, picture);
        if (target.NoteKey is { } noteKey)
        {
            var (width, height) = target.Limit(workspace.Data) ?? (picture.Width, picture.Height);
            session.Note = Loc.Current.Format(noteKey, ("w", width), ("h", height));
        }

#pragma warning disable CA2000 // the view owns its session and disposes it
        var view = new ImageEditorView(session, 0, Workspaces.Settings.Current.Image) { Bindings = id => Registry.Find(id)?.Shortcut, AliasBindings = id => Registry.Find(id)?.Aliases ?? [] };
#pragma warning restore CA2000
        view.Canvas.Edited += _ => Docking.KeepOpen(tab.Id);
        view.ApplyRequested += () => _ = ApplyPictureAsync(workspace, session);
        session.DirtyChanged += () =>
        {
            if (Docking.DocumentOf(tab.Id) is { } doc) doc.IsModified = session.Dirty;
        };
        _imageViews[tab.Id] = view;
        _pictureSessions[tab.Id] = (workspace, session);
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded);
        return view;
    }

    /// <summary>Writes the edited picture through the target's command; a size the target cannot take is warned about and may go on. True when it was written.</summary>
    internal async Task<bool> ApplyPictureAsync(Workspace workspace, TargetImageSession session)
    {
        var picture = session.Current;
        var done = await EditOrForceAsync(workspace, force =>
        {
            using (workspace.Lock.Read()) return session.Target.Apply(workspace.Data, picture, force);
        }, session.Target.Title);
        if (!done) return false;
        session.MarkApplied();
        Notify(NotificationLevel.Info, Loc.Current.Format("image.applied", ("name", session.Target.Title)));
        return true;
    }

    /// <summary>Applies every picture edit still waiting in a tab, before a save or an exit; false (with a message) when one is refused, so nothing is lost silently.</summary>
    private bool ApplyPictureEditors()
    {
        foreach (var (_, (workspace, session)) in _pictureSessions.ToList())
        {
            if (!session.Dirty) continue;
            try
            {
                var picture = session.Current;
                EditCommand command;
                using (workspace.Lock.Read()) command = session.Target.Apply(workspace.Data, picture, false);
                workspace.Edit(command);
                session.MarkApplied();
            }
            catch (InvalidOperationException e)
            {
                Notify(NotificationLevel.Error, Loc.Current.Format("image.notApplied", ("name", session.Target.Title), ("reason", e.Message)));
                return false;
            }
        }

        return true;
    }

    /// <summary>Runs an image editor command on the image tab in front.</summary>
    public void ImageCommand(string id)
    {
        if (Docking.ActiveTabId is { } tab && _imageViews.TryGetValue(tab, out var view)) view.Run(id);
    }

    private void DisposeImage(string tabId)
    {
        if (!_imageViews.Remove(tabId, out var view)) return;
        view.Dispose();
        if (!_pictureSessions.Remove(tabId, out var pending) || !pending.Session.Dirty) return;
        // The tab went with edits not applied: write them (asking about a size warning) instead of dropping them.
        _ = ApplyPictureAsync(pending.Workspace, pending.Session);
    }

    private void PruneImages(HashSet<string> open)
    {
        foreach (var id in _imageViews.Keys.Where(id => !open.Contains(id)).ToList()) DisposeImage(id);
    }
}
