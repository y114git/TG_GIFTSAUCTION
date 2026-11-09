using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>The room order of the game: the first room is where the game starts. Move, remove and add rooms; every change is one undoable step.</summary>
public sealed class RoomOrderWindow : Window
{
    private readonly Workspace _workspace;
    private readonly ListBox _list = new() { Name = "RoomOrderList", MinHeight = 120 };
    private readonly ComboBox _rooms = new() { Name = "RoomOrderRooms", MinWidth = 220 };
    private readonly Button _up = new() { Name = "RoomOrderUp" }, _down = new() { Name = "RoomOrderDown" }, _remove = new() { Name = "RoomOrderRemove" }, _add = new() { Name = "RoomOrderAdd" };
    private readonly TextBlock _message = new() { Name = "RoomOrderMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public RoomOrderWindow(Workspace workspace)
    {
        _workspace = workspace;
        Title = Loc.Current["roomOrder.title"];
        Width = 520;
        Height = 480;
        MinWidth = 380;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _up.Content = Loc.Current["roomOrder.up"];
        _down.Content = Loc.Current["roomOrder.down"];
        _remove.Content = Loc.Current["roomOrder.remove"];
        _add.Content = Loc.Current["roomOrder.add"];
        _up.Click += (_, _) => Move(-1);
        _down.Click += (_, _) => Move(1);
        _remove.Click += (_, _) => Run(new RoomOrderCommand(RoomOrderCommand.Remove, index: _list.SelectedIndex));
        _add.Click += (_, _) =>
        {
            if (_rooms.SelectedItem is string room) Run(new RoomOrderCommand(RoomOrderCommand.Add, room));
        };
        _list.SelectionChanged += (_, _) => RefreshButtons();
        workspace.History.Changed += Fill;
        Closed += (_, _) => workspace.History.Changed -= Fill;
        var buttons = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { _up, _down, _remove } };
        var adder = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Children = { _rooms, _add } };
        var content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                Docked(new TextBlock { Text = Loc.Current["roomOrder.hint"], Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 0, 0, 8) }, Avalonia.Controls.Dock.Top),
                Docked(adder, Avalonia.Controls.Dock.Bottom),
                Docked(buttons, Avalonia.Controls.Dock.Bottom),
                _list,
            },
        };
        DialogFrame.Show(this, content, [DialogFrame.Close(this)], _message);
        Fill();
    }

    private static T Docked<T>(T control, Avalonia.Controls.Dock dock) where T : Control
    {
        DockPanel.SetDock(control, dock);
        control.Margin = dock == Avalonia.Controls.Dock.Top ? control.Margin : new Avalonia.Thickness(0, 8, 0, 0);
        return control;
    }

    private void Fill()
    {
        var selected = _list.SelectedIndex;
        var order = _workspace.Data.GeneralInfo.RoomOrder.Select(r => r.Resource?.Name?.Content ?? "").ToList();
        _list.ItemsSource = order.Select((name, i) => $"{i + 1}  {name}").ToList();
        _list.SelectedIndex = Math.Min(selected, order.Count - 1);
        _rooms.ItemsSource = _workspace.Data.Rooms.Select(r => r.Name.Content ?? "").Where(n => n.Length > 0 && !order.Contains(n)).ToList();
        _rooms.SelectedIndex = _rooms.ItemCount > 0 ? 0 : -1;
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        var at = _list.SelectedIndex;
        _up.IsEnabled = at > 0;
        _down.IsEnabled = at >= 0 && at < _list.ItemCount - 1;
        _remove.IsEnabled = at >= 0;
        _add.IsEnabled = _rooms.SelectedItem is not null;
    }

    private void Move(int delta)
    {
        var at = _list.SelectedIndex;
        if (at < 0) return;
        if (Run(new RoomOrderCommand(RoomOrderCommand.Move, index: at, to: at + delta))) _list.SelectedIndex = at + delta;
    }

    private bool Run(RoomOrderCommand command)
    {
        try
        {
            _workspace.Edit(command);
            _message.Text = "";
            return true;
        }
        catch (InvalidOperationException e)
        {
            _message.Text = e.Message;
            return false;
        }
    }
}
