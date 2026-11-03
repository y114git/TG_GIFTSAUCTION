using Avalonia.Controls;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Commands;
using G3MStudio.Core.Notifications;

namespace G3MStudio;

public partial class MainWindow
{
    /// <summary>Asks for a name, adds an empty resource and opens it.</summary>
    public void NewResource(string type)
    {
        if (Workspaces.Current is not { } workspace || !ResourceListCommand.Creatable.Contains(type)) return;
        var label = Loc.Current["explorer.type." + type];
        var dialog = new RenameWindow("", name => CreateResource(workspace, type, name), Loc.Current.Format("explorer.newTitle", ("type", label)), Loc.Current.Format("explorer.newLabel", ("type", label)), Loc.Current["explorer.newOk"]);
        _ = dialog.AskAsync(this);
    }

    private string? CreateResource(Workspace workspace, string type, string name)
    {
        try
        {
            workspace.Edit(ResourceTemplates.New(workspace.Data, type, name));
        }
        catch (InvalidOperationException e)
        {
            return e.Message;
        }

        var key = ResourceTypes.KeyOf(type, name);
        Explorer.Select(key);
        Docking.OpenTab("resource", key, false);
        return null;
    }

    /// <summary>Asks for a name and copies the selected object or script; the copy opens when it is made.</summary>
    public void DuplicateResource()
    {
        if (Workspaces.Current is not { } workspace) return;
        var key = Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);
        if (key is null || ResourceTypes.SplitKey(key) is not var (type, name) || !ResourceDuplicate.Types.Contains(type)) return;
        _ = new RenameWindow("", copy => CopyResource(workspace, type, name, copy), Loc.Current.Format("explorer.duplicateTitle", ("name", name)), Loc.Current["explorer.duplicateLabel"], Loc.Current["explorer.newOk"]).AskAsync(this);
    }

    private string? CopyResource(Workspace workspace, string type, string name, string copy)
    {
        try
        {
            // The copy's code is decompiled from the model first; a name that is taken or not valid is refused by the command's first step before anything changes.
            EditCommand command;
            using (workspace.Lock.Read()) command = ResourceDuplicate.Build(workspace.Data, type, name, copy);
            workspace.Edit(command);
        }
        catch (Exception e) when (e is InvalidOperationException or CodeCompileException)
        {
            return e.Message;
        }

        var key = ResourceTypes.KeyOf(type, copy);
        Explorer.Select(key);
        Docking.OpenTab("resource", key, false);
        return null;
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

    public void DeleteResource()
    {
        if (Workspaces.Current is not { } workspace) return;
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
        if (!await Confirm(question.Item1, question.Item2)) return;
        try
        {
            workspace.Edit(hits.Count == 0 ? new ResourceListCommand(type, ResourceListCommand.Delete, name) : new GroupCommand([new ClearReferencesCommand(type, name), new ResourceListCommand(type, ResourceListCommand.Delete, name)]));
        }
        catch (InvalidOperationException e)
        {
            Notify(NotificationLevel.Error, e.Message);
            return;
        }

        foreach (var tab in Docking.Tabs.Tabs.Where(t => t.Key == key).ToList()) Docking.CloseTab(tab.Id);
    }

    /// <summary>The right-click menu of the Explorer: the commands that act on the entry, and New on a type heading.</summary>
    private void ShowExplorerMenu(ExplorerRow row, Control anchor)
    {
        if (Workspaces.Current is null) return;
        var menu = new ContextMenu();
        MenuItem Item(string id) => new() { Header = Loc.Current[AppCommands.TitleKey(id)], InputGesture = Shortcuts.Parse(Registry.Get(id).Shortcut), Command = new RegistryCommand(Registry, Registry.Get(id)) };
        if (row.IsItem)
        {
            if (row.Key != "general") menu.Items.Add(Item("edit.rename"));
            menu.Items.Add(Item("edit.copyName"));
            menu.Items.Add(Item("edit.findUsages"));
            menu.Items.Add(Item("file.exportSelection"));
            if (ResourceDuplicate.Types.Contains(row.Key)) menu.Items.Add(Item("edit.duplicate"));
            if (ResourceMoveCommand.CanMove(Workspaces.Current.Data, row.Key, row.Text, -1)) menu.Items.Add(Item("edit.moveUp"));
            if (ResourceMoveCommand.CanMove(Workspaces.Current.Data, row.Key, row.Text, 1)) menu.Items.Add(Item("edit.moveDown"));
            if (ResourceListCommand.Deletable.Contains(row.Key)) menu.Items.Add(Item("edit.delete"));
        }

        if (ResourceListCommand.Creatable.Contains(row.Key))
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            menu.Items.Add(Item("file.new." + row.Key));
        }

        if (menu.Items.Count > 0) menu.Open(anchor);
    }
}
