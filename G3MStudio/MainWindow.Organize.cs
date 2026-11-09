using Avalonia.Controls;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// Organizing the Explorer: the user's folders, collections and tag colours (kept beside the view state, never in the data file),
/// and the bulk operations on a selection, a folder, a collection or a tag.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly (string Name, string Hex)[] Colours =
    [
        ("none", ""), ("red", "#E5534B"), ("orange", "#E0883E"), ("yellow", "#D4B83C"), ("green", "#4CAF72"), ("teal", "#3AAFA9"), ("blue", "#4C8DE0"), ("purple", "#9A6DD7"), ("pink", "#D86AA8"),
    ];

    private QuickAccess NewQuick() => QuickAccess.Empty with { Filter = new ExplorerFilter(Workspaces.Settings.Current.ExplorerSortOrder) };

    private void EditOrganizer(Func<Organizer, Organizer> change)
    {
        var next = change(Explorer.Model.Organizer);
        if (ReferenceEquals(next, Explorer.Model.Organizer)) return;
        Explorer.Model.Organizer = next;
        if (Workspaces.Current is not { } workspace) return;
        try
        {
            _organizers.Save(workspace.Path, next);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notify(NotificationLevel.Warning, e.Message);
        }
    }

    /// <summary>The keys the organizing commands act on: the selected entries, else the entry in front.</summary>
    private IReadOnlyList<string> ActionKeys() => Explorer.SelectedKeys.Count > 0 ? Explorer.SelectedKeys : CurrentKey is { } key ? [key] : [];

    private string? ContainerFolder => Explorer.SelectedRow is { Kind: ExplorerRowKind.Folder } row ? row.Key[ExplorerModel.FolderPrefix.Length..]
        : Explorer.Model.Scope?.StartsWith(ExplorerModel.FolderPrefix, StringComparison.Ordinal) == true ? Explorer.Model.Scope[ExplorerModel.FolderPrefix.Length..] : null;

    private string FolderPath(string id) => string.Join(" / ", Explorer.Model.Organizer.PathOf(id).Select(f => f.Name));

    public void NewFolder() => AskNewFolder(ContainerFolder, Explorer.SelectedKeys);

    /// <summary>Asks for a name and makes the folder under <paramref name="parent"/>; the entries are put in it at once.</summary>
    private void AskNewFolder(string? parent, IReadOnlyList<string> keys) =>
        _ = new RenameWindow("", name =>
        {
            if (Explorer.Model.Organizer.NameProblem(parent, name) is { } problem) return Loc.Current["organize.name." + problem];
            EditOrganizer(o => o.AddFolder(name, parent) is var (next, id) && id is not null && keys.Count > 0 ? next.MoveTo(keys, id) : next);
            return null;
        }, Loc.Current["organize.newFolderTitle"], Loc.Current["organize.folderName"], Loc.Current["organize.create"]).AskAsync(this);

    public void MoveToFolder() => _ = MoveToFolderAsync(ActionKeys());

    private async Task MoveToFolderAsync(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0) return;
        var folders = Explorer.Model.Organizer.Folders.Select(f => (f.Id, Path: FolderPath(f.Id))).OrderBy(f => f.Path, StringComparer.CurrentCultureIgnoreCase).ToList();
        List<string> names = [Loc.Current["organize.topLevel"], .. folders.Select(f => f.Path), Loc.Current["organize.newFolderChoice"]];
        if (await ProjectDialogs.AskChoiceAsync(this, Loc.Current["organize.moveTitle"], Loc.Current.Format("organize.moveLabel", ("count", keys.Count)), names) is not { } pick) return;
        if (pick == names.Count - 1) AskNewFolder(null, keys);
        else MoveTo(keys, pick == 0 ? null : folders[pick - 1].Id);
    }

    private void MoveTo(IReadOnlyList<string> keys, string? folder)
    {
        EditOrganizer(o => o.MoveTo(keys, folder));
        Notify(NotificationLevel.Info, Loc.Current.Format("organize.moved", ("count", keys.Count)));
    }

    public void AddToCollection() => _ = AddToCollectionAsync(ActionKeys());

    private async Task AddToCollectionAsync(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0) return;
        List<string> names = [.. Explorer.Model.Organizer.Collections.Select(c => c.Name), Loc.Current["organize.newCollectionChoice"]];
        if (await ProjectDialogs.AskChoiceAsync(this, Loc.Current["organize.collectionTitle"], Loc.Current.Format("organize.collectionLabel", ("count", keys.Count)), names) is not { } pick) return;
        if (pick < names.Count - 1)
        {
            AddToCollection(names[pick], keys);
            return;
        }

        _ = new RenameWindow("", name =>
        {
            if (Explorer.Model.Organizer.CollectionProblem(name) == "empty") return Loc.Current["organize.name.empty"];
            AddToCollection(name, keys);
            return null;
        }, Loc.Current["organize.newCollectionTitle"], Loc.Current["organize.collectionName"], Loc.Current["organize.create"]).AskAsync(this);
    }

    private void AddToCollection(string name, IReadOnlyList<string> keys)
    {
        EditOrganizer(o => o.AddToCollection(name, keys));
        Notify(NotificationLevel.Info, Loc.Current.Format("organize.added", ("count", keys.Count), ("name", name)));
    }

    /// <summary>Takes the selected entries out of the folder, collection or tag they are listed under.</summary>
    public void RemoveFromGroup()
    {
        if (Explorer.SelectedRow is not { IsItem: true } row || Explorer.ParentOf(row) is not { } parent) return;
        var keys = Explorer.SelectedKeys;
        switch (parent)
        {
            case { Kind: ExplorerRowKind.Folder }:
                var up = Explorer.Model.Organizer.Folder(parent.Key[ExplorerModel.FolderPrefix.Length..])?.Parent;
                EditOrganizer(o => o.MoveTo(keys, up));
                break;
            case { Kind: ExplorerRowKind.Collection }:
                EditOrganizer(o => o.RemoveFromCollection(parent.Key[ExplorerModel.CollectionPrefix.Length..], keys));
                break;
            case { Kind: ExplorerRowKind.Type } when parent.Key.StartsWith(ExplorerModel.TagPrefix, StringComparison.Ordinal):
                EditQuick(q => keys.Aggregate(q, (acc, key) => acc.RemoveTag(key, parent.Key[ExplorerModel.TagPrefix.Length..])));
                break;
            case { Key: ExplorerModel.QuickFavourites }:
                EditQuick(q => keys.Aggregate(q, (acc, key) => acc.IsFavourite(key) ? acc.ToggleFavourite(key) : acc));
                break;
        }
    }

    /// <summary>Entries were dropped on a folder (move), a collection (add) or a tag (tag them).</summary>
    private void DropOntoRow(ExplorerRow row, IReadOnlyList<string> keys)
    {
        switch (row.Kind)
        {
            case ExplorerRowKind.Folder:
                MoveTo(keys, row.Key[ExplorerModel.FolderPrefix.Length..]);
                break;
            case ExplorerRowKind.Collection:
                AddToCollection(row.Key[ExplorerModel.CollectionPrefix.Length..], keys);
                break;
            default:
                var tag = row.Key[ExplorerModel.TagPrefix.Length..];
                EditQuick(q => keys.Aggregate(q, (acc, key) => acc.AddTag(key, tag)));
                break;
        }
    }

    /// <summary>Colours the folder, collection or tag that is selected; with an entry selected, a tag it carries is asked for.</summary>
    public void ColourLabel() => _ = ColourLabelAsync(Explorer.SelectedRow);

    private async Task ColourLabelAsync(ExplorerRow? row)
    {
        string? tag = null;
        if (row is { Kind: ExplorerRowKind.Type } && row.Key.StartsWith(ExplorerModel.TagPrefix, StringComparison.Ordinal)) tag = row.Key[ExplorerModel.TagPrefix.Length..];
        else if (row is not { Kind: ExplorerRowKind.Folder or ExplorerRowKind.Collection })
        {
            var tags = Explorer.Model.Quick.TagNames;
            if (tags.Count == 0 || await ProjectDialogs.AskChoiceAsync(this, Loc.Current["organize.colourTitle"], Loc.Current["organize.colourTagLabel"], tags) is not { } which) return;
            tag = tags[which];
        }

        var names = Colours.Select(c => Loc.Current["organize.colour." + c.Name]).ToList();
        if (await ProjectDialogs.AskChoiceAsync(this, Loc.Current["organize.colourTitle"], Loc.Current["organize.colourPrompt"], names) is not { } pick) return;
        var hex = Colours[pick].Hex;
        if (tag is not null) EditOrganizer(o => o.ColourTag(tag, hex));
        else if (row is { Kind: ExplorerRowKind.Folder }) EditOrganizer(o => o.ColourFolder(row.Key[ExplorerModel.FolderPrefix.Length..], hex));
        else if (row is not null) EditOrganizer(o => o.ColourCollection(row.Key[ExplorerModel.CollectionPrefix.Length..], hex));
    }

    public void ToggleFolderView() => EditOrganizer(o => o with { FolderView = !Explorer.Model.FolderView });

    public void OpenFolder()
    {
        if (Explorer.SelectedRow is { } row && ExplorerModel.ScopeOf(row) is { } scope) Explorer.Model.Scope = scope;
    }

    public void FolderUp()
    {
        var parts = Explorer.Model.Breadcrumb;
        if (Explorer.Model.Scope is not null) Explorer.Model.Scope = parts.Count > 2 ? parts[^2].Scope : null;
    }

    /// <summary>Selects every entry of the type of the selected entry.</summary>
    public void SelectByType()
    {
        if (Explorer.SelectedRow is not { IsItem: true } row) return;
        Explorer.SelectKeys(Explorer.Model.KeysOfType(row.Key), null);
    }

    /// <summary>Selects every entry of the selected folder, collection, tag or type, or of the folder that holds the selected entry.</summary>
    public void SelectContents()
    {
        if (Explorer.SelectedRow is not { } row) return;
        var container = row.IsItem ? Explorer.ParentOf(row) : row;
        if (container is { IsHeader: true } box) Explorer.SelectKeys(Explorer.Model.KeysOf(box), box);
    }

    private static async Task<IReadOnlyList<UsageHit>> FindAllAsync(Workspace workspace, IReadOnlyList<(string Type, string Name)> items, HashSet<(string Type, string Name)> chosen)
    {
        var all = new List<UsageHit>();
        // shortcut: one reference index per entry, as in Delete; share one index when selections of hundreds matter.
        foreach (var (type, name) in items)
            all.AddRange((await UsageSearch.FindAsync(workspace, type, name)).Where(h => h.OwnerType is null || !chosen.Contains((h.OwnerType, h.OwnerName))).Select(h => h with { Where = $"{name}: {h.Where}" }));
        return all;
    }

    public void MoveToTextureGroup() => _ = MoveToGroupAsync(false);

    public void MoveToAudioGroup() => _ = MoveToGroupAsync(true);

    private static readonly Dictionary<string, string> GroupKinds = new() { ["sprites"] = "sprites", ["fonts"] = "fonts", ["tileSets"] = "tilesets" };

    private async Task MoveToGroupAsync(bool audio)
    {
        if (Workspaces.Current is not { } workspace) return;
        var items = BulkResources.Split(ActionKeys()).Where(i => audio ? i.Type == "sounds" : GroupKinds.ContainsKey(i.Type)).ToList();
        string[] groups;
        using (workspace.Lock.Read()) groups = audio ? [.. (workspace.Data.AudioGroups ?? []).Select(g => g.Name.Content ?? "")] : [.. (workspace.Data.TextureGroupInfo ?? []).Select(g => g.Name.Content ?? "")];
        if (items.Count == 0 || groups.Length == 0)
        {
            Notify(NotificationLevel.Info, Loc.Current[audio ? "organize.noSounds" : "organize.noTextureItems"]);
            return;
        }

        if (await ProjectDialogs.AskChoiceAsync(this, Loc.Current[audio ? "organize.audioGroupTitle" : "organize.textureGroupTitle"], Loc.Current.Format("organize.groupLabel", ("count", items.Count)), groups) is not { } pick) return;
        EditCommand[] commands = audio
            ? [new AudioGroupMoveCommand([.. items.Select(i => i.Name)], groups[pick])]
            : [.. items.GroupBy(i => GroupKinds[i.Type]).Select(g => new TextureGroupMoveCommand(g.Key, [.. g.Select(i => i.Name)], groups[pick]))];
        try
        {
            workspace.Edit(commands.Length == 1 ? commands[0] : new GroupCommand(commands));
            Notify(NotificationLevel.Info, Loc.Current.Format("organize.groupMoved", ("count", items.Count), ("group", groups[pick])));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            Notify(NotificationLevel.Error, e.Message);
        }
    }

    private static readonly string[] OrganizeBulk = ["organize.moveToFolder", "organize.addToCollection", "organize.bulkRename", "organize.moveToTextureGroup", "organize.moveToAudioGroup", "organize.selectByType"];

    private static readonly string[] OrganizeSingle = ["organize.moveToFolder", "organize.addToCollection", "organize.bulkRename", "organize.selectByType"];

    /// <summary>A folder, a collection or a tag: a row that holds entries.</summary>
    private static bool IsContainer(ExplorerRow row) =>
        row.Kind is ExplorerRowKind.Folder or ExplorerRowKind.Collection || row.Kind == ExplorerRowKind.Type && row.Key.StartsWith(ExplorerModel.TagPrefix, StringComparison.Ordinal);

    /// <summary>The menu of a folder, collection or tag row: its own commands, then the bulk commands on everything it holds.</summary>
    private void AddContainerMenu(ContextMenu menu, ExplorerRow row)
    {
        MenuItem Plain(string text, Action run)
        {
            var item = new MenuItem { Header = text };
            item.Click += (_, _) => run();
            return item;
        }

        // A bulk command acts on the selection: it first selects what the row holds.
        MenuItem Bulk(string id, string? text = null)
        {
            var item = RegistryItem(id);
            if (text is not null) item.Header = text;
            item.Command = null;
            item.Click += (_, _) =>
            {
                Explorer.SelectKeys(Explorer.Model.KeysOf(row), row);
                Registry.Execute(id);
            };
            return item;
        }

        var isFolder = row.Kind == ExplorerRowKind.Folder;
        if (row.Kind is ExplorerRowKind.Folder or ExplorerRowKind.Collection)
        {
            menu.Items.Add(Plain(Loc.Current["organize.openFolderMenu"], () => Explorer.Model.Scope = row.Key));
            if (isFolder) menu.Items.Add(Plain(Loc.Current["organize.newSubfolder"], () => AskNewFolder(row.Key[ExplorerModel.FolderPrefix.Length..], [])));
            menu.Items.Add(Plain(Loc.Current["cmd.edit.rename"], () => AskRenameContainer(row)));
            menu.Items.Add(Plain(Loc.Current["cmd.organize.colourLabel"], () => _ = ColourLabelAsync(row)));
            menu.Items.Add(Plain(Loc.Current[isFolder ? "organize.deleteFolder" : "organize.deleteCollection"], () => _ = DeleteContainerAsync(row)));
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Plain(Loc.Current["cmd.organize.selectContents"], () => Explorer.SelectKeys(Explorer.Model.KeysOf(row), row)));
        foreach (var id in new[] { "file.exportSelection", "edit.copyToOtherFile", "organize.bulkRename", "organize.moveToFolder", "organize.addToCollection", "organize.moveToTextureGroup", "organize.moveToAudioGroup", "organize.exportPackage" })
            menu.Items.Add(Bulk(id));
        menu.Items.Add(Bulk("edit.delete", Loc.Current["organize.deleteEntries"]));
    }

    private async Task DeleteContainerAsync(ExplorerRow row)
    {
        if (row.Kind == ExplorerRowKind.Folder)
        {
            EditOrganizer(o => o.DeleteFolder(row.Key[ExplorerModel.FolderPrefix.Length..]));
            return;
        }

        if (!await Confirm(Loc.Current.Format("organize.deleteCollectionTitle", ("name", row.Text)), Loc.Current["organize.deleteCollectionBody"])) return;
        EditOrganizer(o => o.DeleteCollection(row.Key[ExplorerModel.CollectionPrefix.Length..]));
    }

    private void AskRenameContainer(ExplorerRow row)
    {
        var folder = row.Kind == ExplorerRowKind.Folder;
        var id = row.Key[(folder ? ExplorerModel.FolderPrefix : ExplorerModel.CollectionPrefix).Length..];
        _ = new RenameWindow(row.Text, name =>
        {
            var o = Explorer.Model.Organizer;
            var problem = folder ? o.NameProblem(o.Folder(id)?.Parent, name, id) : o.CollectionProblem(name, id);
            if (problem is not null) return Loc.Current["organize.name." + problem];
            EditOrganizer(x => folder ? x.RenameFolder(id, name) : x.RenameCollection(id, name));
            return null;
        }).AskAsync(this);
    }

    public void BulkRename()
    {
        if (Workspaces.Current is not { } workspace) return;
        var items = BulkResources.Split(ActionKeys()).Where(i => i.Type != "general" && ResourceListCommand.Deletable.Contains(i.Type)).ToList();
        if (items.Count == 0) return;
        _ = new BulkRenameWindow(items, (type, name) =>
        {
            using (workspace.Lock.Read()) return CopyPlanner.Exists(workspace.Data, type, name);
        }, steps =>
        {
            if (BulkRename_Command(steps) is not { } command) return Loc.Current["organize.nothingToRename"];
            try
            {
                workspace.Edit(command);
                Notify(NotificationLevel.Info, Loc.Current.Format("organize.renamed", ("count", steps.Count(s => s.Runs))));
                return null;
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                return e.Message;
            }
        }).AskAsync(this);
    }

    private static EditCommand? BulkRename_Command(IReadOnlyList<RenameStep> steps) => Core.Workspace.BulkRename.Command(steps);
}
