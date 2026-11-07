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
    public const string DocumentationUrl = "https://github.com/y114git/G3MWiki";

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

    /// <summary>The picture for an Explorer tooltip: the cut of the texture page that holds the entry's page item, or null.</summary>
    private async Task<Avalonia.Media.IImage?> LoadThumbnailAsync(string key)
    {
        if (Workspaces.Current is not { } workspace || Memory.IsLow) return null; // a tooltip picture is optional
        Avalonia.Media.Imaging.Bitmap? page = null;
        G3MLib.DataFile.Models.GameMakerTexturePageItem? item;
        using (workspace.Lock.Read()) item = ResourceThumbnails.ItemOf(ResourceTypes.Find(workspace.Data, key));
        if (item?.TexturePage is { } texture) page = await _textures.GetAsync(texture);
        if (page is null || item is null) return null;
        var area = new Avalonia.PixelRect(item.SourceX, item.SourceY, item.SourceWidth, item.SourceHeight).Intersect(new Avalonia.PixelRect(page.PixelSize));
#pragma warning disable CA2000 // the page bitmap belongs to the shared cache; the crop only views it
        Avalonia.Media.Imaging.CroppedBitmap? crop = area.Width > 0 && area.Height > 0 ? new Avalonia.Media.Imaging.CroppedBitmap(page, area) : null;
#pragma warning restore CA2000
        return crop;
    }

    private static async Task<IReadOnlyList<UsageHit>> UsesWithTypes(Task<IReadOnlyList<UsageHit>> search) =>
        [.. (await search).Select(h => h with { Where = Loc.Current["explorer.type." + h.OwnerType] + "  " + h.Where })];

    private TilePickerView TilePicker(GameMakerBackground background, Workspace workspace)
    {
        var view = new TilePickerView(background, _textures, workspace);
        var set = ResourceTypes.NameOf(background);
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

    public void FindUnreferenced()
    {
        if (Workspaces.Current is not { } workspace || !ApplyCodeEditors()) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowUnreferencedAsync(UsageSearch.FindUnreferencedAsync(workspace));
    }

    /// <summary>Opens the entry an Explorer key names, in its code editor or its property editor.</summary>
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
