using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class ObjectEventsView
{
    /// <summary>The buttons at the end of an event row: move up and down, duplicate, copy to another object, remove.</summary>
    private StackPanel EventTools(uint kind, uint subtype)
    {
        var name = _object.Name.Content ?? "";
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        panel.Children.Add(Tool($"EventUp_{kind}_{subtype}", "editors.object.eventUp", () => RunAny(new ObjectEventOrderCommand(name, kind, subtype, -1))));
        panel.Children.Add(Tool($"EventDown_{kind}_{subtype}", "editors.object.eventDown", () => RunAny(new ObjectEventOrderCommand(name, kind, subtype, 1))));
        panel.Children.Add(Tool($"EventDuplicate_{kind}_{subtype}", "editors.object.eventDuplicate", () => Duplicate(name, kind, subtype)));
        panel.Children.Add(CopyButton(name, kind, subtype));
        panel.Children.Add(RemoveButton(kind, subtype));
        return panel;
    }

    private static Button Tool(string name, string key, Func<bool> action)
    {
        var button = new Button { Name = name, Content = Loc.Current[key], VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => action();
        return button;
    }

    private bool Duplicate(string name, uint kind, uint subtype)
    {
        if (_workspace is null || ObjectEventCopyCommand.FreeSubtype(_workspace.Data, _object, (EventType)kind, subtype) is not { } free)
        {
            _message.Text = Loc.Current["editors.object.noFreeEvent"];
            return false;
        }

        return RunAny(new ObjectEventCopyCommand(name, kind, subtype, name, free));
    }

    // A menu of the other objects: choosing one copies the event there under the same type and subtype.
    private Button CopyButton(string name, uint kind, uint subtype)
    {
        var button = new Button { Name = $"EventCopy_{kind}_{subtype}", Content = Loc.Current["editors.object.eventCopyTo"], VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) =>
        {
            if (_workspace is null) return;
            var menu = new MenuFlyout();
            foreach (var other in _workspace.Data.GameObjects.Where(o => o.Name.Content != name && !o.Events[(int)kind].Any(e => e.EventSubtype == subtype)).Take(200))
            {
                var target = other.Name.Content ?? "";
                var item = new MenuItem { Header = target };
                item.Click += (_, _) => RunAny(new ObjectEventCopyCommand(name, kind, subtype, target, subtype));
                menu.Items.Add(item);
            }

            if (menu.Items.Count == 0) _message.Text = Loc.Current["editors.object.noCopyTarget"];
            else menu.ShowAt(button);
        };
        return button;
    }

    private bool RunAny(EditCommand command)
    {
        try
        {
            _workspace?.Edit(command);
            _message.Text = "";
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or CodeCompileException)
        {
            _message.Text = e.Message;
            return false;
        }
    }

    private StackPanel ParentRow(Workspace workspace)
    {
        var none = Loc.Current["editors.object.noParent"];
        var names = workspace.Data.GameObjects.Select(o => o.Name.Content ?? "").Where(n => n != _object.Name.Content).Take(2000).Prepend(none).ToList();
        var picker = new ComboBox { Name = "ObjectParent", ItemsSource = names, MinWidth = 200 };
        void Show() => picker.SelectedItem = _object.ParentId?.Name?.Content ?? none;
        Show();
        picker.SelectionChanged += (_, _) =>
        {
            var chosen = picker.SelectedItem as string ?? none;
            var parent = chosen == none ? "" : chosen;
            if ((_object.ParentId?.Name?.Content ?? "") == parent) return;
            if (!RunAny(new ObjectParentCommand(_object.Name.Content ?? "", parent))) Show();
        };
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Show;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Show;
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["editors.object.parent"], VerticalAlignment = VerticalAlignment.Center }, picker } };
    }
}
