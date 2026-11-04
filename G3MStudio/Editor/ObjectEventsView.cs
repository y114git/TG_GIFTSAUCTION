using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The events of an object above its properties: each event with the code entries it runs as links that open the code, a button to remove it,
/// and a row that adds an event of any type with an empty code entry. Every change is one undoable step.
/// </summary>
public sealed class ObjectEventsView : StackPanel
{
    private readonly GameMakerGameObject _object;
    private readonly Action<string> _open;
    private readonly Workspace? _workspace;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBlock _message = new() { Name = "EventMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public ObjectEventsView(GameMakerGameObject obj, Action<string> open, Workspace? workspace = null)
    {
        (_object, _open, _workspace) = (obj, open, workspace);
        Name = "ObjectEvents";
        Spacing = 6;
        Children.Add(_list); // plain controls: an items control in a docked header got no size in the tab
        if (workspace is not null)
        {
            Children.Add(AddRow(workspace));
            Children.Add(_message);
        }

        Fill();
        if (workspace is null) return;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Fill;
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += Fill;
            Fill();
        };
    }

    /// <summary>Event label and code entry name, in file order.</summary>
    public IReadOnlyList<(string Event, string Code)> Rows { get; private set; } = [];

    private string _shown = "";

    // What the list would show, as text: every edit and every tab switch calls Fill, and most of them change no event.
    private string Signature()
    {
        var text = new System.Text.StringBuilder();
        for (var type = 0; type < _object.Events.Count; type++)
            foreach (var evt in _object.Events[type])
            {
                text.Append(type).Append('/').Append(evt.EventSubtype).Append(' ').Append(EventNames.Label((uint)type, evt.EventSubtype, _workspace?.Data));
                foreach (var action in evt.Actions) text.Append(';');
            }

        return text.ToString();
    }

    private void Fill()
    {
        var signature = Signature();
        if (signature == _shown && _list.Children.Count > 0 == signature.Length > 0) return;
        _shown = signature;
        Rows = [.. Collect()];
        _list.Children.Clear();
        for (var type = 0; type < _object.Events.Count; type++)
        {
            foreach (var evt in _object.Events[type].ToList())
            {
                var (kind, subtype) = ((uint)type, evt.EventSubtype);
                var label = EventNames.Label(kind, subtype, _workspace?.Data);
                var row = new DockPanel();
                if (_workspace is not null)
                {
                    var remove = new Button { Content = Loc.Current["object.removeEvent"], Name = $"RemoveEvent_{kind}_{subtype}", VerticalAlignment = VerticalAlignment.Center };
                    remove.Click += (_, _) => Run(new ObjectEventCommand(_object.Name.Content ?? "", ObjectEventCommand.Remove, kind, subtype));
                    DockPanel.SetDock(remove, Avalonia.Controls.Dock.Right);
                    row.Children.Add(remove);
                }

                var codes = evt.Actions.Select(a => a.CodeId?.Name?.Content).OfType<string>().ToList();
                var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                if (codes.Count == 0) links.Children.Add(new TextBlock { Text = label, Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });
                foreach (var code in codes) links.Children.Add(Link($"{label}  →  {code}", code));
                row.Children.Add(links);
                _list.Children.Add(row);
            }
        }
    }

    private IEnumerable<(string Event, string Code)> Collect()
    {
        for (var type = 0; type < _object.Events.Count; type++)
        {
            foreach (var evt in _object.Events[type])
            {
                var label = EventNames.Label((uint)type, evt.EventSubtype, _workspace?.Data);
                foreach (var action in evt.Actions)
                    if (action.CodeId?.Name?.Content is { } code) yield return (label, code);
            }
        }
    }

    private Button Link(string text, string code)
    {
        var button = new Button { Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, Classes = { "link" } };
        button.Click += (_, _) => _open(ResourceTypes.KeyOf("code", code));
        return button;
    }

    /// <summary>Runs the edit; a refusal (the object already has that event) shows as a message instead of an error.</summary>
    private bool Run(ObjectEventCommand command)
    {
        try
        {
            _workspace!.Edit(command);
            _message.Text = "";
            return true;
        }
        catch (InvalidOperationException e)
        {
            _message.Text = e.Message;
            return false;
        }
    }

    private StackPanel AddRow(Workspace workspace)
    {
        var types = new ComboBox { Name = "NewEventType", ItemsSource = EventNames.Types.Select(t => t.ToString()).ToList(), SelectedIndex = 0, MinWidth = 140 };
        var subtypes = new ComboBox { Name = "NewEventSubtype", MinWidth = 180, IsVisible = false };
        var add = new Button { Name = "AddEvent", Content = Loc.Current["object.addEvent"] };
        List<(string Label, uint Value)> shown = [];
        void Refresh()
        {
            var type = EventNames.Types[Math.Max(0, types.SelectedIndex)];
            shown = [.. EventNames.Subtypes(type, workspace.Data)];
            subtypes.ItemsSource = shown.Select(s => s.Label).ToList();
            subtypes.SelectedIndex = shown.Count > 0 ? 0 : -1;
            subtypes.IsVisible = EventNames.HasSubtype(type);
        }

        types.SelectionChanged += (_, _) => Refresh();
        Refresh();
        add.Click += (_, _) =>
        {
            var type = EventNames.Types[Math.Max(0, types.SelectedIndex)];
            var subtype = EventNames.HasSubtype(type) && subtypes.SelectedIndex >= 0 ? shown[subtypes.SelectedIndex].Value : 0u;
            var name = _object.Name.Content ?? "";
            if (Run(new ObjectEventCommand(name, ObjectEventCommand.Add, (uint)type, subtype)))
                _open(ResourceTypes.KeyOf("code", ObjectEventCommand.CodeName(name, (uint)type, subtype)));
        };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { types, subtypes, add } };
    }
}
