using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Help menu and the Edit commands that go to the text editor having the focus.</summary>
public sealed partial class MainWindow
{
    /// <summary>The documentation lives in the G3MWiki repository.</summary>
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
        var key = (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null) ?? Explorer.SelectedKey;
        if (Workspaces.Current is not { } workspace || key is null || ResourceTypes.SplitKey(key) is not var (type, name) || !ApplyCodeEditors()) return;
        Docking.ShowPanel("Usages");
        _ = _usages.ShowAsync(name, UsageSearch.FindAsync(workspace, type, name));
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
        if (hit.OwnerType == "code") OpenCodeAt(hit.OwnerName, 1, 1);
        else if (hit.OwnerType is { } type) Docking.OpenTab("resource", ResourceTypes.KeyOf(type, hit.OwnerName), false);
    }

    public void ShowSearch() => Docking.ShowPanel("Search");

    public void SelectAll()
    {
        switch (FocusManager?.GetFocusedElement())
        {
            case TextBox box: box.SelectAll(); break;
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
