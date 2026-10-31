using Avalonia.Controls;
using Avalonia.Data;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
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
                item.Bind(MenuItem.HeaderProperty, Tr(AppCommands.TitleKey(id)));
                // view.language.en goes into the submenu "menu.sub.view.language".
                var parts = id.Split('.');
                if (parts.Length < 3)
                {
                    menu.Items.Add(item);
                    continue;
                }

                var group = string.Join('.', parts[..2]);
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

        // Open Recent is a submenu, not a command; it follows Open in the File menu.
        _recentMenu.Bind(MenuItem.HeaderProperty, Tr("cmd.file.openRecent"));
        if (MainMenu.Items[0] is MenuItem file) file.Items.Insert(1, _recentMenu);
    }

    private readonly Dictionary<string, MenuItem> _menuItems = [];
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
