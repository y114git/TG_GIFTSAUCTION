using Avalonia.Controls;
using G3MStudio.Core.Commands;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio;

public sealed partial class MainWindow
{
    /// <summary>A renamed resource keeps its tabs: they follow the new name, and back on undo.</summary>
    private void FollowRename(string op, EditCommand command)
    {
        if (command is GroupCommand group)
        {
            // An undo walks the items backwards, as the group reverts them: a swap through a temporary name only follows its steps in that order.
            foreach (var item in op == "undo" ? group.Items.Reverse() : group.Items) FollowRename(op, item);
            return;
        }

        if (command is ResourceListCommand { Args: var deleted } && deleted["operation"] == ResourceListCommand.Delete)
        {
            FollowDelete(op, command, ResourceTypes.KeyOf(deleted["type"], deleted["name"]));
            return;
        }

        if (command is not RenameResourceCommand rename) return;
        var (from, to) = op == "undo" ? (rename.Args["new"], rename.Args["old"]) : (rename.Args["old"], rename.Args["new"]);
        var (oldKey, newKey) = (ResourceTypes.KeyOf(rename.Args["type"], from), ResourceTypes.KeyOf(rename.Args["type"], to));
        Docking.Rekey(oldKey, newKey, ForgetTabViews);
        EditQuick(q => q.Rekey(oldKey, newKey));
        if (op != "undo") Explorer.Flash(newKey);
        EditOrganizer(o => o.Rekey(oldKey, newKey));
        Workspaces.Current?.Nicknames.Rekey(oldKey, newKey); // the nickname goes with the resource, and back on undo
        if (rename.Args["type"] == "tileSets" && Workspaces.Current is { } open) AutoTilesOf(open).RenameTileSet(from, to);
    }

    // A deleted resource leaves its folder and collections (a later resource of the same name must not inherit them); undo puts it back.
    private void FollowDelete(string op, EditCommand command, string key)
    {
        if (op == "undo")
        {
            if (!_deletedMembership.Remove(command, out var was)) return;
            EditOrganizer(o => o.Restore(key, was.Folder, was.Collections, was.AlsoIn));
            EditQuick(q => q.Restore(key, was.Tags, was.Favourite));
            Workspaces.Current?.Nicknames.Restore(key, was.Nickname);
            return;
        }

        var organizer = Explorer.Model.Organizer;
        var quick = Explorer.Model.Quick;
        _deletedMembership[command] = (organizer.FolderOf.GetValueOrDefault(key), organizer.FoldersOfKey(key).Skip(1).ToList(), organizer.CollectionsOf(key), quick.TagsOf(key), quick.IsFavourite(key), Workspaces.Current?.Nicknames.Of(key));
        EditOrganizer(o => o.Rekey(key, null));
        EditQuick(q => q.Rekey(key, null));
        Workspaces.Current?.Nicknames.Rekey(key, null); // a delete clears the nickname; a later resource of the same name must not inherit it
    }

    // A new edit drops the redo stack, and an import drops the whole stack; the saved membership of a delete that is gone for good is dropped with it.
    private void PruneDeletedMembership(EditHistory history)
    {
        if (_deletedMembership.Count == 0) return;
        static IEnumerable<EditCommand> Flat(EditCommand c) => c is GroupCommand g ? g.Items.SelectMany(Flat) : [c];
        var live = history.Items.SelectMany(Flat).ToHashSet();
        foreach (var gone in _deletedMembership.Keys.Where(k => !live.Contains(k)).ToList()) _deletedMembership.Remove(gone);
    }

    private readonly Dictionary<EditCommand, (string? Folder, IReadOnlyList<string> AlsoIn, IReadOnlyList<string> Collections, IReadOnlyList<string> Tags, bool Favourite, string? Nickname)> _deletedMembership = [];

    public void ShowPanel(string id) => Docking.ShowPanel(id);

    private void OpenProblem(Problem problem)
    {
        if (problem.Code != "RUN") OpenCodeAt(problem.Source, problem.Line, problem.Column);
        else if (problem.Source == RunProblemSource) Docking.ShowPanel("Output");
        else OpenRunLocation(new GmlLocation(problem.Source, problem.Line));
    }

    /// <summary>Opens a code entry in a tab and puts the caret on a one-based line and column.</summary>
    // A name to select goes with the line it was found on (0 when unknown) or with how many uses come before it in the entry.
    private void OpenCodeAt(string entry, int line, int column, string use = "", int skip = 0, int useLine = 0)
    {
        if (Docking.OpenTab("code", ResourceTypes.KeyOf("code", entry), false) is not { } tab) return;
        Dispatcher.UIThread.Post(() => _ = GoToAsync(tab.Id, line, column, use, skip, useLine), DispatcherPriority.Loaded);
    }

    private async Task GoToAsync(string tabId, int line, int column, string use = "", int skip = 0, int useLine = 0)
    {
        if (!_codeTabs.TryGetValue(tabId, out var editor)) return;
        await editor.Ready; // the text must be in before a position means anything
        if (!_codeTabs.TryGetValue(tabId, out var current) || current != editor) return;
        editor.GoTo(line, column);
        if (use.Length > 0) editor.SelectUse(use, skip, useLine);
    }

    /// <summary>Commits the text typed in the open code editors and the picture edits waiting in image tabs; false (with a notification) when one does not compile.</summary>
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
            Notify(NotificationLevel.Error, Loc.Current.Format("code.failed", ("entry", CodeShown(editor.Entry)), ("reason", Loc.Current["code.notApplied"])));
            return false;
        }

        return ApplyPictureEditors();
    }

    /// <summary>Commits the typed text that compiles before a search. A tab whose text does not compile is searched in its saved state and a notice says so; a search never stops for it.</summary>
    private void ApplyCodeEditorsForSearch()
    {
        var left = new List<string>();
        foreach (var (id, editor) in _codeTabs.ToList())
        {
            if (Docking.Tabs.Get(id) is null) _codeTabs.Remove(id);
            else if (!editor.Apply()) left.Add(editor.Entry);
        }

        if (left.Count > 0) Notify(NotificationLevel.Warning, Loc.Current.Format("usages.unsavedLeftOut", ("entry", string.Join(", ", left.Select(CodeShown)))));
    }

    public void Rename()
    {
        if (Workspaces.Current is null) return;
        // In a code editor F2 renames the name under the caret; elsewhere the selected resource.
        if (FocusManager?.GetFocusedElement() is Avalonia.Controls.Control focused && focused.FindAncestorOfType<CodeEditorTab>() is { } code)
        {
            code.RenameSymbol();
            return;
        }

        if (Explorer.SelectedKeys.Count == 0 && Explorer.SelectedRow is { Kind: ExplorerRowKind.Folder or ExplorerRowKind.Collection } box)
        {
            AskRenameContainer(box);
            return;
        }

        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        AskRenameResource(type, name);
    }

    private void AskRenameResource(string type, string name)
    {
        if (type == "general") return;
        var key = ResourceTypes.KeyOf(type, name);
        _ = new RenameWindow(name, newName => ApplyRename(type, name, newName), label: Loc.Current.Format("rename.label", ("name", Workspaces.Current?.Nicknames.Both(type, name) ?? name)), options: new RenameOptions(RenamePreviewText(type, name), Workspaces.Current?.Nicknames.Of(key),
                (finalName, nickname) => Workspaces.Current?.Nicknames.Set(ResourceTypes.KeyOf(type, finalName), nickname, NameOwner),
                NicknameRefusal(type), Workspaces.Settings.Current.RenameStrings, on => Workspaces.Settings.Update(x => x with { RenameStrings = on }, notify: false))).AskAsync(this);
    }

    private string? NameOwner(string name)
    {
        if (Workspaces.Current is not { } workspace) return null;
        using (workspace.Lock.Read()) return ResourceTypes.KeyOfName(workspace.Data, name);
    }

    /// <summary>The rename dialog's check: a nickname that another resource has, or that is another resource's name, is refused with the resource it clashes with.</summary>
    private Func<string, string, string?> NicknameRefusal(string type) => (finalName, nickname) =>
        Workspaces.Current?.Nicknames.Conflict(ResourceTypes.KeyOf(type, finalName), nickname, NameOwner) is { } other
            ? Loc.Current.Format("rename.nicknameTaken", ("nickname", Nicknames.Clean(nickname)), ("other", ResourceTypes.SplitKey(other) is var (_, name) ? name : other))
            : null;

    /// <summary>The preview line of the rename dialog: the refusal, or how many names change and the first few of them.</summary>
    private Func<string, string?> RenamePreviewText(string type, string name)
    {
        var workspace = Workspaces.Current ?? throw new InvalidOperationException("No workspace is open.");
        // The dialog opens first; the index is made in the background so Rename finds it ready.
        if (!Memory.IsLow) _ = Task.Run(() => { using (workspace.Lock.Read()) ReferenceCache.Of(workspace.Data).Get(workspace.Data); });
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
            Workspaces.Current?.Edit(new RenameResourceCommand(type, name, newName, Workspaces.Settings.Current.RenameStrings));
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
        if (!UrlOpener(Workspaces.UserData.Root)) Notify(NotificationLevel.Info, Workspaces.UserData.Root); // no file manager here: the path is the answer
    }

    public void ReopenTab() => Docking.ReopenClosed(Exists);

    public void TabBack() => Navigate(-1);

    public void TabForward() => Navigate(1);

    public void TabHistoryBack() => Docking.StepHistory(-1);

    public void TabHistoryForward() => Docking.StepHistory(1);

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
        _spriteViews.Remove(tabId);
        ForgetSound(tabId);
        DisposeImage(tabId);
    }

    // A step back or forward is offered only while the history holds a tab that is still open.
    private void UpdateTabHistory()
    {
        if (Registry is null) return;
        void Set(Button button, string id, string context, int direction, Avalonia.Layout.Orientation axis, bool forward)
        {
            var on = Docking.CanStepHistory(direction);
            Registry.SetContext(context, on);
            button.IsEnabled = on;
            button.Content = G3MStudio.Theme.GlyphIcons.Chevron(axis, forward);
            var title = Loc.Current[AppCommands.TitleKey(id)];
            ToolTip.SetTip(button, KeyTips.For(Registry, id, title));
            Avalonia.Automation.AutomationProperties.SetName(button, title);
        }

        Set(TabBackButton, "tabs.historyBack", AppCommands.TabHistoryBack, -1, Avalonia.Layout.Orientation.Horizontal, false);
        Set(TabForwardButton, "tabs.historyForward", AppCommands.TabHistoryForward, 1, Avalonia.Layout.Orientation.Horizontal, true);
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
