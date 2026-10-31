using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Plugins;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Lists the plugins with their state, turns them on and off, reloads them and shows their errors and log.</summary>
public sealed class PluginsWindow : Window
{
    private readonly PluginHost _host;
    private readonly Func<string, string, Task<bool>> _confirm;
    private readonly ListBox _list = new() { Name = "PluginList", MinHeight = 160 };
    private readonly TextBox _details = new() { Name = "PluginDetails", IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 120 };
    private readonly TextBlock _empty = new() { Name = "PluginsEmpty", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _url = new() { Name = "PluginUrl", MinWidth = 240, PlaceholderText = "https://github.com/owner/repository" };
    private bool _building;

    public PluginsWindow(PluginHost host, Func<string, string, Task<bool>> confirm)
    {
        (_host, _confirm) = (host, confirm);
        Title = Loc.Current["plugins.title"];
        Width = 560;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _empty.Text = Loc.Current.Format("plugins.empty", ("folder", host.Folder));
        Button Tool(string name, string key, Action run)
        {
            var button = new Button { Name = name, Content = Loc.Current[key] };
            button.Click += (_, _) => run();
            return button;
        }

        var tools = new WrapPanel
        {
            Children =
            {
                Tool("PluginNew", "plugins.new", NewPlugin),
                Tool("PluginInstallZip", "plugins.installZip", () => _ = InstallAsync(false)),
                Tool("PluginInstallFolder", "plugins.installFolder", () => _ = InstallAsync(true)),
                _url,
                Tool("PluginInstallUrl", "plugins.installUrl", () => _ = InstallFromGitHubAsync()),
                Tool("PluginCheck", "plugins.check", () => _ = CheckUpdateAsync()),
                Tool("PluginUpdate", "plugins.update", () => { if (Selected?.Origin is { } origin) { _url.Text = origin; _ = InstallFromGitHubAsync(); } else _details.Text = Loc.Current["plugins.noOrigin"]; }),
                Tool("PluginReload", "plugins.reload", () => { if (Selected is { } p) host.Reload(p); }),
                Tool("PluginReloadAll", "plugins.reloadAll", host.Load),
                Tool("PluginOpenFolder", "plugins.openFolder", () => OpenFolder(Selected?.Folder ?? host.Folder)),
            },
        };
        var panel = new DockPanel { Margin = new Avalonia.Thickness(12) };
        DockPanel.SetDock(tools, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_empty, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_details, Avalonia.Controls.Dock.Bottom);
        panel.Children.Add(tools);
        panel.Children.Add(_empty);
        panel.Children.Add(_details);
        panel.Children.Add(_list);
        Content = panel;
        _list.SelectionChanged += (_, _) => ShowDetails();
        host.Changed += Refresh;
        Closed += (_, _) => host.Changed -= Refresh;
        Refresh();
    }

    private PluginInfo? Selected => (_list.SelectedItem as ListBoxItem)?.Tag as PluginInfo;

    private void NewPlugin()
    {
        var existing = _host.Plugins.Select(p => p.Manifest.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = "New Plugin";
        for (var i = 2; existing.Contains(name); i++) name = "New Plugin " + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _host.CreateTemplate(name);
        _host.Load();
    }

    private async Task InstallAsync(bool folder)
    {
        string? source;
        if (folder)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Loc.Current["plugins.installFolder"], AllowMultiple = false });
            source = folders is [{ } picked, ..] ? picked.TryGetLocalPath() : null;
        }
        else
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Loc.Current["plugins.installZip"], AllowMultiple = false });
            source = files is [{ } picked, ..] ? picked.TryGetLocalPath() : null;
        }

        if (source is not null) await TrustAndInstallAsync(source, source);
    }

    /// <summary>Asks GitHub for the latest commit of the selected plugin's repository and says whether the installed one is older.</summary>
    private async Task CheckUpdateAsync()
    {
        if (Selected?.Origin is not { } origin)
        {
            _details.Text = Loc.Current["plugins.noOrigin"];
            return;
        }

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("G3MStudio");
            var latest = await PluginDownload.LatestRevisionAsync(http, origin);
            _details.Text = PluginDownload.IsOutdated(Selected?.Revision, latest) ? Loc.Current["plugins.updateAvailable"] : Loc.Current["plugins.upToDate"];
        }
        catch (Exception e) when (e is HttpRequestException or InvalidDataException or System.Text.Json.JsonException or TaskCanceledException)
        {
            _details.Text = Loc.Current["plugins.error"] + ": " + e.Message;
        }
    }

    private async Task InstallFromGitHubAsync()
    {
        var address = _url.Text?.Trim() ?? "";
        if (PluginDownload.ParseGitHub(address) is null)
        {
            _details.Text = Loc.Current["plugins.error"] + ": " + Loc.Current["plugins.badUrl"];
            return;
        }

        var folder = Directory.CreateTempSubdirectory("g3mstudio-plugin-");
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("G3MStudio");
            var file = Path.Combine(folder.FullName, "plugin.zip");
            await PluginDownload.DownloadAsync(http, address, file);
            await TrustAndInstallAsync(file, address);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
        {
            _details.Text = Loc.Current["plugins.error"] + ": " + e.Message;
        }
        finally
        {
            folder.Delete(true);
        }
    }

    /// <summary>Shows the trust prompt for what is about to be installed and installs it when the user agrees. <paramref name="label"/> is the source the prompt names.</summary>
    private async Task TrustAndInstallAsync(string source, string label)
    {
        try
        {
            var manifest = PluginHost.Inspect(source);
            var text = Loc.Current.Format("plugins.trust", ("name", manifest.Name), ("version", manifest.Version), ("authors", string.Join(", ", manifest.Authors)), ("permissions", string.Join(", ", manifest.Permissions)), ("source", label));
            if (!await _confirm(Loc.Current["plugins.installTitle"], text)) return;
            _host.Install(source, label.StartsWith("https://", StringComparison.Ordinal) ? label : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            _details.Text = Loc.Current["plugins.error"] + ": " + e.Message;
        }
    }

    private static void OpenFolder(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // No file manager on this system: the path is in the list details.
        }
    }

    private void Refresh()
    {
        _building = true;
        var selected = Selected?.Folder;
        _list.ItemsSource = _host.Plugins.Select(plugin =>
        {
            var enabled = new CheckBox { Content = Loc.Current["plugins.enabled"], IsChecked = plugin.State == PluginState.Active, Tag = plugin };
            enabled.IsCheckedChanged += (_, _) =>
            {
                if (_building) return;
                if (enabled.IsChecked == true) _host.Enable(plugin);
                else _host.Disable(plugin);
            };
            var title = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = plugin.Manifest.Name + "  " + plugin.Manifest.Version },
                    new TextBlock { Text = Loc.Current["plugins.state." + plugin.State], Classes = { "muted" } },
                },
            };
            var row = new DockPanel { Children = { enabled, title } };
            DockPanel.SetDock(enabled, Avalonia.Controls.Dock.Right);
            return new ListBoxItem { Content = row, Tag = plugin };
        }).ToList();
        _list.SelectedItem = _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => (i.Tag as PluginInfo)?.Folder == selected);
        _empty.IsVisible = _host.Plugins.Count == 0;
        _building = false;
        ShowDetails();
    }

    private void ShowDetails()
    {
        if (Selected is not { } plugin)
        {
            _details.Text = "";
            return;
        }

        var lines = new List<string> { plugin.Manifest.Name + " " + plugin.Manifest.Version, plugin.Manifest.Description, plugin.Folder };
        if (plugin.Manifest.Homepage.Length > 0) lines.Add(plugin.Manifest.Homepage);
        if (plugin.Manifest.Permissions.Length > 0) lines.Add(Loc.Current["plugins.permissions"] + ": " + string.Join(", ", plugin.Manifest.Permissions));
        if (plugin.Error is not null) lines.Add(Loc.Current["plugins.error"] + ": " + plugin.Error);
        lines.AddRange(plugin.Log);
        _details.Text = string.Join(Environment.NewLine, lines.Where(l => l.Length > 0));
    }
}
