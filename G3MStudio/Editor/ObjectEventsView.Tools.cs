using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class ObjectEventsView
{
    /// <summary>The "..." button at the end of an event row: one menu with move up and down, duplicate, copy to another object, change type and remove.</summary>
    private Button EventTools(uint kind, uint subtype)
    {
        var name = _object.Name.Content ?? "";
        var button = new Button { Name = $"EventMenu_{kind}_{subtype}", Content = "\u22EF", Classes = { "icon" }, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(button, Loc.Current["editors.object.eventMenu"]);
        Avalonia.Automation.AutomationProperties.SetName(button, Loc.Current["editors.object.eventMenu"]);
        button.Click += (_, _) =>
        {
            var menu = new MenuFlyout();
            void Add(string key, Action action)
            {
                var item = new MenuItem { Header = Loc.Current[key] };
                item.Click += (_, _) => action();
                menu.Items.Add(item);
            }

            Add("editors.object.eventUp", () => RunAny(new ObjectEventOrderCommand(name, kind, subtype, -1)));
            Add("editors.object.eventDown", () => RunAny(new ObjectEventOrderCommand(name, kind, subtype, 1)));
            Add("editors.object.eventDuplicate", () => Duplicate(name, kind, subtype));
            Add("editors.object.eventCopyTo", () => ShowCopyMenu(button, name, kind, subtype));
            Add("editors.object.eventRetype", () => ShowRetypeMenu(button, kind, subtype));
            menu.Items.Add(new Separator());
            Add("object.removeEvent", () => RemoveEvent(kind, subtype));
            menu.ShowAt(button);
        };
        return button;
    }

    internal bool RemoveEvent(uint kind, uint subtype) => Run(new ObjectEventCommand(_object.Name.Content ?? "", ObjectEventCommand.Remove, kind, subtype));

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
    private void ShowCopyMenu(Control anchor, string name, uint kind, uint subtype)
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
        else menu.ShowAt(anchor);
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
}
