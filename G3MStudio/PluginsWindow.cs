using Avalonia.Controls;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Plugins;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>Lists the plugins with their state, turns them on and off, reloads them and shows their errors and log.</summary>
public sealed class PluginsWindow : Window
{
    private readonly PluginHost _host;
    private readonly Func<string, string, Task<bool>> _confirm;
    private readonly ListBox _list = new() { Name = "PluginList", MinHeight = 160 };
    private readonly TextBox _details = new() { Name = "PluginDetails", IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 100, MaxHeight = 200 };
    private readonly TextBlock _empty = new() { Name = "PluginsEmpty", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _options = new() { Name = "PluginOptions", Spacing = 6 };
    private readonly TextBox _url = new TextBox { Name = "PluginUrl", MinWidth = 240, PlaceholderText = "https://github.com/owner/repository" }.Labeled("plugins.urlLabel");
    private readonly List<Button> _needsSelection = [];
    private bool _building;

    public PluginsWindow(PluginHost host, Func<string, string, Task<bool>> confirm)
    {
        (_host, _confirm) = (host, confirm);
        _optionsScroll = new ScrollViewer { Name = "PluginOptionsScroll", Content = _options, MaxHeight = 220, IsVisible = false, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        Title = Loc.Current["plugins.title"];
        Width = 560;
        Height = 680;
        MinWidth = 420;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _empty.Text = Loc.Current.Format("plugins.empty", ("folder", host.Folder));
        Button Tool(string name, string key, Action run, bool needsSelection = false)
        {
            var button = new Button { Name = name, Content = Loc.Current[key] };
            button.Click += (_, _) => run();
            if (needsSelection) _needsSelection.Add(button);
            return button;
        }

        var install = new WrapPanel
        {
            ItemSpacing = 6,
            LineSpacing = 6,
            Margin = new Avalonia.Thickness(0, 0, 0, 6),
            Children =
            {
                Tool("PluginNew", "plugins.new", NewPlugin),
                Tool("PluginInstallZip", "plugins.installZip", () => _ = InstallAsync(false)),
                Tool("PluginInstallFolder", "plugins.installFolder", () => _ = InstallAsync(true)),
                _url,
                Tool("PluginInstallUrl", "plugins.installUrl", () => _ = InstallFromGitHubAsync()),
            },
        };
        var tools = new WrapPanel
        {
            ItemSpacing = 6,
            LineSpacing = 6,
            Children =
            {
                Tool("PluginCheck", "plugins.check", () => _ = CheckUpdateAsync(), true),
                Tool("PluginUpdate", "plugins.update", () => { if (Selected?.Origin is { } origin) { _url.Text = origin; _ = InstallFromGitHubAsync(); } else _details.Text = Loc.Current["plugins.noOrigin"]; }, true),
                Tool("PluginReload", "plugins.reload", () => { if (Selected is { } p) host.Reload(p); }, true),
                Tool("PluginReloadAll", "plugins.reloadAll", host.Load),
                Tool("PluginOpenFolder", "plugins.openFolder", () => OpenFolder(Selected?.Folder ?? host.Folder)),
            },
        };
        var panel = new DockPanel();
        tools.Margin = new Avalonia.Thickness(0, 0, 0, 8);
        _details.Margin = new Avalonia.Thickness(0, 8, 0, 0);
        DockPanel.SetDock(install, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(tools, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_empty, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_details, Avalonia.Controls.Dock.Bottom);
        var optionsScroll = _optionsScroll;
        DockPanel.SetDock(optionsScroll, Avalonia.Controls.Dock.Bottom);
        panel.Children.Add(install);
        panel.Children.Add(tools);
        panel.Children.Add(_empty);
        panel.Children.Add(_details);
        panel.Children.Add(optionsScroll);
        panel.Children.Add(_list);
        DialogFrame.Show(this, panel, [DialogFrame.Close(this, isDefault: false)]);
        _list.SelectionChanged += (_, _) => ShowDetails();
        host.Changed += Refresh;
        Closed += (_, _) => host.Changed -= Refresh;
        Refresh();
    }

    private readonly ScrollViewer _optionsScroll;

    /// <summary>One control per option the selected plugin declared; a change is saved at once and the plugin's notes in open code editors follow.</summary>
    private void ShowOptions(PluginInfo? plugin)
    {
        _options.Children.Clear();
        var settings = plugin?.Settings;
        _optionsScroll.IsVisible = settings is { Definitions.Count: > 0 };
        if (settings is null) return;
        if (settings.Definitions.Count > 0) _options.Children.Add(new TextBlock { Text = Loc.Current["plugins.options"], Classes = { "muted" } });
        foreach (var option in settings.Definitions)
        {
            Control input;
            switch (option.Kind)
            {
                case PluginSettingKind.Bool:
                    var check = new CheckBox { Name = "PluginOption_" + option.Key, Content = option.Label, IsChecked = settings.GetBool(option.Key) };
                    check.IsCheckedChanged += (_, _) => settings.Set(option.Key, check.IsChecked == true ? "true" : "false");
                    input = check;
                    break;
                case PluginSettingKind.Number:
                    var number = new NumericUpDown { Name = "PluginOption_" + option.Key, Minimum = (decimal)option.Min, Maximum = (decimal)option.Max, Value = (decimal)settings.GetNumber(option.Key), MinWidth = 120 };
                    number.ValueChanged += (_, e) => { if (e.NewValue is { } v) settings.Set(option.Key, ((double)v).ToString(System.Globalization.CultureInfo.InvariantCulture)); };
                    input = Labeled(option.Label, number);
                    break;
                case PluginSettingKind.Choice:
                    var choices = option.Choices ?? [];
                    var combo = new ComboBox { Name = "PluginOption_" + option.Key, ItemsSource = choices, SelectedIndex = Math.Max(0, Array.IndexOf(choices, settings.Get(option.Key))), MinWidth = 160 };
                    combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string chosen) settings.Set(option.Key, chosen); };
                    input = Labeled(option.Label, combo);
                    break;
                default:
                    var text = new TextBox { Name = "PluginOption_" + option.Key, Text = settings.Get(option.Key), MinWidth = 220 };
                    text.LostFocus += (_, _) => settings.Set(option.Key, text.Text ?? "");
                    text.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) settings.Set(option.Key, text.Text ?? ""); };
                    input = Labeled(option.Label, text);
                    break;
            }

            _options.Children.Add(input);
            if (option.Description.Length > 0) _options.Children.Add(new TextBlock { Text = option.Description, Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        }
    }

    private static StackPanel Labeled(string label, Control input) => new() { Spacing = 2, Children = { new TextBlock { Text = label }, input } };

    private PluginInfo? Selected => (_list.SelectedItem as ListBoxItem)?.Tag as PluginInfo;

    private void NewPlugin()
    {
        var existing = _host.Plugins.Select(p => p.Manifest.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        const string baseName = "New Plugin"; // the template name becomes the plugin folder name, so it stays plain ASCII in every language
        var name = baseName;
        for (var i = 2; existing.Contains(name); i++) name = baseName + " " + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
        // No file manager on this system: the path is in the list details.
        _ = MainWindow.UrlOpener(folder);
    }

    private void Refresh()
    {
        _building = true;
        var selected = Selected?.Folder;
        _list.ItemsSource = _host.Plugins.Select(plugin =>
        {
            var enabled = new CheckBox { Content = Loc.Current["plugins.enabled"], IsChecked = plugin.State == PluginState.Active, Tag = plugin };
            Avalonia.Automation.AutomationProperties.SetName(enabled, Loc.Current.Format("plugins.enabledFor", ("name", plugin.Manifest.Name)));
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
        foreach (var button in _needsSelection) button.IsEnabled = Selected is not null;
        ShowOptions(Selected);
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
