using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The menu bar: one menu per group of the command registry, and the Open Recent submenu.</summary>
public sealed partial class MainWindow
{
    private static Binding Tr(string key) => LocBinding.To(key);

    private void BuildMenu()
    {
        foreach (var (titleKey, ids) in AppCommands.Menus)
        {
            var menu = new MenuItem();
            if (AppCommands.MenuContexts.TryGetValue(titleKey, out var context)) _contextMenus[menu] = context;
            AutomationProperties.SetAutomationId(menu, titleKey);
            menu.Bind(MenuItem.HeaderProperty, Tr(titleKey));
            var submenus = new Dictionary<string, MenuItem>();
            foreach (var id in ids)
            {
                if (id == "-")
                {
                    menu.Items.Add(new Separator());
                    continue;
                }

                var command = Registry.Get(id);
                var item = new MenuItem { Command = new RegistryCommand(Registry, command), InputGesture = Shortcuts.Parse(command.Shortcut) };
                _menuItems[id] = item;
                AutomationProperties.SetAutomationId(item, id);
                item.Bind(MenuItem.HeaderProperty, Tr(AppCommands.TitleKey(id)));
                // view.panel.explorer goes into the submenu "menu.sub.view.panel"; AppCommands.Submenu says which entries stay flat.
                if (AppCommands.Submenu(titleKey, id) is not { } group)
                {
                    menu.Items.Add(item);
                    continue;
                }

                if (!submenus.TryGetValue(group, out var submenu))
                {
                    submenu = new MenuItem { Name = "Menu_" + group.Replace('.', '_') };
                    submenu.Bind(MenuItem.HeaderProperty, Tr("menu.sub." + group));
                    submenus[group] = submenu;
                    menu.Items.Add(submenu);
                }

                submenu.Items.Add(item);
            }

            MainMenu.Items.Add(menu);
        }

        Registry.ContextChanged += ShowContextMenus;
        Registry.ContextChanged += RefreshNewTips;
        Registry.ShortcutChanged += RefreshMenuTip;
        ShowContextMenus();
        RefreshMenuTips();

        // Open Recent is a submenu, not a command; it follows Open in the File menu.
        _recentMenu.Bind(MenuItem.HeaderProperty, Tr("cmd.file.openRecent"));
        if (MainMenu.Items[0] is MenuItem file) file.Items.Insert(1, _recentMenu);
    }

    private readonly Dictionary<string, MenuItem> _menuItems = [];
    private readonly Dictionary<MenuItem, string> _contextMenus = [];

    /// <summary>The Room, Image, Code and Sprite menus show only while a tab of their kind is in front.</summary>
    private void ShowContextMenus()
    {
        foreach (var (menu, context) in _contextMenus) menu.IsVisible = Registry.HasContext(context);
    }

    private void RefreshMenuTips()
    {
        foreach (var id in _menuItems.Keys) RefreshMenuTip(id);
    }

    private void RefreshMenuTip(string id)
    {
        if (!_menuItems.TryGetValue(id, out var item)) return;
        ToolTip.SetTip(item, KeyTips.For(Registry, id, Loc.Current[AppCommands.TitleKey(id)]));
        ToolTip.SetShowOnDisabled(item, true);
    }

    // A New entry that is off while a file is open is off because the data file has no section for it: the tip says so.
    private void RefreshNewTips()
    {
        foreach (var type in ResourceListCommand.Creatable)
        {
            var id = "file.new." + type;
            if (!_menuItems.TryGetValue(id, out var item)) continue;
            if (Registry.HasContext(AppCommands.WorkspaceOpen) && !Registry.HasContext(AppCommands.CanNew(type)))
                ToolTip.SetTip(item, Loc.Current.Format("explorer.noSection", ("type", Loc.Current["explorer.typeOne." + type])));
            else RefreshMenuTip(id);
        }
    }
    private readonly MenuItem _recentMenu = new() { Name = "OpenRecent" };

    private void RefreshRecentMenu()
    {
        _recentMenu.Items.Clear();
        foreach (var entry in Workspaces.Recent.Entries.Take(10))
        {
            var item = new MenuItem { Header = entry.Name, Tag = entry.Path };
            ToolTip.SetTip(item, entry.Path);
            item.Click += (_, _) => _ = OpenAsync(entry.Path);
            _recentMenu.Items.Add(item);
        }

        if (_recentMenu.Items.Count == 0)
        {
            var none = new MenuItem { IsEnabled = false };
            none.Bind(MenuItem.HeaderProperty, Tr("start.empty"));
            _recentMenu.Items.Add(none);
            return;
        }

        _recentMenu.Items.Add(new Separator());
        var clear = new MenuItem { Name = "ClearRecent" };
        clear.Bind(MenuItem.HeaderProperty, Tr("start.clear"));
        clear.Click += (_, _) => Workspaces.Recent.Clear();
        _recentMenu.Items.Add(clear);
    }
}
