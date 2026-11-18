using Avalonia.Input.Platform;
using Avalonia.Controls;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The right-click menu of a type heading in the Explorer (Sprites, Game, Scripts, ...): the commands that act on every entry of the type.</summary>
public sealed partial class MainWindow
{
    private static readonly string[] TypeImports = ["sprites", "sounds", "scripts"];

    private void AddTypeMenu(ContextMenu menu, ExplorerRow row)
    {
        var model = Explorer.Model;
        var isType = row is { Kind: ExplorerRowKind.Type } && model.IsType(row.Key);
        if (!isType && row is not { Kind: ExplorerRowKind.Group, Key: ExplorerModel.DataGroup }) return;
        MenuItem Plain(string text, Action run)
        {
            var item = new MenuItem { Header = text };
            item.Click += (_, _) => run();
            return item;
        }

        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        if (isType)
        {
            var keys = model.KeysOfType(row.Key);
            if (keys.Count > 0 && row.Key != "general") menu.Items.Add(Plain(Loc.Current["cmd.organize.selectContents"], () => Explorer.SelectKeys(model.KeysOf(row), row)));
            if (ResourceTransfer.Supports(row.Key)) menu.Items.Add(Plain(Loc.Current.Format("explorer.exportType", ("type", row.Text)), () => _ = ExportTypeAsync(row.Key)));
            if (TypeImports.Contains(row.Key)) menu.Items.Add(RegistryItem("file.importFiles"));
            if (row.Key != "general" && keys.Count > 0) menu.Items.Add(Plain(Loc.Current["explorer.copyNames"], () => CopyNames(keys)));
            if (ExplorerModel.PrefixOf(row.Key) is { } prefix) menu.Items.Add(Plain(Loc.Current.Format("explorer.onlyType", ("type", row.Text)), () => Explorer.SetFilterText(prefix + ":")));
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Plain(Loc.Current["explorer.expandAll"], () => model.FoldAll(true)));
        menu.Items.Add(Plain(Loc.Current["explorer.collapseAll"], () => model.FoldAll(false)));
    }

    /// <summary>Exports every entry of one type to a folder, the same files as Export Selection writes for them.</summary>
    public async Task ExportTypeAsync(string type)
    {
        if (Workspaces.Current is not { } workspace) return;
        if (!ResourceTransfer.Supports(type))
        {
            Notify(NotificationLevel.Info, Loc.Current.Format("transfer.unsupported", ("type", Loc.Current["explorer.type." + type])));
            return;
        }

        if (await PickFolder(Loc.Current["transfer.pickExport"]) is not { } folder) return;
        await RunOperationAsync(Loc.Current["explorer.type." + type], (progress, token) => ResourceTransfer.ExportAsync(workspace, folder, type, null, progress, token), folder, export: true);
    }

    private void CopyNames(IReadOnlyList<string> keys)
    {
        if (Clipboard is { } clipboard) _ = clipboard.SetTextAsync(string.Join(Environment.NewLine, keys.Select(k => ResourceTypes.SplitKey(k) is var (_, name) ? name : k)));
    }
}
