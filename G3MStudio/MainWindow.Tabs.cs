using Avalonia.Input.Platform;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

public sealed partial class MainWindow
{
    /// <summary>A renamed resource keeps its tabs: they follow the new name, and back on undo.</summary>
    private void FollowRename(string op, EditCommand command)
    {
        if (command is not RenameResourceCommand rename) return;
        var (from, to) = op == "undo" ? (rename.Args["new"], rename.Args["old"]) : (rename.Args["old"], rename.Args["new"]);
        Docking.Rekey(ResourceTypes.KeyOf(rename.Args["type"], from), ResourceTypes.KeyOf(rename.Args["type"], to), ForgetTabViews);
    }

    public void ShowPanel(string id) => Docking.ShowPanel(id);

    private void OpenProblem(Problem problem) => OpenCodeAt(problem.Source, problem.Line, problem.Column);

    /// <summary>Opens a code entry in a tab and puts the caret on a one-based line and column.</summary>
    private void OpenCodeAt(string entry, int line, int column)
    {
        if (Docking.OpenTab("code", ResourceTypes.KeyOf("code", entry), false) is not { } tab) return;
        Dispatcher.UIThread.Post(() => _ = GoToAsync(tab.Id, line, column), DispatcherPriority.Loaded);
    }

    private async Task GoToAsync(string tabId, int line, int column)
    {
        if (!_codeTabs.TryGetValue(tabId, out var editor)) return;
        await editor.Ready; // the text must be in before a position means anything
        if (_codeTabs.TryGetValue(tabId, out var current) && current == editor) editor.GoTo(line, column);
    }

    /// <summary>Commits the text typed in the open code editors; false (with a notification) when one does not compile.</summary>
    private bool ApplyCodeEditors()
    {
        foreach (var (id, editor) in _codeTabs.ToList())
        {
            if (Docking.Tabs.Get(id) is null)
            {
                _codeTabs.Remove(id);
                continue;
            }

            if (editor.Apply()) continue;
            Notify(NotificationLevel.Error, Loc.Current.Format("code.failed", ("entry", editor.Entry), ("reason", Loc.Current["code.notApplied"])));
            return false;
        }

        return true;
    }

    public void Rename()
    {
        if (Workspaces.Current is null) return;
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name) || type == "general") return;
        _ = new RenameWindow(name, newName => ApplyRename(type, name, newName), preview: RenamePreviewText(type, name)).AskAsync(this);
    }

    /// <summary>The preview line of the rename dialog: the refusal, or how many names change and the first few of them.</summary>
    private Func<string, string?> RenamePreviewText(string type, string name)
    {
        var workspace = Workspaces.Current ?? throw new InvalidOperationException("No workspace is open.");
        // The dialog opens first; the index is made in the background so Rename finds it ready.
        _ = Task.Run(() => { using (workspace.Lock.Read()) ReferenceCache.Of(workspace.Data).Get(workspace.Data); });
        return newName =>
        {
            RenamePreview found;
            using (workspace.Lock.Read()) found = RenamePreview.Of(workspace.Data, type, name, newName, ReferenceCache.Of(workspace.Data).Get(workspace.Data));
            if (found.Refusal is not null) return found.Refusal;
            var more = found.Changes.Count > 6 ? Environment.NewLine + Loc.Current.Format("rename.more", ("count", found.Changes.Count - 6)) : "";
            return Loc.Current.Format("rename.preview", ("count", found.Changes.Count), ("uses", found.Uses)) + Environment.NewLine + string.Join(Environment.NewLine, found.Changes.Take(6)) + more;
        };
    }

    private string? ApplyRename(string type, string name, string newName)
    {
        try
        {
            Workspaces.Current?.Edit(new RenameResourceCommand(type, name, newName));
            return null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return e.Message;
        }
    }

    public void CloseTab()
    {
        if (Docking.ActiveTabId is not { } id) return;
        // Closing a tab applies its typed code first; one that does not compile stays open.
        if (_codeTabs.TryGetValue(id, out var editor) && !editor.Apply()) return;
        Docking.CloseTab(id);
    }

    public void CloseAllTabs() => Docking.CloseAll();

    public void CloseOtherTabs()
    {
        if (Docking.ActiveTabId is { } id && ApplyCodeEditors()) Docking.CloseOthers(id);
    }

    private bool Exists(string kind, string key) => Workspaces.Current is { } workspace && ResourceTypes.Find(workspace.Data, key) is not null;

    public void NextTab() => Docking.CycleTab(1);

    public void PreviousTab() => Docking.CycleTab(-1);

    public void CopyName()
    {
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || Clipboard is not { } clipboard) return;
        _ = clipboard.SetTextAsync(ResourceTypes.SplitKey(key) is var (_, name) ? name : key);
    }

    public void OpenDataFolder()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Workspaces.UserData.Root, UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Notify(NotificationLevel.Info, Workspaces.UserData.Root); // no file manager here: the path is the answer
        }
    }

    public void ReopenTab() => Docking.ReopenClosed(Exists);

    public void TabBack() => Navigate(-1);

    public void TabForward() => Navigate(1);

    // The tab in front shows the other resource, so code typed in it is applied first; one that does not compile stays.
    private void Navigate(int direction)
    {
        if (Docking.ActiveTabId is { } id && _codeTabs.TryGetValue(id, out var editor) && !editor.Apply()) return;
        // What the old content registered for the tab goes before the new content registers its own.
        Docking.Navigate(direction, Exists, ForgetTabViews);
    }

    private void ForgetTabViews(string tabId)
    {
        _codeTabs.Remove(tabId);
        _roomViews.Remove(tabId);
        _frameViews.Remove(tabId);
    }

    public void PinTab()
    {
        if (Docking.ActiveTabId is { } id && Docking.Tabs.Get(id) is { } tab) Docking.SetPinned(id, !tab.Pinned);
    }

    public void KeepTabOpen()
    {
        if (Docking.ActiveTabId is { } id) Docking.KeepOpen(id);
    }

    public void SplitRight()
    {
        if (Docking.ActiveTabId is { } id) Docking.SplitTab(id, Dock.Model.Core.DockOperation.Right);
    }
}
