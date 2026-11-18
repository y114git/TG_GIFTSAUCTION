using Avalonia.Threading;
using Avalonia.Controls;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;
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

    private Control ImageTab(TabInfo tab, Workspace workspace, string kind, string sprite)
    {
        if (kind != "sprites") return PictureTab(tab, workspace, kind, sprite);
        if (workspace.Data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) is not { Textures.Count: > 0 })
            return new TextBlock { Text = Loc.Current["image.noFrames"], Classes = { "muted" } };
#pragma warning disable CA2000 // the view owns its session and disposes it
        // Default: edits wait in the editor for Apply. The setting "Apply image edits immediately" sends each edit to the workspace history as before.
        ImageSession session = Workspaces.Settings.Current.Image.ApplyImmediately ? new WorkspaceImageSession(workspace, sprite) : new DeferredImageSession(workspace, sprite);
        var view = new ImageEditorView(session, _imageFrame, Workspaces.Settings.Current.Image) { Bindings = id => Registry.Find(id)?.Shortcut, AliasBindings = id => Registry.Find(id)?.Aliases ?? [], ConflictAsker = () => new ImageChangesWindow(workspace.Nicknames.Shown(sprite), true).AskAsync(this) };
#pragma warning restore CA2000
        view.Canvas.Edited += _ => Docking.KeepOpen(tab.Id);
        view.SaveAsNewRequested += pictures => _ = SaveAsNewSpriteAsync(workspace, sprite, pictures);
        ConnectOriginal(view, workspace, kind, sprite);
        view.PendingChanged += () =>
        {
            if (Docking.DocumentOf(tab.Id) is { } doc) doc.IsModified = view.HasPending;
        };
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
        // The Resize dialog and the canvas handles say where the picture goes on the texture pages; a target with no page item has no plan, and keeps the plain prompts.
        bool planned;
        using (workspace.Lock.Read()) planned = target.Plan(workspace.Data, picture.Width, picture.Height) is not null;
        if (planned)
        {
            session.Planner = (width, height, fit) =>
            {
                using (workspace.Lock.Read()) return target.Plan(workspace.Data, width, height, fit);
            };
            using (workspace.Lock.Read()) (session.Pages, session.AutomaticMax) = (WorkspaceImageSession.PagesOf(workspace.Data), TextureFit.AutomaticMaxSide(workspace.Data));
        }

        if (target.NoteKey is { } noteKey)
        {
            (int Width, int Height)? limit;
            using (workspace.Lock.Read()) limit = target.Limit(workspace.Data);
            session.FixedSize = limit is not null;
            var (width, height) = limit ?? (picture.Width, picture.Height);
            session.Note = Loc.Current.Format(noteKey, ("w", width), ("h", height));
        }

#pragma warning disable CA2000 // the view owns its session and disposes it
        var view = new ImageEditorView(session, 0, Workspaces.Settings.Current.Image) { Bindings = id => Registry.Find(id)?.Shortcut, AliasBindings = id => Registry.Find(id)?.Aliases ?? [] };
#pragma warning restore CA2000
        view.Canvas.Edited += _ => Docking.KeepOpen(tab.Id);
        view.ApplyRequested += () => _ = ApplyPictureAsync(workspace, session);
        view.SaveAsNewRequested += pictures => _ = SaveAsNewSpriteAsync(workspace, session.Target.Title, pictures);
        ConnectOriginal(view, workspace, kind, entry);
        session.Baseline = picture;
        session.DirtyChanged += () =>
        {
            if (Docking.DocumentOf(tab.Id) is { } doc) doc.IsModified = session.Dirty;
        };
        _imageViews[tab.Id] = view;
        _pictureSessions[tab.Id] = (workspace, session);
        Dispatcher.UIThread.Post(UpdateTabContexts, DispatcherPriority.Loaded);
        return view;
    }

    /// <summary>The resource a picture belongs to: a mask belongs to its sprite, a glyph to its font; null when the picture has no tab of its own (an included file).</summary>
    private static string? OriginalKey(Workspace workspace, string kind, string entry)
    {
        var cut = entry.LastIndexOf('#');
        var key = kind switch
        {
            "spriteMasks" when cut > 0 => ResourceTypes.KeyOf("sprites", entry[..cut]),
            "fontGlyphs" when cut > 0 => ResourceTypes.KeyOf("fonts", entry[..cut]),
            _ => ResourceTypes.KeyOf(kind, entry),
        };
        return ResourceTypes.Find(workspace.Data, key) is null ? null : key;
    }

    // The button beside the toolbar opens the tab of the resource this editor edits.
    private void ConnectOriginal(ImageEditorView view, Workspace workspace, string kind, string entry)
    {
        if (OriginalKey(workspace, kind, entry) is null) return;
        view.HasOriginal = true;
        // Looked up again at the click: the resource may have been deleted or renamed since the tab opened.
        view.OriginalRequested += () =>
        {
            if (OriginalKey(workspace, kind, entry) is { } key) Docking.OpenTab("resource", key, false);
            else view.HasOriginal = false;
        };
    }

    /// <summary>Writes the edited picture through the target's command; a size the target cannot take is warned about and may go on. True when it was written.</summary>
    internal async Task<bool> ApplyPictureAsync(Workspace workspace, TargetImageSession session)
    {
        // The picture may have changed elsewhere since this tab read it (another editor, the sprite editor); applying would replace that change without a word.
        if (ChangedElsewhere(workspace, session) && !await Confirm(Loc.Current["image.conflictTitle"], Loc.Current.Format("image.changedElsewhere", ("name", workspace.Nicknames.Shown(session.Target.Title))))) return false;
        var picture = session.Current;
        var done = await EditOrForceAsync(workspace, force =>
        {
            using (workspace.Lock.Read()) return session.Target.Apply(workspace.Data, picture, force, session.Shift, session.Fit ?? FitOptionsPanel.Current);
        }, session.Target.Title);
        if (!done) return false;
        session.MarkApplied();
        session.Baseline = ReadTarget(workspace, session);
        Notify(NotificationLevel.Info, Loc.Current.Format("image.applied", ("name", workspace.Nicknames.Shown(session.Target.Title))));
        return true;
    }

    private async Task ApplyDirtyPicturesAsync()
    {
        foreach (var (workspace, session) in _pictureSessions.Values.Where(p => p.Session.Dirty).ToList()) await ApplyPictureAsync(workspace, session);
    }

    /// <summary>Applies every picture edit still waiting in a tab, before a save or an exit; false (with a message) when one is refused, so nothing is lost silently.</summary>
    private bool ApplyPictureEditors()
    {
        // A sprite tab with local edits writes them first, so a save or an exit does not lose them; a frame changed elsewhere stops the save with a message.
        foreach (var (id, view) in _imageViews.ToList())
        {
            if (view.Deferred is not { Pending: true } deferred) continue;
            string reason;
            try
            {
                if (deferred.Apply()) continue;
                reason = Loc.Current["image.conflictTitle"];
            }
            catch (InvalidOperationException e)
            {
                reason = e.Message; // a locked sprite: the save stops with the reason and the edits stay in the tab
            }

            Notify(NotificationLevel.Error, Loc.Current.Format("image.notApplied", ("name", Docking.DocumentOf(id)?.Title ?? ""), ("reason", reason)));
            return false;
        }

        foreach (var (_, (workspace, session)) in _pictureSessions.ToList())
        {
            if (!session.Dirty) continue;
            try
            {
                if (ChangedElsewhere(workspace, session)) throw new InvalidOperationException(Loc.Current["image.conflictTitle"]);
                var picture = session.Current;
                EditCommand command;
                using (workspace.Lock.Read()) command = session.Target.Apply(workspace.Data, picture, false, session.Shift, session.Fit ?? FitOptionsPanel.Current);
                workspace.Edit(command);
                session.MarkApplied();
                session.Baseline = ReadTarget(workspace, session);
            }
            catch (InvalidOperationException e)
            {
                Notify(NotificationLevel.Error, Loc.Current.Format("image.notApplied", ("name", workspace.Nicknames.Shown(session.Target.Title)), ("reason", e.Message)));
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

    private static PixelBuffer? ReadTarget(Workspace workspace, TargetImageSession session)
    {
        try
        {
            using (workspace.Lock.Read()) return session.Target.Read(workspace.Data);
        }
        catch (Exception e) when (e is InvalidOperationException or ImageMagick.MagickException)
        {
            return null; // nothing to compare with later
        }
    }

    // True when the target now holds another picture than the one this tab last read or wrote.
    private static bool ChangedElsewhere(Workspace workspace, TargetImageSession session) =>
        session.Baseline is { } was && ReadTarget(workspace, session) is { } now && (now.Width != was.Width || now.Height != was.Height || !now.Data.AsSpan().SequenceEqual(was.Data));

    /// <summary>Test hook: answers the Apply, Discard or Cancel question of a closing image tab.</summary>
    public Func<string, Task<CloseChoice>>? ImageChangesAsker { get; set; }

    // A user close of an image tab with edits waiting asks first: false holds the close back, and the answer closes the tab again.
    private bool GuardImageClose(string tabId)
    {
        if (!_imageViews.TryGetValue(tabId, out var view) || !view.HasPending) return true;
        if (!_closingImage.Add(tabId)) return false; // the question is already open: the close stays held back
        _ = AskImageCloseAsync(tabId, view);
        return false;
    }

    private readonly HashSet<string> _closingImage = [];

    private async Task AskImageCloseAsync(string tabId, ImageEditorView view)
    {
        var close = false;
        try
        {
            var name = Docking.DocumentOf(tabId)?.Title ?? "";
            var choice = await (ImageChangesAsker?.Invoke(name) ?? new ImageChangesWindow(name).AskAsync(this));
            if (choice == CloseChoice.Save)
            {
                if (_pictureSessions.TryGetValue(tabId, out var pending)) close = await ApplyPictureAsync(pending.Workspace, pending.Session);
                else close = await view.ApplyChangesAsync();
            }
            else if (choice == CloseChoice.DoItLater)
            {
                // The target tab drops its edits before the tab goes, so DisposeImage does not apply them.
                close = view.DiscardChanges();
                if (close) _pictureSessions.Remove(tabId);
            }
        }
        catch (Exception e)
        {
            // nobody awaits this task, so the failure is shown here; the tab stays open with its edits
            FailureReport.Report(e, "image-close");
            Notify(NotificationLevel.Error, Loc.Current.Format("image.notApplied", ("name", Docking.DocumentOf(tabId)?.Title ?? ""), ("reason", e.Message)));
        }
        finally
        {
            _closingImage.Remove(tabId);
        }

        if (close) Docking.CloseTab(tabId);
    }

    /// <summary>Asks for a name and makes a new sprite from the pictures as one undoable step.</summary>
    private async Task SaveAsNewSpriteAsync(Workspace workspace, string from, IReadOnlyList<PixelBuffer> pictures)
    {
        var suggested = FreeName(workspace, from);
        var name = (await (NameAsker?.Invoke(suggested) ?? ProjectDialogs.AskTextAsync(this, Loc.Current[pictures.Count > 1 ? "image.saveNewAllTitle" : "image.saveNewTitle"], Loc.Current["image.saveNewLabel"], suggested, false)))?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        try
        {
            EditCommand command;
            using (workspace.Lock.Read())
            {
                ResourceDuplicate.CheckCopyName(name, false);
                ResourceDuplicate.CheckCopyFree(workspace.Data, "sprites", name);
                command = ResourceTemplates.NewSprite(workspace.Data, name, pictures);
            }

            workspace.Edit(command);
            Notify(NotificationLevel.Info, Loc.Current.Format("image.savedNew", ("name", name)));
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    /// <summary>Test hook: answers the name prompt of Save as new.</summary>
    public Func<string, Task<string?>>? NameAsker { get; set; }

    private static string FreeName(Workspace workspace, string from)
    {
        using (workspace.Lock.Read())
        {
            for (var i = 1; ; i++)
            {
                var name = i == 1 ? from + "_copy" : $"{from}_copy{i}";
                try
                {
                    ResourceDuplicate.CheckCopyFree(workspace.Data, "sprites", name);
                    return name;
                }
                catch (InvalidOperationException)
                {
                    // taken: the next number
                }
            }
        }
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
