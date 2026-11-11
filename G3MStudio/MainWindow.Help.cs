using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Help menu and the Edit commands that go to the text editor having the focus.</summary>
public sealed partial class MainWindow
{
    public const string DocumentationUrl = "https://g3m.gitbook.io/g3mwiki/main/g3mstudio";

    public void ShowAbout() => _ = new AboutWindow().ShowDialog(this);

    /// <summary>Opens an address in the default browser; tests replace it. False when the machine has no handler.</summary>
    public static Func<string, bool> UrlOpener { get; set; } = url =>
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    };

    /// <summary>Opens a page of the GML manual in the browser.</summary>
    public void OpenManual(string address)
    {
        if (!UrlOpener(address)) Notify(NotificationLevel.Warning, Loc.Current.Format("help.noBrowser", ("url", address)));
    }

    public void OpenDocumentation()
    {
        if (!UrlOpener(DocumentationUrl)) Notify(NotificationLevel.Warning, Loc.Current.Format("help.noBrowser", ("url", DocumentationUrl)));
    }

    public void FindUsages()
    {
        // The same target as the Inspector: the tab in front, else the Explorer selection.
        // In a code editor the name under the caret is searched: a resource, or a variable or function used in code.
        if (ActiveCodeTab is { } code && Workspaces.Current is { } open && GmlNames.WordAt(code.Text, code.Editor.CaretOffset) is { } word)
        {
            _ = ShowNameUsagesAsync(open, word);
            return;
        }

        // Several selected entries: the places that use any of them, apart from the selection itself.
        if (Explorer.SelectedKeys.Count > 1 && Workspaces.Current is { } many && BulkResources.Split(Explorer.SelectedKeys) is { Count: > 1 } items && ApplyCodeEditors())
        {
            Docking.ShowPanel("Usages");
            _ = _usages.ShowAsync(Loc.Current.Format("organize.selectionName", ("count", items.Count)), FindAllAsync(many, items, [.. items]));
            return;
        }

        var key = (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null) ?? Explorer.SelectedKey;
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not var (type, name) || !ApplyCodeEditors()) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowAsync(name, UsageSearch.FindAsync(workspace, type, name));
    }

    /// <summary>Lists the assets the resource in front uses; a double click on a row opens that asset.</summary>
    public void FindUses()
    {
        var key = (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null) ?? Explorer.SelectedKey;
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not var (type, name) || !ApplyCodeEditors()) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowUsesAsync(name, UsesWithTypes(UsageSearch.FindUsesAsync(workspace, type, name)));
    }

    /// <summary>The picture of an entry (tooltips, hover cards, Go To), shared with the Explorer rows; null when it has none.</summary>
    private Task<Avalonia.Media.IImage?> LoadThumbnailAsync(string key) => _thumbs?.GetAsync(key) ?? Task.FromResult<Avalonia.Media.IImage?>(null);

    private static async Task<IReadOnlyList<UsageHit>> UsesWithTypes(Task<IReadOnlyList<UsageHit>> search) =>
        [.. (await search).Select(h => h with { Where = Loc.Current["explorer.type." + h.OwnerType] + "  " + h.Where })];

    private TilePickerView TilePicker(GameMakerBackground background, Workspace workspace)
    {
        var set = ResourceTypes.NameOf(background);
        var view = new TilePickerView(background, _textures, workspace, AutoTilesOf(workspace), () => ReplaceItemPictureAsync(workspace, "tileSets", set), () => EditImageOf("tileSets", set));
        if (view.AutoTiles is { } rules) rules.FilePicker = (title, save) => PickPatchFile(title, save, ".json");
        view.FindRequested += index =>
        {
            Docking.ShowPanel("Usages");
            _ = _usages.ShowAsync(set + " #" + index, UsageSearch.FindTileAsync(workspace, set, index));
        };
        return view;
    }

    public void FindChildren()
    {
        var key = (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null) ?? Explorer.SelectedKey;
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not ("objects", var name)) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowAsync(name, UsageSearch.FindChildrenAsync(workspace, name));
    }

    /// <summary>Deletes the selected unreferenced assets through the delete question as one undo step, then lists what is left.</summary>
    private async Task RemoveUnreferencedAsync(IReadOnlyList<UsageHit> hits)
    {
        if (Workspaces.Current is not { } workspace || !ApplyCodeEditors()) return;
        await DeleteManyAsync(workspace, [.. hits.SelectMany(h => h.OwnerType is { } type ? new[] { (type, h.OwnerName) } : [])]);
        FindUnreferenced();
    }

    public void FindUnreferenced()
    {
        if (Workspaces.Current is not { } workspace || !ApplyCodeEditors()) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowUnreferencedAsync(UsageSearch.FindUnreferencedAsync(workspace));
    }

    /// <summary>Opens the entry an Explorer key names, in its code editor or its property editor.</summary>
    private void FindUsagesOfKey(string key)
    {
        if (Workspaces.Current is not { } workspace || ResourceTypes.SplitKey(key) is not var (type, name) || !ApplyCodeEditors()) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowAsync(name, UsageSearch.FindAsync(workspace, type, name));
    }

    private void OpenEntry(string key)
    {
        if (ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (type == "code") OpenCodeAt(name, 1, 1);
        else Docking.OpenTab("resource", key, false);
    }

    private void OpenUsage(UsageHit hit)
    {
        if (hit.OwnerType == "code") OpenCodeAt(hit.OwnerName, LineOf(hit.Where), 1);
        else if (hit.OwnerType is { } type) Docking.OpenTab("resource", ResourceTypes.KeyOf(type, hit.OwnerName), false);
    }

    public void ShowSearch() => Docking.ShowPanel("Search");

    public void ShowReplace()
    {
        ShowSearch();
        _search.FocusReplace();
    }

    public void SelectAll()
    {
        switch (FocusManager?.GetFocusedElement())
        {
            case TextBox box: box.SelectAll(); break;
            case Control control when control.FindAncestorOfType<ImageEditorView>() is { } image: image.Run("img.selectAll"); break;
            case Control control when control.FindAncestorOfType<CodeEditorTab>() is { } tab: tab.Editor.SelectAll(); break;
        }
    }

    public void Find()
    {
        // The Explorer filter is the search box of the Explorer; anything else that has the focus goes to the code tab in front.
        if (FocusManager?.GetFocusedElement() is Control focused && focused.FindAncestorOfType<ExplorerView>() is not null) Explorer.FocusFilter();
        else if (Docking.ActiveTabId is { } id && _codeTabs.TryGetValue(id, out var tab)) tab.ShowFind();
        else Explorer.FocusFilter();
    }
}
