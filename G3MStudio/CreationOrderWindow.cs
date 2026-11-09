using System.Globalization;
using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>Lists the instances of a room in creation order and moves one at a time; every move is an undoable edit.</summary>
public sealed class CreationOrderWindow : Window
{
    private readonly GameMakerRoom _room;
    private readonly Workspace _workspace;
    private readonly ListBox _list = new() { Name = "CreationOrder", MinHeight = 120 };

    public CreationOrderWindow(GameMakerRoom room, Workspace workspace)
    {
        (_room, _workspace) = (room, workspace);
        Title = Loc.Current["rooms.orderTitle"] + " - " + room.Name.Content;
        Width = 420;
        Height = 460;
        MinWidth = 340;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Button Move(string key, string name, Func<int, int> target)
        {
            var button = new Button { Content = Loc.Current[key], Name = name };
            button.Click += (_, _) => MoveSelected(target);
            return button;
        }

        var buttons = new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8,
            Margin = new Avalonia.Thickness(0, 0, 0, 8),
            Children =
            {
                Move("rooms.orderTop", "OrderTop", _ => 0),
                Move("rooms.orderUp", "OrderUp", i => i - 1),
                Move("rooms.orderDown", "OrderDown", i => i + 1),
                Move("rooms.orderEnd", "OrderEnd", _ => int.MaxValue),
            },
        };
        var note = new TextBlock { Text = Loc.Current["rooms.orderNote"], TextWrapping = Avalonia.Media.TextWrapping.Wrap, Classes = { "muted" } };
        var panel = new DockPanel();
        DockPanel.SetDock(buttons, Avalonia.Controls.Dock.Top);
        panel.Children.Add(buttons);
        panel.Children.Add(_list);
        DialogFrame.Show(this, panel, [DialogFrame.Close(this)], note);
        workspace.History.Changed += Refresh;
        Closed += (_, _) => workspace.History.Changed -= Refresh;
        Refresh();
    }

    private G3MLib.DataFile.GameMakerPointerList<GameMakerRoom.GameObject> Instances => _room.GameObjects;

    private void Refresh()
    {
        var selected = (_list.SelectedItem as GameMakerRoom.GameObject)?.InstanceID;
        _list.ItemsSource = Instances.Select((o, i) => new Row(o, string.Create(CultureInfo.InvariantCulture, $"{i + 1}  inst_{o.InstanceID:X8} : {o.ObjectDefinition?.Name.Content ?? "?"}"))).ToList();
        _list.SelectedItem = _list.ItemsSource.Cast<Row>().FirstOrDefault(r => r.Instance.InstanceID == selected);
    }

    private void MoveSelected(Func<int, int> target)
    {
        if (_list.SelectedItem is not Row row) return;
        var at = Instances.IndexOf(row.Instance);
        var to = Math.Clamp(target(at), 0, Instances.Count - 1);
        if (to != at) _workspace.Edit(new RoomCreationOrderCommand(_room.Name.Content ?? "", row.Instance.InstanceID, to));
    }

    private sealed record Row(GameMakerRoom.GameObject Instance, string Text)
    {
        public override string ToString() => Text;
    }
}
