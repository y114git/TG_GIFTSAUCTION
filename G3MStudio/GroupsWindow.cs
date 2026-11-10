using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// Lists the texture groups (or the audio groups) of the data file with their members and moves the picked members to another group.
/// A texture group holds sprites, fonts and tile sets, picked by kind. Every move is one undoable edit.
/// </summary>
public sealed class GroupsWindow : Window
{
    private static readonly string[] Kinds = ["sprites", "fonts", "tilesets"];

    private readonly Workspace _workspace;
    private readonly bool _audio;
    private readonly ListBox _groups = new() { Name = "GroupList", MinWidth = 180 };
    private readonly ListBox _members = new() { Name = "GroupMembers", SelectionMode = SelectionMode.Multiple, MinWidth = 260 };
    private readonly ComboBox _kind = new() { Name = "GroupKind", MinWidth = 140 };
    private readonly ComboBox _target = new() { Name = "GroupTarget", MinWidth = 180 };
    private readonly Button _move = new() { Name = "GroupMove" };
    private readonly TextBlock _message = new() { Name = "GroupMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox _name = new() { Name = "GroupName", MinWidth = 160 };
    private readonly Button _new = new() { Name = "GroupNew" };
    private readonly Button _rename = new() { Name = "GroupRename" };
    private readonly TextBlock _noGroups = new() { Name = "GroupsEmpty", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(8), IsHitTestVisible = false };
    private readonly TextBlock _noMembers = new() { Name = "MembersEmpty", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(8), IsHitTestVisible = false };
    private readonly Func<RiskChoice> _risk;
    private bool _building;

    // risk: what the owner chose for risky actions; without it a name that is not a plain name is refused.
    public GroupsWindow(Workspace workspace, bool audio, Func<RiskChoice>? risk = null)
    {
        (_workspace, _audio, _risk) = (workspace, audio, risk ?? (() => RiskChoice.Refuse));
        Title = Loc.Current[audio ? "editors.groups.audioTitle" : "editors.groups.textureTitle"];
        Width = 640;
        Height = 460;
        MinWidth = 500;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _move.Content = Loc.Current["editors.groups.move"];
        _new.Content = Loc.Current["editors.groups.newGroup"];
        _rename.Content = Loc.Current["editors.groups.renameGroup"];
        _name.PlaceholderText = Loc.Current["editors.groups.newName"];
        _name.Labeled("editors.groups.newName");
        _groups.Labeled("editors.groups.groups");
        _members.Labeled("editors.groups.members");
        _kind.Labeled("editors.groups.kind");
        _target.Labeled("editors.groups.moveTo");
        _target.PlaceholderText = Loc.Current["editors.groups.targetPlaceholder"];
        _noGroups.Text = Loc.Current["editors.groups.noGroups"];
        _kind.ItemsSource = Kinds.Select(k => Loc.Current["explorer.type." + (k == "tilesets" ? "tileSets" : k)]).ToList();
        _kind.SelectedIndex = 0;
        _kind.IsVisible = !audio;
        // The label stays with its box when the row wraps.
        var moveTo = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Loc.Current["editors.groups.moveTo"], VerticalAlignment = VerticalAlignment.Center }, _target } };
        var bar = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { _kind, moveTo, _move } };
        var nameBox = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Loc.Current["editors.groups.nameLabel"], VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 220 }, _name } };
        var edit = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(0, 0, 0, 8), Children = { nameBox, _new, _rename } };
        foreach (var button in new[] { _new, _rename, _move }) ToolTip.SetShowOnDisabled(button, true);
        DockPanel.SetDock(edit, Avalonia.Controls.Dock.Top);
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        bar.Margin = new Thickness(0, 0, 0, 8);
        // Each list has a header and a hint that shows while it is empty.
        var groupsColumn = Column("editors.groups.groups", _groups, _noGroups);
        groupsColumn.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(groupsColumn, Avalonia.Controls.Dock.Left);
        foreach (var child in new Control[] { edit, bar, groupsColumn, Column("editors.groups.members", _members, _noMembers) }) panel.Children.Add(child);
        DialogFrame.Show(this, panel, [DialogFrame.Close(this)], _message);
        _groups.SelectionChanged += (_, _) => ShowMembers();
        _kind.SelectionChanged += (_, _) => ShowMembers();
        _move.Click += (_, _) => Move();
        _new.Click += async (_, _) => await CreateGroupAsync();
        _name.KeyDown += async (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            e.Handled = true;
            await CreateGroupAsync();
        };
        _name.TextChanged += (_, _) => UpdateButtons();
        _members.SelectionChanged += (_, _) => UpdateButtons();
        _target.SelectionChanged += (_, _) => UpdateButtons();
        _rename.Click += async (_, _) =>
        {
            if (_groups.SelectedItem is string group) await ChangeAsync(force => new GroupListCommand(_audio, GroupListCommand.Rename, group, _name.Text?.Trim() ?? "", force), "editors.groups.renamed");
        };
        workspace.History.Changed += Refresh;
        Closed += (_, _) => workspace.History.Changed -= Refresh;
        Refresh();
        UpdateButtons();
    }

    private static DockPanel Column(string headerKey, ListBox list, TextBlock empty)
    {
        var header = new TextBlock { Text = Loc.Current[headerKey], Classes = { "muted" }, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        return new DockPanel { Children = { header, new Grid { Children = { list, empty } } } };
    }

    private async Task CreateGroupAsync()
    {
        if (_new.IsEnabled) await ChangeAsync(force => new GroupListCommand(_audio, GroupListCommand.New, _name.Text?.Trim() ?? "", force: force), "editors.groups.created");
    }

    // A button that cannot work yet is disabled and its tip says what is missing.
    private void UpdateButtons()
    {
        var named = !string.IsNullOrWhiteSpace(_name.Text);
        _new.IsEnabled = named;
        ToolTip.SetTip(_new, named ? null : Loc.Current["editors.groups.hintName"]);
        var picked = _groups.SelectedItem is string;
        _rename.IsEnabled = named && picked;
        ToolTip.SetTip(_rename, !named ? Loc.Current["editors.groups.hintName"] : picked ? null : Loc.Current["editors.groups.hintRename"]);
        var ready = _target.SelectedItem is string && _members.SelectedItems is { Count: > 0 };
        _move.IsEnabled = ready;
        ToolTip.SetTip(_move, ready ? null : Loc.Current["editors.groups.hintMove"]);
    }

    private IReadOnlyList<string> GroupNames => _audio
        ? [.. (_workspace.Data.AudioGroups ?? []).Select(g => g.Name.Content ?? "")]
        : [.. (_workspace.Data.TextureGroupInfo ?? []).Select(g => g.Name.Content ?? "")];

    private string Kind => Kinds[Math.Max(0, _kind.SelectedIndex)];

    private void Refresh()
    {
        _building = true;
        var picked = _groups.SelectedItem as string;
        var names = GroupNames;
        _groups.ItemsSource = names.ToList();
        _target.ItemsSource = names.ToList();
        _noGroups.IsVisible = names.Count == 0;
        _groups.SelectedItem = picked is not null && names.Contains(picked) ? picked : (names.Count > 0 ? names[0] : null);
        _building = false;
        ShowMembers();
        UpdateButtons();
    }

    private List<string> Members(string group)
    {
        if (_audio) return [.. _workspace.Data.Sounds.Where(s => s.AudioGroup?.Name?.Content == group).Select(s => s.Name.Content ?? "")];
        var info = _workspace.Data.TextureGroupInfo?.FirstOrDefault(g => g.Name.Content == group);
        return info is null ? [] : [.. (TextureGroupMoveCommand.Members(info, Kind) ?? new List<object>()).Cast<object?>().Select(TextureGroupMoveCommand.NameOf).OfType<string>()];
    }

    private void ShowMembers()
    {
        if (_building) return;
        var members = _groups.SelectedItem is string group ? Members(group) : [];
        _members.ItemsSource = members;
        _noMembers.Text = Loc.Current[_groups.SelectedItem is string ? "editors.groups.noMembers" : "editors.groups.pickGroup"];
        _noMembers.IsVisible = members.Count == 0;
    }

    // The name box holds the new group's name, or the new name of the picked group. A name that is not a plain name is confirmed in a dialog; the setting that allows risky actions skips it.
    internal async Task ChangeAsync(Func<bool, GroupListCommand> build, string doneKey)
    {
        var text = _name.Text?.Trim() ?? "";
        var choice = _risk();
        var force = choice == RiskChoice.Allow;
        while (true)
        {
            UnusualNameException unusual;
            try
            {
                var queued = !_workspace.Edit(build(force));
                _message.Text = queued ? Loc.Current["app.editQueued"] : Loc.Current.Format(doneKey, ("name", text));
                return;
            }
            catch (UnusualNameException e)
            {
                unusual = e;
            }
            catch (InvalidOperationException e)
            {
                _message.Text = e.Message;
                return;
            }

            if (force || choice == RiskChoice.Refuse)
            {
                _message.Text = unusual.Message;
                return;
            }

            if (!await ProjectDialogs.AskConfirmAsync(this, Loc.Current["editors.groups.unusualTitle"], Loc.Current.Format("editors.groups.unusualConfirm", ("name", text)))) return;
            force = true;
        }
    }

    private void Move()
    {
        var names = _members.SelectedItems?.Cast<string>().ToList() ?? [];
        if (_target.SelectedItem is not string target || names.Count == 0) return;
        try
        {
            _workspace.Edit(_audio ? new AudioGroupMoveCommand(names, target) : new TextureGroupMoveCommand(Kind, names, target));
            _message.Text = Loc.Current.Format("editors.groups.moved", ("count", names.Count), ("group", target));
        }
        catch (InvalidOperationException e)
        {
            _message.Text = e.Message;
        }
    }
}
