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
    private readonly SpriteFramesView? _sprite;

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
        // The sprite strip and the room canvas bring their own inset; every other header gets the same one so nothing touches the tab edge.
        if (header is not (null or SpriteFramesView or RoomPreview) && header.Margin == default) header.Margin = new Thickness(8, 8, 8, 0);
        if (header is SpriteFramesView sprite)
        {
            _sprite = sprite;
            sprite.OriginLockChanged += () => Fill();
        }

        if (Sections.TryGetValue(type, out var sections)) _closedGroups.UnionWith(sections.Select(x => "§" + x.Key));
        var body = new DockPanel();
        SetDock(_filter, Avalonia.Controls.Dock.Top);
        SetDock(_message, Avalonia.Controls.Dock.Top);
        body.Children.Add(_filter);
        body.Children.Add(_message);
        body.Children.Add(new ScrollViewer { Content = _rows });
        if (header is RoomPreview or StackPanel)
        {
            // A room, sequence or font editor is tall: the splitter lets the canvas and the properties share the tab at any window size.
            var tall = header is RoomPreview || type == "fonts"; // a font's glyph list, kerning and glyph fields need most of the tab
            var top = new RowDefinition(tall ? new GridLength(3, GridUnitType.Star) : GridLength.Auto) { MinHeight = tall ? 200 : 0 };
            var split = new Grid { RowDefinitions = { top, new RowDefinition(GridLength.Auto), new RowDefinition(2, GridUnitType.Star) { MinHeight = 120 } } };
            var splitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows, IsVisible = tall };
            // A stacked header takes the room its content needs, at most 60% of the tab, and scrolls beyond that.
            if (!tall) split.SizeChanged += (_, e) => top.MaxHeight = e.NewSize.Height * 0.6;
            Grid.SetRow(splitter, 1);
            Grid.SetRow(body, 2);
            split.Children.Add(header is StackPanel ? new ScrollViewer { Content = header } : header);
            split.Children.Add(splitter);
            split.Children.Add(body);
            Children.Add(split);
        }
        else
        {
            if (header is not null)
            {
                SetDock(header, Avalonia.Controls.Dock.Top);
                Children.Add(header);
            }

            Children.Add(body);
        }

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
        if (!_applying) Fill(false);
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

    private List<PropertyRow>? _shown;

    private static bool Same(PropertyRow a, PropertyRow b) =>
        a.Path == b.Path && a.Name == b.Name && a.Kind == b.Kind && a.Value == b.Value && a.Depth == b.Depth && a.ReadOnly == b.ReadOnly && a.Target == b.Target
        && (a.Choices ?? []).SequenceEqual(b.Choices ?? []);

    /// <param name="force">False after an edit elsewhere: the rows are built again only when what they show changed, which keeps typed text and the scroll position.</param>
    private void Fill(bool force = true)
    {
        if (PropertyModel.FindRoot(_workspace.Data, _type, _name) is not { } root)
        {
            _rows.Children.Clear();
            _headings.Clear();
            _shown = null;
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

        if (shown is null)
        {
            _rows.Children.Clear();
            _headings.Clear();
            _shown = null;
            return;
        }

        var text = _filter.Text?.Trim() ?? "";
        var collapsed = new List<string>();
        var kept = new List<PropertyRow>();
        foreach (var described in PropertyModel.Describe(shown))
        {
            var row = _prefix.Length > 0 ? described with { Path = _prefix + "." + described.Path } : described;
            if (_prefix.Length == 0 && Covered(row.Path)) continue;
            if (text.Length > 0 && row.Kind is not PropertyKind.Group && !row.Path.Contains(text, StringComparison.OrdinalIgnoreCase) && !row.Value.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            // A filter shows every match; without one the rows under a closed heading stay out.
            if (text.Length == 0 && collapsed.Any(c => row.Path.StartsWith(c + ".", StringComparison.Ordinal) || row.Path.StartsWith(c + "[", StringComparison.Ordinal))) continue;
            kept.Add(row);
            if (text.Length == 0 && IsClosed(row)) collapsed.Add(row.Path);
        }

        var wanted = _prefix.Length == 0 && text.Length == 0 ? Sectioned(kept) : kept;
        if (!force && _shown is not null && _shown.Count == wanted.Count && _shown.Zip(wanted).All(pair => Same(pair.First, pair.Second))) return;
        _shown = wanted;
        _rows.Children.Clear();
        _headings.Clear();
        foreach (var row in wanted)
        {
            _rows.Children.Add(Build(row));
            if (row.Kind is PropertyKind.Group or PropertyKind.List) _headings[row.Path] = row.Kind;
        }
    }

    /// <summary>The list of each type that the view above the property list already edits, so it is not listed twice.</summary>
    private static readonly Dictionary<string, string> CoveredLists = new() { ["paths"] = "Points", ["fonts"] = "Glyphs", ["particleSystems"] = "Emitters", ["sequences"] = "Tracks", ["timelines"] = "Moments" };

    /// <summary>True for a field the view above the list already edits (shader sources, the items of a list in <see cref="CoveredLists"/>).</summary>
    private bool Covered(string path) => _type == "shaders"
        ? ShaderSourceView.SourceProperties.Contains(path)
        : CoveredLists.TryGetValue(_type, out var list) && (path == list || path.StartsWith(list + "[", StringComparison.Ordinal));

    /// <summary>Fields that GameMaker keeps in their own collapsible section; they start closed and sit after the general fields. A filter shows them in place.</summary>
    private static readonly Dictionary<string, (string Key, string[] Fields)[]> Sections = new()
    {
        ["objects"] = [("properties.section.physics", ["UsesPhysics", "IsSensor", "CollisionShape", "Density", "Restitution", "Group", "LinearDamping", "AngularDamping", "Friction", "Awake", "Kinematic"])],
        ["sprites"] = [("properties.section.texture", ["Transparent", "Smooth", "Preload"]), ("properties.section.mask", ["BBoxMode", "SepMasks", "MarginLeft", "MarginRight", "MarginBottom", "MarginTop"])],
    };

    private List<PropertyRow> Sectioned(List<PropertyRow> rows)
    {
        if (!Sections.TryGetValue(_type, out var sections)) return rows;
        var inSection = sections.SelectMany(s => s.Fields).ToHashSet(StringComparer.Ordinal);
        var result = rows.Where(r => !inSection.Contains(r.Path)).ToList();
        foreach (var (key, fields) in sections)
        {
            var heading = new PropertyRow("§" + key, Loc.Current[key], PropertyKind.Group, "", 0, true);
            result.Add(heading);
            if (!IsClosed(heading)) result.AddRange(rows.Where(r => fields.Contains(r.Path)).Select(r => r with { Depth = 1 }));
        }

        return result;
    }

    private bool IsClosed(PropertyRow row) => row.Kind switch
    {
        PropertyKind.List => !_openLists.Contains(row.Path),
        PropertyKind.Group => _closedGroups.Contains(row.Path),
        _ => false,
    };

    private readonly Dictionary<string, PropertyKind> _headings = [];

    /// <summary>The field name as words: MarginLeft shows as "Margin Left", and the sprite fields SVersion and SSpriteType as "Special Version" and "Special Sprite Type". The path, filter and edits keep the field name.</summary>
    public static string Spaced(string name) => name is ['S', >= 'A' and <= 'Z', >= 'a' and <= 'z', ..] ? "Special " + Spaced(name[1..]) : string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])) ? " " + c : c.ToString()));

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
        // Read-only rows have no box, so they get the height of an editable row to keep the list evenly spaced.
        var line = new DockPanel { LastChildFill = true, MinHeight = heading ? 0 : 34 };
        SetDock(label, Avalonia.Controls.Dock.Left);
        label.Width = 220;
        if (heading)
        {
            label.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            label.PointerPressed += (_, _) => Toggle(row.Path);
        }

        line.Children.Add(label);
        // The add row and the remove buttons of a list of strings (UTMT-CTRL-06).
        if (row.Addable) line.Children.Add(EntryButton("ListAdd_" + row.Path, Loc.Current["properties.addEntry"], new ListEntryCommand(_type, _name, row.Path, ListEntryCommand.Add, -1)));
        if (row.Removable && ListEntryPath(row.Path) is var (list, at)) line.Children.Add(EntryButton("ListRemove_" + row.Path, Loc.Current["timelines.remove"], new ListEntryCommand(_type, _name, list, ListEntryCommand.Remove, at)));
        var editor = Editor(row);
        if (_sprite is { OriginLocked: true } && _prefix.Length == 0 && row.Path is "OriginX" or "OriginY") editor.IsEnabled = false;
        line.Children.Add(editor);
        return line;
    }

    private static (string List, int Index)? ListEntryPath(string path) => path.LastIndexOf('[') is > 0 and var open && int.TryParse(path[(open + 1)..^1], out var at) ? (path[..open], at) : null;

    private Button EntryButton(string name, string text, ListEntryCommand command)
    {
        var button = new Button { Name = name, Content = text, Margin = new Thickness(8, 0, 0, 0) };
        SetDock(button, Avalonia.Controls.Dock.Right);
        button.Click += (_, _) =>
        {
            try
            {
                _workspace.Edit(command);
                _message.Text = "";
            }
            catch (InvalidOperationException e)
            {
                _message.Text = Loc.Current.Format("properties.refused", ("path", name), ("reason", e.Message));
            }

            Fill();
        };
        return button;
    }

    // A swatch that opens three sliders (red, green, blue) and the number itself in a box beside it (UTMT-CTRL-05). The picked color is committed when the flyout closes, so a drag is one undo step; the top byte of the stored value is kept.
    private StackPanel ColorEditor(PropertyRow row, uint color)
    {
        var (r, g, b) = PropertyModel.Rgb(color);
        var sliders = new[] { r, g, b }.Select((v, n) => new Slider { Minimum = 0, Maximum = 255, Value = v, Width = 220, Name = $"Color{"RGB"[n]}_{row.Path}" }).ToArray();
        var swatch = new Border { Width = 40, Height = 22, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), BorderBrush = (Avalonia.Media.IBrush?)Application.Current?.FindResource("Border.Subtle") };
        Avalonia.Media.IBrush Brush() => new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb((byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value));
        swatch.Background = Brush();
        foreach (var slider in sliders) slider.ValueChanged += (_, _) => swatch.Background = Brush();
        var pick = new Button { Name = "Picker_" + row.Path, Content = swatch, Padding = new Thickness(3), IsEnabled = !row.ReadOnly };
        var flyout = new Flyout { Content = new StackPanel { Spacing = 4, Children = { sliders[0], sliders[1], sliders[2] } } };
        flyout.Closed += (_, _) =>
        {
            var next = PropertyModel.WithRgb(color, (byte)sliders[0].Value, (byte)sliders[1].Value, (byte)sliders[2].Value);
            if (next != color) Commit(row.Path, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
        };
        pick.Flyout = flyout;
        var box = new TextBox { Name = "Prop_" + row.Path, Text = row.Value, IsReadOnly = row.ReadOnly, Width = 120 };
        void Done()
        {
            if (!row.ReadOnly && box.Text != row.Value && !Commit(row.Path, box.Text ?? "")) box.Text = row.Value;
        }

        box.LostFocus += (_, _) => Done();
        box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Done(); };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { pick, box } };
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
                return new TextBlock { Text = row.Value.Length > 0 || row.Kind == PropertyKind.List ? row.Value : Loc.Current["explorer.noValue"], Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
            case PropertyKind.Bool:
                var check = new CheckBox { Name = "Prop_" + row.Path, IsChecked = row.Value == "True", IsEnabled = !row.ReadOnly };
                check.IsCheckedChanged += async (_, _) =>
                {
                    if (!check.IsEnabled || (check.IsChecked == true ? "True" : "False") == row.Value) return;
                    // Turning the debugger on (clearing "disabled") can make the normal runner hang, so it asks first (UTMT-GEN-05).
                    if (row.Path.EndsWith("IsDebuggerDisabled", StringComparison.Ordinal) && check.IsChecked == false && TopLevel.GetTopLevel(this) is Window owner
                        && !await ProjectDialogs.AskConfirmAsync(owner, Loc.Current["warnings.debuggerAskTitle"], Loc.Current["warnings.debuggerAsk"]))
                    {
                        check.IsChecked = true;
                        return;
                    }

                    Commit(row.Path, check.IsChecked == true ? "True" : "False");
                };
                return check;
            case PropertyKind.Color when uint.TryParse(row.Value, System.Globalization.CultureInfo.InvariantCulture, out var color):
                return ColorEditor(row, color);
            case PropertyKind.Flags:
                // One check box per single flag (UTMT-CTRL-04); a change commits the whole set as text.
                var set = row.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                // Parts of the value that no check box stands for (a numeric field inside the flags, a named combination, an unknown bit) stay as they are.
                var extras = set.Where(part => !(row.Choices ?? []).Contains(part)).ToList();
                var boxes = new WrapPanel { Name = "Prop_" + row.Path, IsEnabled = !row.ReadOnly };
                foreach (var flag in row.Choices ?? [])
                {
                    var one = new CheckBox { Content = Spaced(flag), IsChecked = set.Contains(flag), Margin = new Thickness(0, 0, 12, 0), Name = "Flag_" + row.Path + "_" + flag };
                    one.IsCheckedChanged += (_, _) =>
                    {
                        if (one.IsChecked == true) set.Add(flag); else set.Remove(flag);
                        Commit(row.Path, PropertyModel.FlagsText((row.Choices ?? []).Where(set.Contains).Concat(extras)));
                    };
                    boxes.Children.Add(one);
                }

                return boxes;
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
