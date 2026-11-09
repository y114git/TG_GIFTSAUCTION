using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class ObjectEventsView
{
    /// <summary>
    /// The Add Event menu as GameMaker has it: one entry per event type, and for a type with subtypes (alarms, keys, mouse, collision, other, draw...)
    /// a cascade of them. A pick calls <paramref name="pick"/> with the type and subtype.
    /// </summary>
    public static MenuFlyout BuildEventMenu(GameMakerData data, Action<uint, uint> pick)
    {
        var menu = new MenuFlyout();
        foreach (var type in EventNames.Types)
        {
            var item = new MenuItem { Header = type.ToString(), Name = "EventMenu_" + type };
            if (!EventNames.HasSubtype(type)) item.Click += (_, _) => pick((uint)type, 0);
            else AddSubtypes(item, type, data, pick);
            menu.Items.Add(item);
        }

        return menu;
    }

    private static void AddSubtypes(MenuItem item, EventType type, GameMakerData data, Action<uint, uint> pick)
    {
        foreach (var (label, value) in EventNames.Subtypes(type, data))
        {
            var sub = new MenuItem { Header = label };
            var (kind, subtype) = ((uint)type, value);
            sub.Click += (_, _) => pick(kind, subtype);
            item.Items.Add(sub);
        }
    }

    private Button CascadeButton(Workspace workspace)
    {
        var button = new Button { Name = "AddEventMenu", Content = Loc.Current["editors.object.addEventMenu"] };
        button.Click += (_, _) => BuildEventMenu(workspace.Data, AddEvent).ShowAt(button);
        return button;
    }

    // Change the type of one event: the same cascade, the pick moves the event and its code to the new type and subtype.
    private void ShowRetypeMenu(Control anchor, uint kind, uint subtype)
    {
        if (_workspace is null) return;
        var name = _object.Name.Content ?? "";
        BuildEventMenu(_workspace.Data, (toType, toSubtype) => RunAny(new ObjectEventRetypeCommand(name, kind, subtype, toType, toSubtype))).ShowAt(anchor);
    }

    /// <summary>Greyed rows for the events of the parent chain, each with a button that adds the event to this object so it can override it.</summary>
    private void AddInherited(Workspace workspace)
    {
        foreach (var inherited in InheritedEvents.Of(_object))
        {
            var label = EventNames.Label(inherited.Type, inherited.Subtype, workspace.Data);
            var text = new TextBlock { Text = Loc.Current.Format("editors.object.inherited", ("event", label), ("object", inherited.Owner)), Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
            var (kind, subtype) = (inherited.Type, inherited.Subtype);
            var overrideButton = Tool($"InheritedOverride_{kind}_{subtype}", "editors.object.override", () => { AddEvent(kind, subtype); return true; });
            DockPanel.SetDock(overrideButton, Avalonia.Controls.Dock.Right);
            _list.Children.Add(new DockPanel { Name = $"Inherited_{kind}_{subtype}", Children = { overrideButton, text } });
        }
    }
}
