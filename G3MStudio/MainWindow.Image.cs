using Avalonia.Threading;
using Avalonia.Controls;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Opens the image editor on a sprite frame; each tab owns its session and frees it when the tab closes.</summary>
public sealed partial class MainWindow
{
    private int _imageFrame;

    /// <summary>Opens the image editor tab for the sprite in front, on the frame picked in its strip.</summary>
    public void EditImage()
    {
        if (Docking.ActiveTabId is not { } id || !_frameViews.TryGetValue(id, out var view)
            || Docking.Tabs.Get(id)?.Key is not { } key || ResourceTypes.SplitKey(key) is not var (_, sprite))
        {
            Notify(NotificationLevel.Info, Loc.Current["sprites.noSprite"]);
            return;
        }

        _imageFrame = view.SelectedFrame;
        Docking.OpenTab("image", ResourceTypes.KeyOf("sprites", sprite), false);
    }

    private Control ImageTab(TabInfo tab, Workspace workspace, string sprite)
    {
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

    /// <summary>Runs an image editor command on the image tab in front.</summary>
    public void ImageCommand(string id)
    {
        if (Docking.ActiveTabId is { } tab && _imageViews.TryGetValue(tab, out var view)) view.Run(id);
    }

    private void DisposeImage(string tabId)
    {
        if (!_imageViews.Remove(tabId, out var view)) return;
        view.Dispose();
    }

    private void PruneImages(HashSet<string> open)
    {
        foreach (var id in _imageViews.Keys.Where(id => !open.Contains(id)).ToList()) DisposeImage(id);
    }
}
