using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
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

    public void ShowAbout() => _ = new AboutWindow(Workspaces.Settings.Current.InstallId).ShowDialog(this);

    public void SendFeedback() => _ = new FeedbackWindow(CrashReporting.Active, (text, contact, diagnostics) =>
    {
        var sent = CrashReporting.SendFeedback(text, contact, diagnostics) == CaptureFeedbackResult.Success;
        Notify(sent ? NotificationLevel.Info : NotificationLevel.Warning, Loc.Current[sent ? "feedback.thanks" : "feedback.failed"]);
        return sent;
    }).ShowDialog(this);

    public void ReportProblem()
    {
        if (!UrlOpener(IssuesUrl)) Notify(NotificationLevel.Warning, Loc.Current.Format("help.noBrowser", ("url", IssuesUrl)));
    }

    public void OpenLogsFolder()
    {
        var folder = Directory.CreateDirectory(Workspaces.UserData.Combine("crashes")).FullName;
        if (!UrlOpener(folder)) Notify(NotificationLevel.Info, folder); // no file manager here: the path is the answer
    }

    public void CopyDiagnostics()
    {
        if (Clipboard is not { } clipboard) return;
        _ = clipboard.SetTextAsync(CrashReporting.DiagnosticsText(Workspaces.Settings.Current.InstallId, Workspaces.UserData.Combine("crashes")));
        Notify(NotificationLevel.Info, Loc.Current["diagnostics.copied"]);
    }

    /// <summary>Opens a web address or a folder with the system's default handler; tests replace it. False when it is neither, or the machine has no handler.</summary>
    public static Func<string, bool> UrlOpener { get; set; } = target =>
    {
        // The shell would run any file it is given, so only a web address or an existing folder goes to it.
        if (!(Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") && !Directory.Exists(target)) return false;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or FileNotFoundException or PlatformNotSupportedException)
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

    public void FindUsages() => FindUsages(true);

    /// <summary>The Inspector button: always the resource itself, never the word under the caret of an open code tab.</summary>
    public void FindUsagesOfResource() => FindUsages(false);

    private void FindUsages(bool wordUnderCaret)
    {
        // The same target as the Inspector: the tab in front, else the Explorer selection.
        // In a code editor the name under the caret is searched: a resource, or a variable or function used in code.
        if (wordUnderCaret && ActiveCodeTab is { } code && Workspaces.Current is { } open && GmlNames.WordAt(code.Text, code.Editor.CaretOffset) is { } word)
        {
            _ = ShowNameUsagesAsync(open, word, code);
            return;
        }

        // Several selected entries: the places that use any of them, apart from the selection itself.
        if (Explorer.SelectedKeys.Count > 1 && Workspaces.Current is { } many && BulkResources.Split(Explorer.SelectedKeys) is { Count: > 1 } items)
        {
            ApplyCodeEditorsForSearch();
            Docking.ShowPanel("Usages");
            _ = _usages.ShowAsync(Loc.Current.Format("organize.selectionName", ("count", items.Count)), FindAllAsync(many, items, [.. items]));
            return;
        }

        var key = (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null) ?? Explorer.SelectedKey;
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        ApplyCodeEditorsForSearch();
        Docking.ShowPanel("Usages");
        _ = _usages.ShowAsync(name, UsageSearch.FindAsync(workspace, type, name), workspace.Nicknames.Both(type, name));
    }

    /// <summary>Lists the assets the resource in front uses; a double click on a row opens that asset.</summary>
    public void FindUses()
    {
        var key = (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null) ?? Explorer.SelectedKey;
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        ApplyCodeEditorsForSearch();
        Docking.ShowPanel("Usages");
        _ = _usages.ShowUsesAsync(name, UsesWithTypes(UsageSearch.FindUsesAsync(workspace, type, name)), workspace.Nicknames.Both(type, name));
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
            _ = _usages.ShowAsync(set + " #" + index, UsageSearch.FindTileAsync(workspace, set, index), workspace.Nicknames.Both("tileSets", set) + " #" + index);
        };
        return view;
    }

    public void FindChildren()
    {
        var key = (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null) ?? Explorer.SelectedKey;
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not ("objects", var name)) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowAsync(name, UsageSearch.FindChildrenAsync(workspace, name), workspace.Nicknames.Both("objects", name));
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
        if (Workspaces.Current is not { } workspace) return;
        ApplyCodeEditorsForSearch();
        Docking.ShowPanel("Usages");
        _ = _usages.ShowUnreferencedAsync(UsageSearch.FindUnreferencedAsync(workspace));
    }

    /// <summary>Opens the entry an Explorer key names, in its code editor or its property editor.</summary>
    private void FindUsagesOfKey(string key)
    {
        if (Workspaces.Current is not { } workspace || ResourceTypes.SplitKey(key) is not var (type, name)) return;
        ApplyCodeEditorsForSearch();
        Docking.ShowPanel("Usages");
        _ = _usages.ShowAsync(name, UsageSearch.FindAsync(workspace, type, name), workspace.Nicknames.Both(type, name));
    }

    // A link of a grid nested in another editor (room layers, extensions) has no listener of its own.
    private void OnLinkRequested(object? sender, G3MStudio.Editor.LinkRequestedEventArgs e)
    {
        switch (e.Action)
        {
            case G3MStudio.Editor.LinkAction.Open: OpenEntry(e.Key); break;
            case G3MStudio.Editor.LinkAction.FindUsages: FindUsagesOfKey(e.Key); break;
            default: EditImageKey(e.Key); break;
        }

        e.Handled = true;
    }

    private void OpenEntry(string key)
    {
        if (ResourceTypes.SplitKey(key) is not var (type, name)) return;
        if (type == "code") OpenCodeAt(name, 1, 1);
        else Docking.OpenTab("resource", key, false);
    }

    internal void OpenUsage(UsageHit hit)
    {
        var subject = UsageSearch.CodeNameOf(_usages.Subject);
        if (hit.OwnerType == "code")
        {
            var skip = UseIndexOf(hit);
            OpenCodeAt(hit.OwnerName, LineOf(hit.Where), 1, subject, skip, int.TryParse(hit.Where.Split(':', 2)[0], out var line) ? line : 0);
        }
        else if (hit.OwnerType is { } type)
        {
            var key = ResourceTypes.KeyOf(type, hit.OwnerName);
            Docking.OpenTab("resource", key, false);
            if (hit.Where.Length > 0) Dispatcher.UIThread.Post(() => this.GetVisualDescendants().OfType<PropertyGridTab>().FirstOrDefault(p => p.Resource == key && p.IsVisible)?.Reveal(hit.Where), DispatcherPriority.Loaded);
        }
    }

    // A hit without a line is the n-th use of the name in this entry: the earlier hits of the same entry say which.
    private int UseIndexOf(UsageHit hit) => _usages.Hits.TakeWhile(h => h != hit).Count(h => h.OwnerType == "code" && h.OwnerName == hit.OwnerName);

    private readonly Dictionary<string, (int Version, Task<string> Text)> _usageTexts = [];

    /// <summary>The line and its text for a row that has only an instruction offset, read from the code tab of the entry when one is open, else from the decompiled entry (once per entry, only for rows that show).</summary>
    internal async Task<string?> UsageDetailAsync(UsageHit hit)
    {
        if (Workspaces.Current is not { } workspace || hit.OwnerType != "code" || _usages.Subject.Length == 0) return null;
        var (name, skip) = (UsageSearch.CodeNameOf(_usages.Subject), UseIndexOf(hit));
        if (_codeTabs.Values.FirstOrDefault(t => t.Entry == hit.OwnerName) is { Text.Length: > 0 } tab) return UsageSearch.Describe(tab.Text, name, skip);
        var version = workspace.History.Version;
        if (_usageTexts.Count > 64) _usageTexts.Clear();
        if (!_usageTexts.TryGetValue(hit.OwnerName, out var cached) || cached.Version != version)
            _usageTexts[hit.OwnerName] = cached = (version, Task.Run(() =>
            {
                try
                {
                    using (workspace.Lock.Read()) return CodeText.Decompile(workspace.Data, hit.OwnerName, workspace.DecompileContext());
                }
                catch (Exception e) when (e is InvalidOperationException or NotSupportedException or ArgumentException) { return ""; }
            }));
        return UsageSearch.Describe(await cached.Text, name, skip);
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
            case Rooms.RoomView room: room.SelectAll(); break;
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
