using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Mods;

/// <summary>
/// Compares two data files side by side: a tree of the added, removed and changed resources per type, and for the selected one its properties, code
/// (side by side or unified), pictures (overlay, swipe, difference) or room instances. The left file is the open one; "Apply this change" makes it like the right file.
/// </summary>
public sealed partial class DiffWindow : Window
{
    private readonly MainWindow _owner;
    private readonly Workspace _left, _right;
    private readonly DiffOptions _options;
    private DiffResult _diff;
    private DiffItem? _selected;
    private readonly TreeView _tree = new() { Name = "DiffTree" };
    private readonly TextBlock _summary = new() { Name = "DiffSummary", VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { Name = "DiffStatus", Classes = { "muted" } };
    private readonly TextBox _filter = new TextBox { Name = "DiffFilter", Width = 200, PlaceholderText = Loc.Current["mods.diff.filter"] }.Labeled("mods.diff.filter");
    private readonly ComboBox _types = new() { Name = "DiffTypes", MinWidth = 150 };
    private readonly List<string> _typeKeys = [];
    private readonly Dictionary<ChangeKind, ToggleButton> _kinds = [];
    private readonly Button _apply = new() { Name = "DiffApply", Content = Loc.Current["mods.diff.apply"] };

    public DiffWindow(MainWindow owner, Workspace left, Workspace right, DiffResult diff, DiffOptions options)
    {
        (_owner, _left, _right, _diff, _options) = (owner, left, right, diff, options);
        Title = Loc.Current.Format("mods.diff.title", ("left", left.Name), ("right", right.Name));
        Width = 1100;
        Height = 720;
        MinWidth = 760;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        BuildTypes();
        foreach (var kind in Enum.GetValues<ChangeKind>())
        {
            var button = new ToggleButton { Name = "DiffKind" + kind, Content = ModsUi.KindName(kind), IsChecked = true, Foreground = ModsUi.Ink(kind) };
            button.IsCheckedChanged += (_, _) => RenderTree();
            _kinds[kind] = button;
        }

        _filter.TextChanged += (_, _) => RenderTree();
        _types.SelectionChanged += (_, _) => RenderTree();
        _tree.SelectionChanged += async (_, _) => await SelectAsync();
        Layout();
        RenderTree();
    }

    private void BuildTypes()
    {
        _types.Items.Add(Loc.Current["mods.diff.allTypes"]);
        _typeKeys.Add("");
        foreach (var group in DiffResult.ByType(_diff.Items))
        {
            _types.Items.Add(Loc.Current["explorer.type." + group.Key]);
            _typeKeys.Add(group.Key);
        }

        _types.SelectedIndex = 0;
    }

    private void Layout()
    {
        var bar = new WrapPanel { ItemSpacing = ModsUi.Space(2), LineSpacing = ModsUi.Space(2), Margin = new Avalonia.Thickness(0, 0, 0, ModsUi.Space(2)) };
        _summary.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        bar.Children.AddRange([_summary, _filter, _types, .. _kinds.Values]);
        var body = new Grid { ColumnDefinitions = { new ColumnDefinition(340, GridUnitType.Pixel), new ColumnDefinition(ModsUi.Space(2), GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star) } };
        var splitter = new GridSplitter { Width = ModsUi.Space(2) };
        Grid.SetColumn(splitter, 1);
        var details = Details();
        Grid.SetColumn(details, 2);
        body.Children.AddRange([_tree, splitter, details]);
        _apply.Click += async (_, _) => await ApplyAsync();
        _apply.Classes.Add("primary");
        _status.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        var panel = new DockPanel();
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        panel.Children.AddRange([bar, body]);
        DialogFrame.Show(this, panel, [Action("DiffCopy", "mods.diff.copy", CopyAsync), Action("DiffReport", "mods.diff.saveReport", () => _owner.SaveDiffReportAsync(_left, _right)), Action("DiffExport", "mods.diff.exportPack", async () => await _owner.ExportPackAsync(_left, _right, _diff.Where(CurrentFilter()))),
            Action("DiffCreateMod", "mods.diff.createMod", async () => await _owner.CreateG3MModAsync(_left, _right)), Action("DiffRefresh", "mods.diff.refresh", RefreshAsync), _apply, DialogFrame.Close(this, "DiffClose", isDefault: false)], _status);
    }

    private static Button Action(string name, string key, Func<Task> run)
    {
        var button = new Button { Name = name, Content = Loc.Current[key] };
        button.Click += async (_, _) => await run();
        return button;
    }

    private DiffFilter CurrentFilter()
    {
        var kinds = _kinds.Where(k => k.Value.IsChecked == true).Select(k => k.Key).ToHashSet();
        var type = _types.SelectedIndex > 0 ? _typeKeys[_types.SelectedIndex] : "";
        return new DiffFilter(type.Length == 0 ? [] : [type], kinds.Count == _kinds.Count ? [] : [.. kinds], _filter.Text ?? "");
    }

    private void RenderTree()
    {
        var shown = _diff.Where(CurrentFilter());
        _tree.Items.Clear();
        foreach (var group in DiffResult.ByType(shown))
        {
            var node = new TreeViewItem { Header = $"{Loc.Current["explorer.type." + group.Key]} ({group.Count()})", IsExpanded = true };
            foreach (var item in group) node.Items.Add(new TreeViewItem { Header = new TextBlock { Text = Marker(item.Kind) + item.Name, Foreground = ModsUi.Ink(item.Kind) }, Tag = item });
            _tree.Items.Add(node);
        }

        _summary.Text = _diff.Items.Count == 0 ? Loc.Current["mods.diff.noChanges"]
            : Loc.Current.Format("mods.diff.summary", ("added", _diff.Count(ChangeKind.Added)), ("removed", _diff.Count(ChangeKind.Removed)), ("changed", _diff.Count(ChangeKind.Changed)), ("shown", shown.Count));
    }

    private static string Marker(ChangeKind kind) => kind switch { ChangeKind.Added => "+ ", ChangeKind.Removed => "- ", _ => "~ " };

    private async Task RefreshAsync()
    {
        if (await _owner.ComputeDiffAsync(_left, _right) is not { } diff) return;
        _diff = diff;
        _selected = null;
        RenderTree();
    }

    private bool CanEdit => ReferenceEquals(_owner.Workspaces.Current, _left);

    private async Task ApplyAsync()
    {
        if (_selected is not { } item)
        {
            _status.Text = Loc.Current["mods.diff.selectItem"];
            return;
        }

        if (!CanEdit)
        {
            _status.Text = Loc.Current["mods.diff.readOnly"];
            return;
        }

        if (await _owner.ApplyDiffItemAsync(_left, _right, item, SelectedProperty())) await RefreshAsync();
    }

    private async Task CopyAsync()
    {
        if (_selected is not { } item)
        {
            _status.Text = Loc.Current["mods.diff.selectItem"];
            return;
        }

        await _owner.CopyDiffItemAsync(_left, _right, item);
        await RefreshAsync();
    }
}
