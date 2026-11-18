using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The events of an object above its properties: each event with the code entries it runs as links that open the code, a button to remove it,
/// and a row that adds an event of any type with an empty code entry. Every change is one undoable step.
/// </summary>
public sealed partial class ObjectEventsView : StackPanel
{
    private readonly GameMakerGameObject _object;
    private readonly Action<string> _open;
    private readonly Workspace? _workspace;
    // One event per line, as a list.
    private readonly StackPanel _list = new() { Name = "EventList", Spacing = 2 };
    private readonly TextBlock _message = new() { Name = "EventMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    private readonly Func<string, Control>? _codePane;
    private readonly ContentControl _pane = new() { Name = "EventCodePane", MinWidth = 360, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch, IsVisible = false };
    private readonly GridSplitter? _splitter; // only with a code pane
    private readonly ColumnDefinition _paneColumn = new(GridLength.Star);

    // codePane makes a code editor for an entry; when given, a double-click on an event shows its code beside the list instead of opening a tab.
    public ObjectEventsView(GameMakerGameObject obj, Action<string> open, Workspace? workspace = null, Func<string, Control>? codePane = null)
    {
        (_object, _open, _workspace, _codePane) = (obj, open, workspace, codePane);
        Name = "ObjectEvents";
        Spacing = 6;
        // plain controls: an items control in a docked header got no size in the tab
        if (codePane is null)
        {
            Children.Add(_list);
        }
        else
        {
            // The pane and its splitter appear with the first code shown; before that the list has the whole width.
            var side = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star) { MinWidth = 120 }, new ColumnDefinition(GridLength.Auto), _paneColumn } };
            _splitter = new GridSplitter { Width = 5, VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns, IsVisible = false };
            Grid.SetColumn(_splitter, 1);
            Grid.SetColumn(_pane, 2);
            _paneColumn.Width = new GridLength(0);
            side.Children.Add(_list);
            side.Children.Add(_splitter);
            side.Children.Add(_pane);
            Children.Add(side);
        }
        if (workspace is not null)
        {
            Children.Add(AddRow(workspace));
            Children.Add(_message);
        }

        Fill();
        if (workspace is null) return;
        DetachedFromVisualTree += (_, _) =>
        {
            workspace.History.Changed -= Fill;
            workspace.Nicknames.Changed -= Fill;
        };
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += Fill;
            workspace.Nicknames.Changed += Fill;
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
                text.Append(type).Append('/').Append(evt.EventSubtype).Append(' ').Append(EventNames.Label((uint)type, evt.EventSubtype, _workspace?.Data, Loc.Current));
                foreach (var action in evt.Actions) text.Append(';');
            }

        // The links name the code entries, which can be renamed without the object changing.
        foreach (var (_, code) in Collect()) text.Append('|').Append(code);

        if (_workspace is not null)
            foreach (var inherited in InheritedEvents.Of(_object)) text.Append('^').Append(inherited.Owner).Append(inherited.Type).Append('/').Append(inherited.Subtype);

        // The links show the nickname of the owner, so a nickname change makes them again.
        text.Append('~').Append(_workspace?.Nicknames.Version);
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
                var label = EventNames.Label(kind, subtype, _workspace?.Data, Loc.Current);
                // The "..." button sits right next to the link; the links take the room that is left and a name longer than that ends in an ellipsis (the hover card has all of it).
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), HorizontalAlignment = HorizontalAlignment.Left };

                var codes = evt.Actions.Select(a => a.CodeId?.Name?.Content).OfType<string>().ToList();
                var links = new WrapPanel { ItemSpacing = 4, Margin = new Thickness(0, 0, 8, 0) };
                if (codes.Count == 0) links.Children.Add(new TextBlock { Text = label, Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });
                foreach (var code in codes) links.Children.Add(Link($"{label}  →  {_workspace?.Nicknames.CodeLabel(code) ?? code}", code));
                row.Children.Add(links);
                if (_workspace is not null)
                {
                    var tools = EventTools(kind, subtype);
                    Grid.SetColumn(tools, 1);
                    row.Children.Add(tools);
                }

                OpenOnDoubleTap(row, codes.FirstOrDefault());
                _list.Children.Add(row);
            }
        }

        if (_workspace is not null) AddInherited(_workspace);
    }

    private IEnumerable<(string Event, string Code)> Collect()
    {
        for (var type = 0; type < _object.Events.Count; type++)
        {
            foreach (var evt in _object.Events[type])
            {
                var label = EventNames.Label((uint)type, evt.EventSubtype, _workspace?.Data, Loc.Current);
                foreach (var action in evt.Actions)
                    if (action.CodeId?.Name?.Content is { } code) yield return (label, code);
            }
        }
    }

    private Button Link(string text, string code)
    {
        var button = new Button { Content = new TextBlock { Text = text, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, TextWrapping = Avalonia.Media.TextWrapping.NoWrap }, HorizontalContentAlignment = HorizontalAlignment.Left, Classes = { "link" } };
        button.Click += (_, _) => _open(ResourceTypes.KeyOf("code", code));
        HoverCards.Attach(button, ResourceTypes.KeyOf("code", code), text);
        return button;
    }

    /// <summary>Runs the edit; a refusal (the object already has that event) shows as a message instead of an error.</summary>
    private bool Run(ObjectEventCommand command)
    {
        try
        {
            if (_workspace is null) return false;
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

    /// <summary>The event code beside the list (pending text of the one shown before is applied first), or in its own tab without a pane.</summary>
    public void ShowCode(string code)
    {
        if (_codePane is null)
        {
            _open(ResourceTypes.KeyOf("code", code));
            return;
        }

        if (_pane.Content is CodeEditorTab { HasPending: true } old && !old.Apply()) return;
        if (!_pane.IsVisible)
        {
            _pane.IsVisible = true;
            _splitter?.IsVisible = true;
            _paneColumn.Width = GridLength.Star;
            _paneColumn.MinWidth = 120;
        }

        _pane.Content = _codePane(code);
    }

    private void OpenOnDoubleTap(Control row, string? code)
    {
        if (code is not null) row.DoubleTapped += (_, _) => ShowCode(code);
    }

    internal void AddEvent(uint type, uint subtype)
    {
        var name = _object.Name.Content ?? "";
        if (Run(new ObjectEventCommand(name, ObjectEventCommand.Add, type, subtype)))
            _open(ResourceTypes.KeyOf("code", ObjectEventCommand.CodeName(name, type, subtype)));
    }

    private StackPanel AddRow(Workspace workspace) => new() { Orientation = Orientation.Horizontal, Spacing = 6, Children = { CascadeButton(workspace) } };
}
