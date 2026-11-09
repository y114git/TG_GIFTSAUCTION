using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia.Controls;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// The right-click menus of Explorer rows, editor tabs, code and the room canvas. They share one list of resource commands
/// (<see cref="AppCommands.ResourceMenu"/>), so the same entries carry the same names, keys and order everywhere.
/// </summary>
public sealed partial class MainWindow
{
    private string? CurrentKey => Explorer.SelectedKey ?? (Docking.ActiveTabId is { } id ? Docking.Tabs.Get(id)?.Key : null);

    private MenuItem RegistryItem(string id)
    {
        var command = Registry.Get(id);
        var title = Loc.Current[AppCommands.TitleKey(id)];
        var item = new MenuItem { Header = title, InputGesture = Shortcuts.Parse(command.Shortcut), Command = new RegistryCommand(Registry, command) };
        ToolTip.SetTip(item, KeyTips.For(Registry, id, title));
        return item;
    }

    /// <summary>The shared resource commands for a resource type; Duplicate and Copy to other data file only where the type supports them.</summary>
    public List<Control> ResourceMenuFor(string key)
    {
        var type = ResourceTypes.SplitKey(key)?.Type ?? "";
        return [.. AppCommands.ResourceMenu
            .Where(id => id switch
            {
                "edit.rename" => type != "general",
                "edit.duplicate" => ResourceDuplicate.Types.Contains(type),
                "edit.copyToOtherFile" => CopyPlanner.Supported.Contains(type),
                _ => true,
            })
            .Select(RegistryItem)];
    }

    /// <summary>The menu of an editor tab: the tab commands, a separator, then the resource commands of what the tab shows.</summary>
    public ContextMenu TabMenu(string tabId)
    {
        var menu = new ContextMenu();
        foreach (var id in AppCommands.TabMenu) menu.Items.Add(id == "-" ? new Separator() : RegistryItem(id));
        if (Docking.Tabs.Get(tabId) is { } tab && ResourceMenuFor(tab.Key) is { Count: > 0 } items)
        {
            menu.Items.Add(new Separator());
            foreach (var item in items) menu.Items.Add(item);
        }

        return menu;
    }

    // Tunnel: the dock library opens its own tab menu on the same request, so this handler answers first and marks it handled.
    private void OnDockContext(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<DocumentTabStripItem>() is not { DataContext: Document doc } strip) return;
        Docking.Factory.SetActiveDockable(doc);
        TabMenu(doc.Id).Open(strip);
        e.Handled = true;
    }

    /// <summary>Resource commands for the code editor and the room canvas; the key follows the tab in front when the menu opens.</summary>
    private List<Control> FrontResourceMenu() => Docking.ActiveTabId is { } id && Docking.Tabs.Get(id) is { } tab ? ResourceMenuFor(tab.Key) : [];

    public void RevealInExplorer()
    {
        if (Docking.ActiveTabId is { } id && Docking.Tabs.Get(id) is { } tab) Explorer.Select(tab.Key);
        else if (Explorer.SelectedKey is { } key) Explorer.Select(key);
        Docking.ShowPanel("Explorer");
    }

    /// <summary>Changes the Quick Access state of the open file and saves the view.</summary>
    private void EditQuick(Func<QuickAccess, QuickAccess> change)
    {
        Explorer.Model.Quick = change(Explorer.Model.Quick);
        SaveView();
    }

    public void ToggleFavourite()
    {
        if (CurrentKey is { } key) EditQuick(q => q.ToggleFavourite(key));
    }

    public void AddTag()
    {
        if (CurrentKey is not { } key) return;
        _ = new RenameWindow("", tag =>
        {
            if (string.IsNullOrWhiteSpace(tag)) return Loc.Current["explorer.tagInvalid"];
            EditQuick(q => q.AddTag(key, tag));
            return null;
        }, Loc.Current["explorer.tagTitle"], Loc.Current["explorer.tagLabel"], Loc.Current["explorer.tagOk"]).AskAsync(this);
    }
}
