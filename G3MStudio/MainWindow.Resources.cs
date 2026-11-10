using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Settings;

namespace G3MStudio;

public partial class MainWindow
{
    /// <summary>Asks for a name, adds an empty resource and opens it.</summary>
    public void NewResource(string type)
    {
        if (Workspaces.Current is not { } workspace || !ResourceListCommand.Creatable.Contains(type)) return;
        var label = Loc.Current["explorer.typeOne." + type];
        if (!ResourceListCommand.CanHold(workspace.Data, type))
        {
            Notify(NotificationLevel.Info, Loc.Current.Format("explorer.noSection", ("type", label)));
            return;
        }

        var dialog = new RenameWindow("", name => CreateResource(workspace, type, name), Loc.Current.Format("explorer.newTitle", ("type", label)), Loc.Current.Format("explorer.newLabel", ("type", label)), Loc.Current["explorer.newOk"]);
        _ = AskNameAsync(dialog);
    }

    private string? _unusualName;

    // The warning about an unusual name belongs to one dialog: a later dialog starts with a clean slate.
    private async Task AskNameAsync(RenameWindow dialog)
    {
        try
        {
            await dialog.AskAsync(this);
        }
        finally
        {
            _unusualName = null;
        }
    }

    internal string? CreateResource(Workspace workspace, string type, string name)
    {
        if (!ResourceListCommand.CanHold(workspace.Data, type)) return Loc.Current.Format("explorer.noSection", ("type", Loc.Current["explorer.typeOne." + type]));
        try
        {
            // An unusual name is warned about once in the dialog; the same name sent again, or the setting that allows risky actions, goes through.
            var choice = RiskPolicy.Of(Workspaces.Settings.Current);
            if (!workspace.Edit(ResourceTemplates.New(workspace.Data, type, name, choice == RiskChoice.Allow || choice == RiskChoice.Ask && _unusualName == name))) return Queued();
        }
        catch (UnusualNameException e)
        {
            if (RiskPolicy.Of(Workspaces.Settings.Current) == RiskChoice.Refuse) return e.Message;
            _unusualName = name;
            return Loc.Current.Format(e.Taken ? "explorer.nameTaken" : "explorer.nameUnusual", ("name", name));
        }
        catch (InvalidOperationException e)
        {
            return e.Message;
        }

        _unusualName = null;
        var key = ResourceTypes.KeyOf(type, name);
        Explorer.Select(key);
        Docking.OpenTab("resource", key, false);
        return null;
    }

    // The edit waits while the file is saved or exported, so there is nothing to select or open yet.
    private string? Queued()
    {
        _unusualName = null;
        Notify(NotificationLevel.Info, Loc.Current["app.editQueued"]);
        return null;
    }

    /// <summary>Asks for a name and copies the selected object or script; the copy opens when it is made.</summary>
    public void DuplicateResource()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (Explorer.SelectedKeys.Count > 1)
        {
            DuplicateMany(workspace, BulkResources.Split(Explorer.SelectedKeys));
            return;
        }

        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name) || !ResourceDuplicate.All.Contains(type)) return;
        _ = AskNameAsync(new RenameWindow("", copy => CopyResource(workspace, type, name, copy), Loc.Current.Format("explorer.duplicateTitle", ("name", name)), Loc.Current["explorer.duplicateLabel"], Loc.Current["explorer.newOk"]));
    }

    private string? CopyResource(Workspace workspace, string type, string name, string copy)
    {
        try
        {
            // The copy's code is decompiled from the model first; a name that is taken or not valid is refused by the command's first step before anything changes.
            // An unusual name is warned about once in the dialog; the same name sent again, or the setting that allows risky actions, goes through.
            var choice = RiskPolicy.Of(Workspaces.Settings.Current);
            var allow = choice == RiskChoice.Allow || choice == RiskChoice.Ask && _unusualName == copy;
            bool viaCopier;
            using (workspace.Lock.Read()) viaCopier = ResourceDuplicate.NeedsCopier(workspace.Data, type, name);
            if (viaCopier)
            {
                // The package is made off the UI thread: the dialog closes now and a failure is reported as a message.
                ResourceDuplicate.CheckCopyName(copy, allow);
                using (workspace.Lock.Read()) ResourceDuplicate.CheckCopyFree(workspace.Data, type, copy);
                _ = GuardedAsync(() => DuplicateViaCopierAsync(workspace, [(type, name, copy)], ResourceTypes.KeyOf(type, copy), allow));
                return null;
            }

            EditCommand command;
            using (workspace.Lock.Read()) command = ResourceDuplicate.Build(workspace.Data, type, name, copy, allow);
            if (!workspace.Edit(command)) return Queued();
        }
        catch (UnusualNameException e)
        {
            if (RiskPolicy.Of(Workspaces.Settings.Current) == RiskChoice.Refuse) return e.Message;
            _unusualName = copy;
            return Loc.Current.Format(e.Taken ? "explorer.nameTaken" : "explorer.nameUnusual", ("name", copy));
        }
        catch (Exception e) when (e is InvalidOperationException or CodeCompileException)
        {
            return e.Message;
        }

        _unusualName = null;
        var key = ResourceTypes.KeyOf(type, copy);
        Explorer.Select(key);
        Docking.OpenTab("resource", key, false);
        return null;
    }

    // Duplicates through the copy between files, with this file as source and target; the copy is one undo step. A name check that fails is reported here, as the dialog is gone.
    private async Task DuplicateViaCopierAsync(Workspace workspace, IReadOnlyList<(string Type, string Name, string Copy)> items, string openKey, bool allowUnusual = false)
    {
        try
        {
            var command = await ResourceDuplicate.BuildCopyAsync(workspace, items, allowUnusual);
            await workspace.EditInBackground(command);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or InvalidDataException)
        {
            Notify(NotificationLevel.Error, e.Message);
            return;
        }

        Explorer.Select(openKey);
        Docking.OpenTab("resource", openKey, false);
    }

    /// <summary>Deletes the selected resource after a question, or says why it cannot go: a resource that something uses stays.</summary>
    public void MoveResourceUp() => MoveResource(-1);

    public void MoveResourceDown() => MoveResource(1);

    // Swaps the selected entry with its neighbour; at the end of the list nothing happens.
    private void MoveResource(int by)
    {
        if (Workspaces.Current is not { } workspace) return;
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name) || !ResourceMoveCommand.CanMove(workspace.Data, type, name, by)) return;
        workspace.Edit(new ResourceMoveCommand(type, name, by));
    }

    /// <summary>Deletes every selected resource as one undo step, after one question that lists what uses them.</summary>
    private async Task DeleteManyAsync(Workspace workspace, IReadOnlyList<(string Type, string Name)> items)
    {
        var chosen = items.ToHashSet();
        var used = new List<(string Type, string Name, bool Used)>();
        var places = new List<string>();
        // shortcut: one reference index per resource (about 160 ms each on a large game); share one index when selections of hundreds matter.
        foreach (var (type, name) in items)
        {
            var outside = (await UsageSearch.FindAsync(workspace, type, name)).Where(h => h.OwnerType is null || !chosen.Contains((h.OwnerType, h.OwnerName))).ToList();
            used.Add((type, name, outside.Count > 0));
            places.AddRange(outside.Select(h => $"  {name} < {h.OwnerType} {h.OwnerName}: {h.Where}"));
        }

        var list = string.Join(Environment.NewLine, items.Take(8).Select(i => "  " + i.Name)) + (items.Count > 8 ? Environment.NewLine + $"  ... +{items.Count - 8}" : "");
        var uses = places.Count == 0 ? "" : Environment.NewLine + Loc.Current.Format("explorer.deleteManyUses", ("count", places.Count), ("places", string.Join(Environment.NewLine, places.Take(8)) + (places.Count > 8 ? Environment.NewLine + $"  ... +{places.Count - 8}" : "")));
        if (!await Confirm(Prompts.DeleteMany, Loc.Current.Format("explorer.deleteManyTitle", ("count", items.Count)), Loc.Current.Format("explorer.deleteMany", ("count", items.Count), ("names", list)) + uses, "common.delete")) return;
        if (!await EditOrForceAsync(workspace, keep => BulkResources.Delete(used, keep), string.Join(", ", items.Take(3).Select(i => i.Name)))) return;

        foreach (var tab in Docking.Tabs.Tabs.Where(t => ResourceTypes.SplitKey(t.Key ?? "") is var (type, name) && chosen.Contains((type, name))).ToList()) Docking.CloseTab(tab.Id);
        // The folder, collection and tag entries stay: rows skip keys that do not exist, and undoing the delete brings the membership back.
    }

    /// <summary>Copies every selected object or script as one undo step; the copies are named name_copy.</summary>
    private void DuplicateMany(Workspace workspace, IReadOnlyList<(string Type, string Name)> items)
    {
        try
        {
            EditCommand command;
            IReadOnlyList<string> keys;
            List<(string Type, string Name, string Copy)> viaCopier = [];
            using (workspace.Lock.Read())
            {
                var taken = new HashSet<string>(StringComparer.Ordinal);
                foreach (var (type, name) in items.Where(i => ResourceDuplicate.All.Contains(i.Type) && ResourceDuplicate.NeedsCopier(workspace.Data, i.Type, i.Name)))
                    viaCopier.Add((type, name, BulkResources.CopyName(workspace.Data, name, taken)));
                var plain = items.Where(i => ResourceDuplicate.Types.Contains(i.Type) && !ResourceDuplicate.NeedsCopier(workspace.Data, i.Type, i.Name)).ToList();
                command = BulkResources.Duplicate(workspace.Data, plain, out keys);
            }

            if (keys.Count > 0)
            {
                if (workspace.Edit(command))
                {
                    Notify(NotificationLevel.Info, Loc.Current.Format("explorer.duplicatedMany", ("count", keys.Count)));
                    Explorer.Select(keys[^1]);
                }
                else Queued();
            }

            if (viaCopier.Count > 0) _ = GuardedAsync(() => DuplicateViaCopierAsync(workspace, viaCopier, ResourceTypes.KeyOf(viaCopier[^1].Type, viaCopier[^1].Copy), RiskPolicy.Of(Workspaces.Settings.Current) == RiskChoice.Allow));
        }
        catch (Exception e) when (e is InvalidOperationException or CodeCompileException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    public void DeleteResource()
    {
        if (Workspaces.Current is not { } workspace) return;
        if (Explorer.SelectedKeys.Count == 0 && Explorer.SelectedRow is { Kind: ExplorerRowKind.Folder or ExplorerRowKind.Collection } box)
        {
            _ = DeleteContainerAsync(box);
            return;
        }

        if (Explorer.SelectedKeys.Count > 1)
        {
            _ = DeleteManyAsync(workspace, [.. BulkResources.Split(Explorer.SelectedKeys).Where(i => ResourceListCommand.Deletable.Contains(i.Type))]);
            return;
        }

        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name) || !ResourceListCommand.Deletable.Contains(type)) return;
        _ = DeleteAsync(workspace, key, type, name);
    }

    private async Task DeleteAsync(Workspace workspace, string key, string type, string name)
    {
        var target = ResourceTypes.Find(workspace.Data, key);
        if (target is null) return;
        var hits = await UsageSearch.FindAsync(workspace, type, name);
        var places = string.Join(Environment.NewLine, hits.Take(8).Select(h => $"  {h.OwnerType} {h.OwnerName}: {h.Where}")) + (hits.Count > 8 ? Environment.NewLine + $"  ... +{hits.Count - 8}" : "");
        // A resource in use is deleted only after the owner has seen where it is used and what will change; the command clears or removes those uses first.
        var question = hits.Count == 0
            ? (Loc.Current.Format("explorer.deleteTitle", ("name", name)), Loc.Current.Format("explorer.deleteBody", ("name", name)))
            : (Loc.Current.Format("explorer.deleteUsedTitle", ("name", name)), Loc.Current.Format("explorer.deleteUsed", ("name", name), ("count", hits.Count), ("places", places)));
        if (!await Confirm(Prompts.DeleteResource, question.Item1, question.Item2, "common.delete")) return;
        if (!await EditOrForceAsync(workspace, keep => hits.Count == 0 ? new ResourceListCommand(type, ResourceListCommand.Delete, name) : new GroupCommand([new ClearReferencesCommand(type, name, keep), new ResourceListCommand(type, ResourceListCommand.Delete, name)]), name)) return;

        foreach (var tab in Docking.Tabs.Tabs.Where(t => t.Key == key).ToList()) Docking.CloseTab(tab.Id);
    }

    /// <summary>The right-click menu of the Explorer: the commands that act on the entry, and New on a type heading.</summary>
    private void ShowExplorerMenu(ExplorerRow row, Control anchor)
    {
        if (Workspaces.Current is null) return;
        var menu = new ContextMenu();
        MenuItem Item(string id) => RegistryItem(id);
        if (row.IsItem && Explorer.SelectedKeys.Count > 1)
        {
            // Several selected: only the commands that work on all of them.
            menu.Items.Add(Item("file.exportSelection"));
            menu.Items.Add(Item("edit.duplicate"));
            menu.Items.Add(Item("edit.copyToOtherFile"));
            menu.Items.Add(new Separator());
            foreach (var id in OrganizeBulk) menu.Items.Add(Item(id));
            if (Explorer.ParentOf(row) is { } holder && IsContainer(holder)) menu.Items.Add(Item("organize.removeFromGroup"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("edit.delete"));
        }
        else if (row.IsItem)
        {
            foreach (var entry in ResourceMenuFor(ResourceTypes.KeyOf(row.Key, row.Text))) menu.Items.Add(entry);
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("file.exportSelection"));
            menu.Items.Add(new Separator());
            foreach (var id in OrganizeSingle) menu.Items.Add(Item(id));
            if (Explorer.ParentOf(row) is { } owner && IsContainer(owner)) menu.Items.Add(Item("organize.removeFromGroup"));
            menu.Items.Add(new Separator());
            if (ResourceMoveCommand.CanMove(Workspaces.Current.Data, row.Key, row.Text, -1)) menu.Items.Add(Item("edit.moveUp"));
            if (ResourceMoveCommand.CanMove(Workspaces.Current.Data, row.Key, row.Text, 1)) menu.Items.Add(Item("edit.moveDown"));
            if (ResourceListCommand.Deletable.Contains(row.Key)) menu.Items.Add(Item("edit.delete"));
        }

        if (IsContainer(row) || row.Kind == ExplorerRowKind.Type && row.Key == ExplorerModel.QuickFolders) AddContainerMenu(menu, row);

        if (row.Kind == ExplorerRowKind.Type && row.Key == ExplorerModel.QuickRecent)
        {
            var recent = new MenuItem { Header = Loc.Current["organize.openFolderMenu"] };
            recent.Click += (_, _) => Explorer.Model.Scope = ExplorerModel.QuickRecent;
            menu.Items.Add(recent);
        }

        if (row.Kind == ExplorerRowKind.Search)
        {
            var open = new MenuItem { Header = Loc.Current["organize.openFolderMenu"] };
            open.Click += (_, _) => Explorer.Model.Scope = ExplorerModel.SearchPrefix + row.Text;
            menu.Items.Add(open);
            var remove = new MenuItem { Header = Loc.Current["explorer.removeSearch"] };
            remove.Click += (_, _) => EditQuick(q => q.RemoveSearch(row.Text));
            menu.Items.Add(remove);
        }

        if (ResourceListCommand.Creatable.Contains(row.Key))
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            menu.Items.Add(Item("file.new." + row.Key));
        }

        if (menu.Items.Count > 0) menu.Open(anchor);
    }
}
