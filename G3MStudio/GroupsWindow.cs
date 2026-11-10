using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
    private bool _building;

    public GroupsWindow(Workspace workspace, bool audio)
    {
        (_workspace, _audio) = (workspace, audio);
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
        _kind.ItemsSource = Kinds.Select(k => Loc.Current["explorer.type." + (k == "tilesets" ? "backgrounds" : k)]).ToList();
        _kind.SelectedIndex = 0;
        _kind.IsVisible = !audio;
        var bar = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { _kind, new TextBlock { Text = Loc.Current["editors.groups.moveTo"], VerticalAlignment = VerticalAlignment.Center }, _target, _move } };
        var edit = new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(0, 0, 0, 8), Children = { _name, _new, _rename } };
        DockPanel.SetDock(edit, Avalonia.Controls.Dock.Top);
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_groups, Avalonia.Controls.Dock.Left);
        bar.Margin = new Thickness(0, 0, 0, 8);
        _groups.Margin = new Thickness(0, 0, 8, 0);
        foreach (var child in new Control[] { edit, bar, _groups, _members }) panel.Children.Add(child);
        DialogFrame.Show(this, panel, [DialogFrame.Close(this)], _message);
        _groups.SelectionChanged += (_, _) => ShowMembers();
        _kind.SelectionChanged += (_, _) => ShowMembers();
        _move.Click += (_, _) => Move();
        _new.Click += (_, _) => Change(new GroupListCommand(_audio, GroupListCommand.New, _name.Text?.Trim() ?? ""), "editors.groups.created");
        _rename.Click += (_, _) =>
        {
            if (_groups.SelectedItem is string group) Change(new GroupListCommand(_audio, GroupListCommand.Rename, group, _name.Text?.Trim() ?? ""), "editors.groups.renamed");
        };
        workspace.History.Changed += Refresh;
        Closed += (_, _) => workspace.History.Changed -= Refresh;
        Refresh();
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
        _groups.SelectedItem = picked is not null && names.Contains(picked) ? picked : (names.Count > 0 ? names[0] : null);
        _building = false;
        ShowMembers();
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
        _members.ItemsSource = _groups.SelectedItem is string group ? Members(group) : [];
    }

    // The name box holds the new group's name, or the new name of the picked group.
    internal void Change(GroupListCommand command, string doneKey)
    {
        try
        {
            _workspace.Edit(command);
            _message.Text = Loc.Current.Format(doneKey, ("name", _name.Text?.Trim() ?? ""));
        }
        catch (InvalidOperationException e)
        {
            _message.Text = e.Message;
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
