using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// One editor for every resource: its properties as a filtered list. A change is committed when the box loses focus or Enter is pressed,
/// as one undoable <see cref="SetPropertyCommand"/>. Text the property cannot hold is refused with a message and the old value stays.
/// </summary>
public sealed class PropertyGridTab : DockPanel
{
    private readonly Workspace _workspace;
    private readonly TextBox _filter = new() { Name = "PropertyFilter", PlaceholderText = Loc.Current["properties.filter"], Margin = new Thickness(8, 4) };
    private readonly TextBlock _message = new() { Name = "PropertyMessage", Margin = new Thickness(8, 0), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _rows = new() { Margin = new Thickness(8), Spacing = 2 };
    private readonly string _type, _name, _prefix;
    private bool _applying;

    // Groups start open and lists closed (a list can hold thousands of rows); a click on the heading flips it.
    private readonly HashSet<string> _openLists = [];
    private readonly HashSet<string> _closedGroups = [];

    /// <param name="workspace">The open data file.</param>
    /// <param name="type">Explorer type key of the resource.</param>
    /// <param name="name">Name of the resource.</param>
    /// <param name="header">A picture of the resource shown above the properties, such as the frames of a sprite.</param>
    /// <param name="subPath">A path inside the resource, such as <c>Layers[2].BackgroundData</c>: only that object is shown and edited.</param>
    public PropertyGridTab(Workspace workspace, string type, string name, Control? header = null, string subPath = "")
    {
        (_workspace, _type, _name, _prefix) = (workspace, type, name, subPath);
        if (header is not null)
        {
            SetDock(header, Avalonia.Controls.Dock.Top);
            Children.Add(header);
        }

        SetDock(_filter, Avalonia.Controls.Dock.Top);
        SetDock(_message, Avalonia.Controls.Dock.Top);
        Children.Add(_filter);
        Children.Add(_message);
        Children.Add(new ScrollViewer { Content = _rows });
        _filter.TextChanged += (_, _) => Fill();
        // Only a shown tab follows the model; undo, redo and other editors reach it through the history.
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= OnHistoryChanged;
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += OnHistoryChanged;
            Fill();
        };
        Fill();
    }

    public string Message => _message.Text ?? "";

    /// <summary>A reference was clicked: the Explorer key of the entry to open.</summary>
    public event Action<string>? OpenRequested;

    private void OnHistoryChanged()
    {
        if (!_applying) Fill();
    }

    /// <summary>Commits one row. False, with a message, when the text does not fit.</summary>
    public bool Commit(string path, string text)
    {
        _applying = true;
        try
        {
            _workspace.Edit(new SetPropertyCommand(_type, _name, path, text));
            _message.Text = "";
            return true;
        }
        catch (InvalidOperationException e)
        {
            _message.Text = Loc.Current.Format("properties.refused", ("path", path), ("reason", e.Message));
            return false;
        }
        finally
        {
            _applying = false;
            Fill();
        }
    }

    private void Fill()
    {
        _rows.Children.Clear();
        _headings.Clear();
        if (PropertyModel.FindRoot(_workspace.Data, _type, _name) is not { } root)
        {
            _message.Text = Loc.Current.Format("properties.missing", ("name", _name));
            return;
        }

        object? shown;
        try
        {
            shown = _prefix.Length > 0 ? PropertyModel.ReadRaw(root, _prefix) : root;
        }
        catch (InvalidOperationException)
        {
            shown = null;
        }

        if (shown is null) return;
        var text = _filter.Text?.Trim() ?? "";
        var collapsed = new List<string>();
        foreach (var described in PropertyModel.Describe(shown))
        {
            var row = _prefix.Length > 0 ? described with { Path = _prefix + "." + described.Path } : described;
            if (text.Length > 0 && row.Kind is not PropertyKind.Group && !row.Path.Contains(text, StringComparison.OrdinalIgnoreCase) && !row.Value.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            // A filter shows every match; without one the rows under a closed heading stay out.
            if (text.Length == 0 && collapsed.Any(c => row.Path.StartsWith(c + ".", StringComparison.Ordinal) || row.Path.StartsWith(c + "[", StringComparison.Ordinal))) continue;
            _rows.Children.Add(Build(row));
            if (row.Kind is PropertyKind.Group or PropertyKind.List) _headings[row.Path] = row.Kind;
            if (text.Length == 0 && IsClosed(row)) collapsed.Add(row.Path);
        }
    }

    private bool IsClosed(PropertyRow row) => row.Kind switch
    {
        PropertyKind.List => !_openLists.Contains(row.Path),
        PropertyKind.Group => _closedGroups.Contains(row.Path),
        _ => false,
    };

    private readonly Dictionary<string, PropertyKind> _headings = [];

    /// <summary>The field name as words: MarginLeft shows as "Margin Left". The path, filter and edits keep the field name.</summary>
    private static string Spaced(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])) ? " " + c : c.ToString()));

    /// <summary>Opens or closes the group or list with this path (what a click on its heading does).</summary>
    public void Toggle(string path)
    {
        if (!_headings.TryGetValue(path, out var kind)) return;
        if (kind == PropertyKind.List) _ = _openLists.Add(path) || _openLists.Remove(path);
        else _ = _closedGroups.Add(path) || _closedGroups.Remove(path);
        Fill();
    }

    private DockPanel Build(PropertyRow row)
    {
        var heading = row.Kind is PropertyKind.Group or PropertyKind.List;
        var label = new TextBlock { Text = heading ? (IsClosed(row) ? "▸ " : "▾ ") + Spaced(row.Name) : Spaced(row.Name), Margin = new Thickness(row.Depth * 16, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Classes = { row.Kind is PropertyKind.Group or PropertyKind.List ? "accent" : "plain" } };
        var line = new DockPanel { LastChildFill = true };
        SetDock(label, Avalonia.Controls.Dock.Left);
        label.Width = 220;
        if (heading)
        {
            label.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            label.PointerPressed += (_, _) => Toggle(row.Path);
        }

        line.Children.Add(label);
        line.Children.Add(Editor(row));
        return line;
    }

    private Control Editor(PropertyRow row)
    {
        switch (row.Kind)
        {
            case PropertyKind.Group:
                return new TextBlock();
            case PropertyKind.Info when row.Target is { } target:
                // A reference to another entry: its name is a link that opens it.
                var link = new Button { Name = "Open_" + row.Path, Content = row.Value, Classes = { "link" }, HorizontalAlignment = HorizontalAlignment.Left };
                link.Click += (_, _) => OpenRequested?.Invoke(target);
                return link;
            case PropertyKind.List or PropertyKind.Info:
                return new TextBlock { Text = row.Value, Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
            case PropertyKind.Bool:
                var check = new CheckBox { Name = "Prop_" + row.Path, IsChecked = row.Value == "True", IsEnabled = !row.ReadOnly };
                check.IsCheckedChanged += (_, _) => { if (check.IsEnabled && (check.IsChecked == true ? "True" : "False") != row.Value) Commit(row.Path, check.IsChecked == true ? "True" : "False"); };
                return check;
            case PropertyKind.Enum:
                var choices = new ComboBox { Name = "Prop_" + row.Path, ItemsSource = row.Choices, SelectedItem = row.Value, IsEnabled = !row.ReadOnly, MinWidth = 200 };
                choices.SelectionChanged += (_, _) => { if (choices.SelectedItem is string pick && pick != row.Value) Commit(row.Path, pick); };
                return choices;
            default:
                var box = new TextBox { Name = "Prop_" + row.Path, Text = row.Value, IsReadOnly = row.ReadOnly };
                void Done()
                {
                    if (!row.ReadOnly && box.Text != row.Value && !Commit(row.Path, box.Text ?? "")) box.Text = row.Value;
                }

                box.LostFocus += (_, _) => Done();
                box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Done(); };
                return box;
        }
    }
}
