using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Plugins;

namespace G3MStudio;

/// <summary>The plugin host, its window and the Plugins menu, which lists the commands plugins add.</summary>
public sealed partial class MainWindow
{
    private PluginHost? _plugins;
    private PluginWatcher? _pluginWatcher;
    private readonly MenuItem _pluginMenu = new() { Name = "PluginMenu", IsVisible = false };

    public PluginHost Plugins => _plugins ?? throw new InvalidOperationException("The window has not started yet.");

    private void InitPlugins()
    {
        _plugins = new PluginHost(Workspaces.UserData, Registry, () => Workspaces.Current, Notify);
        _pluginMenu.Bind(MenuItem.HeaderProperty, Tr("menu.plugins"));
        MainMenu.Items.Insert(Math.Max(0, MainMenu.Items.Count - 2), _pluginMenu); // before Window and Help
        Registry.CommandsChanged += RefreshPluginMenu;
        _plugins.PanelRequested += (title, create) =>
        {
            try
            {
                if (create() is Control content) new Window { Title = title, Content = content, Width = 520, Height = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(this);
            }
            catch (Exception e)
            {
                Notify(NotificationLevel.Error, $"{title}: {e.Message}");
            }
        };
        _plugins.Load();
        // Saving a plugin's sources reloads it, so a plugin can be developed while the app runs.
        _pluginWatcher = new PluginWatcher(_plugins.Folder, name => Dispatcher.UIThread.Post(() =>
        {
            if (_plugins.Plugins.FirstOrDefault(p => Path.GetFileName(p.Folder) == name && p.State != PluginState.Disabled) is { } plugin) _plugins.Reload(plugin);
        }));
    }

    private void RefreshPluginMenu()
    {
        _pluginMenu.Items.Clear();
        foreach (var command in Registry.All.Where(c => c.Title is not null && c.Id.StartsWith("plugin.", StringComparison.Ordinal)).OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase))
            _pluginMenu.Items.Add(new MenuItem { Header = command.Title, Command = new RegistryCommand(Registry, command) });
        _pluginMenu.IsVisible = _pluginMenu.Items.Count > 0;
    }

    public void ShowStrings()
    {
        if (Workspaces.Current is { } workspace) new StringsWindow(workspace).Show(this);
    }

    public void ShowPlugins() => new PluginsWindow(Plugins, Confirm).Show(this);
}
