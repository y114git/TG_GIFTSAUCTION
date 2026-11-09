using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using G3MStudio.Localization;
using G3MStudio.Mods;

namespace G3MStudio.Theme;

/// <summary>What a dialog button is for: Primary is the main action (accent, Enter), Cancel leaves without a result (Escape), Normal is any other action.</summary>
internal enum DialogRole { Normal, Primary, Cancel }

/// <summary>How the content of a dialog scrolls when it is taller (or wider) than the window; the action row never scrolls.</summary>
internal enum DialogScroll { None, Vertical, Both }

/// <summary>
/// The one frame of every dialog (design/ux-standards.md, Sizing and Focus): the content on top, an optional status line, and a right-aligned action row
/// that always stays visible. 16 px padding, 8 px gaps. Content taller than the window scrolls inside the frame; a long button row wraps to a second line.
/// </summary>
internal static class DialogFrame
{
    private static readonly AttachedProperty<bool> CommittedProperty = AvaloniaProperty.RegisterAttached<Window, Window, bool>("Committed", false);
    private static readonly AttachedProperty<bool> DiscardedProperty = AvaloniaProperty.RegisterAttached<Window, Window, bool>("Discarded", false);
    private static readonly AttachedProperty<Func<bool>?> DirtyProperty = AvaloniaProperty.RegisterAttached<Window, Window, Func<bool>?>("Dirty");

    /// <summary>A button of the action row. Primary and Normal buttons mark the close that follows as intended, so a guarded dialog does not ask about it.</summary>
    public static Button Action(Window window, string text, string name, Action click, DialogRole role = DialogRole.Normal) =>
        Wire(window, new Button { Content = text, Name = name }, click, role);

    /// <summary>The same for a button the dialog already holds in a field.</summary>
    public static Button Wire(Window window, Button button, Action click, DialogRole role)
    {
        button.IsDefault = role == DialogRole.Primary;
        button.IsCancel = role == DialogRole.Cancel;
        if (role == DialogRole.Primary && !button.Classes.Contains("primary")) button.Classes.Add("primary");
        button.Click += (_, _) =>
        {
            if (role == DialogRole.Cancel) Leave(window, click);
            else Commit(window, click);
        };
        return button;
    }

    /// <summary>Runs <paramref name="run"/> as an intended action: a close inside it is not asked about. Use it for a key (Enter) that does what the Primary button does.</summary>
    public static void Commit(Window window, Action run)
    {
        window.SetValue(CommittedProperty, true);
        try { run(); }
        finally { if (window.IsVisible) window.SetValue(CommittedProperty, false); }
    }

    /// <summary>The Cancel button: shared label, Escape. <paramref name="close"/> defaults to closing without a result.</summary>
    public static Button Cancel(Window window, Action? close = null, string name = "Cancel") => Action(window, Loc.Current["common.cancel"], name, close ?? window.Close, DialogRole.Cancel);

    /// <summary>The Close button of a window that has nothing to confirm: shared label, Escape, and Enter unless the window has boxes where Enter means something else.</summary>
    public static Button Close(Window window, string name = "Close", bool isDefault = true)
    {
        var button = Action(window, Loc.Current["common.close"], name, window.Close, DialogRole.Cancel);
        button.IsDefault = isDefault;
        return button;
    }

    /// <summary>The frame as a control: <paramref name="content"/>, then the status line, then the actions.</summary>
    public static Grid Build(Control content, IEnumerable<Control> actions, Control? status = null, DialogScroll scroll = DialogScroll.None)
    {
        var grid = new Grid { Margin = new Thickness(ModsUi.Space(4)), RowSpacing = ModsUi.Space(2) };
        Grid.SetIsSharedSizeScope(grid, true); // the label columns of every Form in the content line up
        grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        Control body = content;
        if (scroll != DialogScroll.None)
        {
            body = new ScrollViewer
            {
                Name = "DialogScroll",
                Content = content,
                HorizontalScrollBarVisibility = scroll == DialogScroll.Both ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
        }

        grid.Children.Add(body);
        // A stack, so an empty (hidden) status line leaves no gap above the buttons.
        var bottom = new StackPanel { Spacing = ModsUi.Space(2) };
        if (status is TextBlock text) text.HideWhenEmpty();
        if (status is not null) bottom.Children.Add(status);
        var row = new WrapPanel { Name = "DialogActions", ItemSpacing = ModsUi.Space(2), LineSpacing = ModsUi.Space(2), HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.AddRange(actions);
        bottom.Children.Add(row);
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(bottom, 1);
        grid.Children.Add(bottom);
        return grid;
    }

    /// <summary>Puts the frame in the window and, when <paramref name="dirty"/> is given, asks before a close would lose what was typed.</summary>
    public static void Show(Window window, Control content, IEnumerable<Control> actions, Control? status = null, DialogScroll scroll = DialogScroll.None, Func<bool>? dirty = null)
    {
        window.Content = Build(content, actions, status, scroll);
        if (dirty is not null) Guard(window, dirty);
    }

    /// <summary>
    /// Leaves the dialog without a result (Cancel, Escape, the window's close button): while a guarded dialog holds typed values that were not accepted, it asks first.
    /// A close made by code (the owner closing, a test) is never asked about. <paramref name="close"/> is what leaving does.
    /// </summary>
    public static void Leave(Window window, Action close)
    {
        if (window.GetValue(DirtyProperty) is not { } dirty || window.GetValue(DiscardedProperty) || window.GetValue(CommittedProperty) || !dirty())
        {
            close();
            return;
        }

        _ = AskAsync();

        async Task AskAsync()
        {
            if (!await ConfirmDiscardAsync(window)) return;
            window.SetValue(DiscardedProperty, true);
            close();
        }
    }

    /// <summary>Escape in a dialog: the same as its Cancel button.</summary>
    public static void RequestClose(Window window) => Leave(window, window.Close);

    /// <summary>Asks, through <see cref="Leave"/>, before the window's own close button drops what was typed while <paramref name="dirty"/> is true.</summary>
    public static void Guard(Window window, Func<bool> dirty)
    {
        window.SetValue(DirtyProperty, dirty);
        window.Closing += (_, e) =>
        {
            if (e.IsProgrammatic || window.GetValue(DiscardedProperty) || window.GetValue(CommittedProperty) || !dirty()) return;
            e.Cancel = true;
            Leave(window, window.Close);
        };
    }

    /// <summary>True when the text boxes, number boxes, check boxes and combo boxes read the same as they do now.</summary>
    public static Func<bool> Snapshot(params Control[] fields)
    {
        var start = Read(fields);
        return () => Read(fields) != start;
    }

    private static string Read(Control[] fields) => string.Join('\u0001', fields.Select(f => f switch
    {
        TextBox t => t.Text ?? "",
        NumericUpDown n => n.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
        CheckBox c => c.IsChecked.ToString(),
        ComboBox c => c.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "",
    }));

    /// <summary>"Discard changes?" with Discard and Keep editing; Keep editing is the default.</summary>
    public static async Task<bool> ConfirmDiscardAsync(Window owner)
    {
        var window = new Window
        {
            Title = Loc.Current["common.discardTitle"],
            Width = 420,
            MinWidth = 320,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        var discard = false;
        window.Content = Build(
            new TextBlock { Text = Loc.Current["common.discardMessage"], TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            [Action(window, Loc.Current["common.discard"], "Discard", () => { discard = true; window.Close(); }), Action(window, Loc.Current["common.keepEditing"], "KeepEditing", window.Close, DialogRole.Cancel)],
            scroll: DialogScroll.Vertical);
        await window.ShowDialog(owner);
        return discard;
    }

    /// <summary>
    /// Label and field rows in two columns: the label column is as wide as the longest label (up to 220) and its text wraps, so a long translation never clips.
    /// </summary>
    public static Grid Form(params (string Label, Control Field)[] rows)
    {
        var grid = new Grid { ColumnSpacing = ModsUi.Space(2), RowSpacing = ModsUi.Space(2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "FormLabel" });
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = new TextBlock { Text = rows[i].Label, MaxWidth = 220, TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            rows[i].Field.HorizontalAlignment = double.IsNaN(rows[i].Field.Width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            Grid.SetRow(label, i);
            Grid.SetRow(rows[i].Field, i);
            Grid.SetColumn(rows[i].Field, 1);
            grid.Children.Add(label);
            grid.Children.Add(rows[i].Field);
        }

        return grid;
    }

    /// <summary>A message line that takes no room, and leaves no gap in a stack, while it is empty.</summary>
    public static T HideWhenEmpty<T>(this T text) where T : TextBlock
    {
        text.IsVisible = !string.IsNullOrEmpty(text.Text);
        text.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty) text.IsVisible = !string.IsNullOrEmpty(text.Text);
        };
        return text;
    }

    /// <summary>Text that is cut with an ellipsis shows its full text on hover.</summary>
    public static T Ellipsis<T>(this T text) where T : TextBlock
    {
        text.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
        ToolTip.SetTip(text, text.Text);
        return text;
    }
}
