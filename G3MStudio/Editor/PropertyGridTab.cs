using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// One editor for every resource: its properties as a filtered list. A change is committed when the box loses focus or Enter is pressed,
/// as one undoable <see cref="SetPropertyCommand"/>. Text the property cannot hold is refused with a message and the old value stays.
/// </summary>
public sealed partial class PropertyGridTab : DockPanel
{
    private readonly Workspace _workspace;
    private readonly TextBox _filter = new TextBox { Name = "PropertyFilter", Margin = new Thickness(8, 4) }.Tr(TextBox.PlaceholderTextProperty, "properties.filter");
    private readonly TextBlock _message = new() { Name = "PropertyMessage", Margin = new Thickness(8, 0), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _rows = new() { Margin = new Thickness(8), Spacing = 4 };
    private readonly string _type, _name, _prefix;
    private bool _applying;
    private readonly SpriteFramesView? _sprite;

    // Groups start open and lists closed (a list can hold thousands of rows); a click on the heading flips it.
    private readonly HashSet<string> _openLists = [];
    private readonly HashSet<string> _closedGroups = [];

    // While a filter is typed, headings fold on their own: every match is shown unless the user folded its heading in this filtered view.
    private readonly HashSet<string> _foldedInFilter = [];
    private bool Filtering => (_filter.Text?.Trim() ?? "").Length > 0;

    /// <param name="workspace">The open data file.</param>
    /// <param name="type">Explorer type key of the resource.</param>
    /// <param name="name">Name of the resource.</param>
    /// <param name="header">A picture of the resource shown above the properties, such as the frames of a sprite.</param>
    /// <param name="subPath">A path inside the resource, such as <c>Layers[2].BackgroundData</c>: only that object is shown and edited.</param>
    /// <param name="memory">Where the height the user gave the property list is kept; without it the split is not remembered.</param>
    public PropertyGridTab(Workspace workspace, string type, string name, Control? header = null, string subPath = "", PaneMemory? memory = null)
    {
        (_workspace, _type, _name, _prefix) = (workspace, type, name, subPath);
        // The sprite strip and the room canvas bring their own inset; every other header gets the same one so nothing touches the tab edge.
        if (header is not (null or SpriteFramesView or SpriteEditorView or RoomPreview) && header.Margin == default) header.Margin = new Thickness(8, 8, 8, 0);
        if ((header is SpriteEditorView editor ? editor.Frames : header as SpriteFramesView) is { } sprite)
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
        Children.Add(header is null ? body : Split(header, type, body, memory));

        _filter.TextChanged += (_, _) =>
        {
            if (!Filtering) _foldedInFilter.Clear();
            Fill();
        };
        // Only a shown tab follows the model; undo, redo and other editors reach it through the history.
        DetachedFromVisualTree += (_, _) =>
        {
            workspace.History.Changed -= OnHistoryChanged;
            workspace.Locks.Changed -= OnLocksChanged;
            workspace.Nicknames.Changed -= OnNicknamesChanged;
        };
        LanguageWatch.Follow(this, OnLanguageChanged);
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += OnHistoryChanged;
            workspace.Locks.Changed += OnLocksChanged;
            workspace.Nicknames.Changed += OnNicknamesChanged;
            // The rows built in the constructor are still right unless an edit came while the tab was hidden; building them again on every switch cost 100 ms or more.
            Fill(_nicknameVersion != workspace.Nicknames.Version); // a nickname changed meanwhile is no row difference (the links keep their value), so it forces the rows
        };
        Fill();
    }

    public string Message => _message.Text ?? "";

    /// <summary>A reference was clicked: the Explorer key of the entry to open.</summary>
    public event Action<string>? OpenRequested;

    /// <summary>Find Usages was chosen on a reference field; the key of the entry it points at.</summary>
    public event Action<string>? FindUsagesRequested;

    /// <summary>Edit Image was chosen on a reference field that points at a sprite, tile set, font or texture page item; the key of that entry.</summary>
    public event Action<string>? EditImageRequested;

    private void OnLocksChanged() => Fill(false);

    // The reference links show nicknames: the rows are built again.
    private void OnNicknamesChanged()
    {
        _shown = null;
        Fill(false);
    }

    // The rows carry texts such as "No items" and "Add entry": a new language builds them again.
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Loc.Current.Language)) return;
        _shown = null;
        Fill(false);
    }

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
        var locked = _workspace.Locks.Covers(_name);
        foreach (var described in PropertyModel.Describe(shown))
        {
            var row = _prefix.Length > 0 ? described with { Path = _prefix + "." + described.Path } : described;
            if (_prefix.Length == 0 && Covered(row.Path)) continue;
            if (text.Length > 0 && row.Kind is not PropertyKind.Group && !row.Path.Contains(text, StringComparison.OrdinalIgnoreCase) && !row.Value.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            // The rows under a folded heading stay out, with or without a filter.
            if (collapsed.Any(c => row.Path.StartsWith(c + ".", StringComparison.Ordinal) || row.Path.StartsWith(c + "[", StringComparison.Ordinal))) continue;
            kept.Add(locked ? row with { ReadOnly = true } : row);
            if (IsClosed(row)) collapsed.Add(row.Path);
        }

        var wanted = _prefix.Length == 0 && text.Length == 0 ? Sectioned(kept) : kept;
        if (!force && _shown is not null && _shown.Count == wanted.Count && _shown.Zip(wanted).All(pair => Same(pair.First, pair.Second))) return;
        (_shown, _nicknameVersion) = (wanted, _workspace.Nicknames.Version);
        _rows.Children.Clear();
        _headings.Clear();
        var index = 0;
        foreach (var row in wanted)
        {
            _rows.Children.Add(index++ < EagerRows || row.Kind is PropertyKind.Group or PropertyKind.List ? Build(row) : Lazy(row));
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

    private bool IsClosed(PropertyRow row) => Filtering ? row.Kind is PropertyKind.Group or PropertyKind.List && _foldedInFilter.Contains(row.Path) : row.Kind switch
    {
        PropertyKind.List => !_openLists.Contains(row.Path),
        PropertyKind.Group => _closedGroups.Contains(row.Path),
        _ => false,
    };

    private readonly Dictionary<string, PropertyKind> _headings = [];

    // What a Usages hit is matched against to find the tab showing it.
    public string Resource => ResourceTypes.KeyOf(_type, _name);

    /// <summary>Narrows the rows to the property of this name, so a use lands on the field itself. Nothing changes when no property has the name.</summary>
    public void Reveal(string member)
    {
        if (member.Length == 0) return;
        var before = _filter.Text;
        _filter.Text = member;
        if (_rows.Children.Count == 0) _filter.Text = before;
    }

    /// <summary>Opens or closes the group or list with this path (what a click on its heading does).</summary>
    public void Toggle(string path)
    {
        if (!_headings.TryGetValue(path, out var kind)) return;
        if (Filtering) _ = _foldedInFilter.Add(path) || _foldedInFilter.Remove(path);
        else if (kind == PropertyKind.List) _ = _openLists.Add(path) || _openLists.Remove(path);
        else _ = _closedGroups.Add(path) || _closedGroups.Remove(path);
        Fill();
    }

    // About one screen of rows is built at once. A text box with its template costs around a millisecond, so the rows further down get an empty slot of the same
    // height and are built when a scroll or a resize brings them within a few hundred pixels of the view.
    private const int EagerRows = 20;
    private const double BuildMargin = 400;

    private Border Lazy(PropertyRow row)
    {
        var slot = new Border { MinHeight = 34 };
        void Near(object? sender, EffectiveViewportChangedEventArgs e)
        {
            var view = e.EffectiveViewport;
            if (view.Height <= 0 || view.Bottom < -BuildMargin || view.Top > slot.Bounds.Height + BuildMargin) return;
            slot.EffectiveViewportChanged -= Near;
            slot.Child = Build(row);
        }

        slot.EffectiveViewportChanged += Near;
        return slot;
    }

    private DockPanel Build(PropertyRow row)
    {
        var heading = row.Kind is PropertyKind.Group or PropertyKind.List;
        var label = new TextBlock { Text = heading ? (IsClosed(row) ? "▸ " : "▾ ") + Label(row.Name) : Label(row.Name), Margin = new Thickness(row.Depth * 16, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Classes = { row.Kind is PropertyKind.Group or PropertyKind.List ? "accent" : "plain" } };
        // Read-only rows have no box, so they get the height of an editable row to keep the list evenly spaced.
        var line = new DockPanel { LastChildFill = true, MinHeight = heading ? 0 : 32 };
        SetDock(label, Avalonia.Controls.Dock.Left);
        label.Width = 220;
        // A narrow tab puts the label above its editor instead of squeezing the editor to a sliver.
        var narrow = false;
        line.SizeChanged += (_, e) =>
        {
            if ((e.NewSize.Width < 460) == narrow) return;
            narrow = !narrow;
            SetDock(label, narrow ? Avalonia.Controls.Dock.Top : Avalonia.Controls.Dock.Left);
            label.Width = narrow ? double.NaN : 220;
        };
        if (heading)
        {
            label.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            label.PointerPressed += (_, _) => Toggle(row.Path);
        }

        line.Children.Add(label);
        // The add row and the remove buttons of a list of strings (UTMT-CTRL-06).
        if (row.Addable && row.TargetType is { } linkType) line.Children.Add(LinkAddButton("ListAdd_" + row.Path, row.Path, linkType));
        else if (row.Addable) line.Children.Add(EntryButton("ListAdd_" + row.Path, Loc.Current["properties.addEntry"], new ListEntryCommand(_type, _name, row.Path, ListEntryCommand.Add, -1)));
        if (row.Removable && ListEntryPath(row.Path) is var (list, at)) line.Children.Add(EntryButton("ListRemove_" + row.Path, Loc.Current["timelines.remove"], new ListEntryCommand(_type, _name, list, ListEntryCommand.Remove, at)));
        if (row.Removable && ListEntryPath(row.Path) is var (moveList, moveAt))
        {
            if (row.CanMoveDown) line.Children.Add(EntryButton("ListDown_" + row.Path, Loc.Current["properties.moveDown"], new ListEntryCommand(_type, _name, moveList, ListEntryCommand.Down, moveAt)));
            if (row.CanMoveUp) line.Children.Add(EntryButton("ListUp_" + row.Path, Loc.Current["properties.moveUp"], new ListEntryCommand(_type, _name, moveList, ListEntryCommand.Up, moveAt)));
        }

        var editor = Editor(row);
        if (_sprite is { OriginLocked: true } && _prefix.Length == 0 && row.Path is "OriginX" or "OriginY") editor.GateKey("sprites.originLockedTip");
        line.Children.Add(editor);
        return line;
    }

    private static (string List, int Index)? ListEntryPath(string path) => path.LastIndexOf('[') is > 0 and var open && int.TryParse(path[(open + 1)..^1], out var at) ? (path[..open], at) : null;

    // A list of links takes a new entry from the picker: the entry is the link to the entry chosen.
    private Button LinkAddButton(string name, string path, string type)
    {
        var button = new Button { Name = name, Content = Loc.Current["properties.addEntry"], Margin = new Thickness(8, 0, 0, 0) };
        SetDock(button, Avalonia.Controls.Dock.Right);
        button.Click += (_, _) => ResourcePicker.Show(button, _workspace, type, null, key => Add(path, key));
        return button.Gate(_workspace.Locks.Covers(_name) ? Loc.Current["link.locked"] : null);
    }

    /// <summary>Adds a link to the entry with this Explorer key at the end of the list at <paramref name="path"/>.</summary>
    public bool Add(string path, string key) => Run(new ListEntryCommand(_type, _name, path, ListEntryCommand.Add, -1, key));

    private bool Run(ListEntryCommand command)
    {
        try
        {
            _workspace.Edit(command);
            _message.Text = "";
            return true;
        }
        catch (InvalidOperationException e)
        {
            _message.Text = Loc.Current.Format("properties.refused", ("path", command.Args["path"]), ("reason", e.Message));
            return false;
        }
        finally { Fill(); }
    }

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


    private Control Editor(PropertyRow row)
    {
        switch (row.Kind)
        {
            case PropertyKind.Group:
                return new TextBlock();
            case PropertyKind.Info when row.Target is not null || row.Reference:
                return ReferenceEditor(row);
            case PropertyKind.List or PropertyKind.Info:
                return new TextBlock { Text = row.Count is { } n ? n == 0 ? Loc.Current["properties.noItems"] : Loc.Current.Format("properties.items", ("count", n)) : row.Value.Length > 0 ? row.Value : Loc.Current["explorer.noValue"], Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
            case PropertyKind.Bool:
                var check = new CheckBox { Name = "Prop_" + row.Path, IsChecked = row.Value == "True", IsEnabled = !row.ReadOnly };
                check.IsCheckedChanged += async (_, _) =>
                {
                    if (!check.IsEnabled || (check.IsChecked == true ? "True" : "False") == row.Value) return;
                    // Turning the debugger on (clearing "disabled") can make the normal runner hang, so it asks first (UTMT-GEN-05).
                    if (row.Path.EndsWith("IsDebuggerDisabled", StringComparison.Ordinal) && check.IsChecked == false && TopLevel.GetTopLevel(this) is Window owner
                        && !await (owner is MainWindow main ? main.Confirm(G3MStudio.Core.Settings.Prompts.DebuggerFlag, Loc.Current["warnings.debuggerAskTitle"], Loc.Current["warnings.debuggerAsk"]) : ProjectDialogs.AskConfirmAsync(owner, Loc.Current["warnings.debuggerAskTitle"], Loc.Current["warnings.debuggerAsk"])))
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
                    var one = new CheckBox { Content = FlagLabel(flag), IsChecked = set.Contains(flag), Margin = new Thickness(0, 0, 12, 0), Name = "Flag_" + row.Path + "_" + flag };
                    one.IsCheckedChanged += (_, _) =>
                    {
                        if (one.IsChecked == true) set.Add(flag); else set.Remove(flag);
                        Commit(row.Path, PropertyModel.FlagsText((row.Choices ?? []).Where(set.Contains).Concat(extras)));
                    };
                    boxes.Children.Add(one);
                }

                return boxes;
            case PropertyKind.Enum:
                var choices = new ComboBox { Name = "Prop_" + row.Path, ItemsSource = row.Choices, SelectedItem = row.Value, IsEnabled = !row.ReadOnly, MinWidth = 200, ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((s, _) => new TextBlock { Text = s is null ? "" : ChoiceLabel(s) }) };
                choices.SelectionChanged += (_, _) => { if (choices.SelectedItem is string pick && pick != row.Value) Commit(row.Path, pick); };
                return choices;
            default:
                var box = new TextBox { Name = "Prop_" + row.Path, Text = row.Value, IsReadOnly = row.ReadOnly, PlaceholderText = row.ReadOnly ? null : Loc.Current["properties.empty"] };
                // A number is a few digits: its box stays one field wide instead of stretching over the tab.
                if (row.Kind is PropertyKind.Whole or PropertyKind.Real) (box.MaxWidth, box.HorizontalAlignment) = (200, HorizontalAlignment.Left);
                void Done()
                {
                    if (!row.ReadOnly && box.Text != row.Value && !Commit(row.Path, box.Text ?? "")) box.Text = row.Value;
                }

                box.LostFocus += (_, _) => Done();
                box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) Done(); };
                return CharacterRow(row, box);
        }
    }
}
