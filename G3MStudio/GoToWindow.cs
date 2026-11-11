using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using G3MStudio.Core.Navigation;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Enter opens the entry where it belongs, Shift+Enter opens it in a split beside the tab in front.</summary>
public enum GoToMode { Open, Beside }

/// <summary>
/// Go To (Ctrl+T): one box over resources, code, strings, functions, variables, commands, settings pages and open tabs.
/// Prefixes narrow it (see <see cref="GoToQuery"/>); the right side previews the selected entry.
/// </summary>
public sealed class GoToWindow : Window
{
    private readonly Func<string, IReadOnlyList<GoToEntry>> _search;
    private readonly Func<GoToEntry, Task<string>> _preview;
    private readonly TextBox _input = new() { Name = "Input", PlaceholderText = Loc.Current["quick.goTo.placeholder"] };
    private readonly ListBox _results = new() { Name = "Results", MinHeight = 280, MaxHeight = 360 };
    private readonly TextBlock _empty = new() { Name = "Empty", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _previewText = new() { Name = "Preview", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly DockPanel _left;
    private readonly ScrollViewer _previewScroll;
    private readonly Image _previewPicture = new() { Name = "PreviewPicture", MaxWidth = 128, MaxHeight = 128, Stretch = Avalonia.Media.Stretch.Uniform, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, IsVisible = false, Margin = new Thickness(0, 0, 0, 6) };
    private int _previewRun;

    /// <summary>The picture of a resource entry, shown above its preview text; null when there is no picture to show.</summary>
    public Func<GoToEntry, Task<Avalonia.Media.IImage?>>? Picture { get; init; }

    public GoToWindow(Func<string, IReadOnlyList<GoToEntry>> search, Func<GoToEntry, Task<string>> preview)
    {
        (_search, _preview) = (search, preview);
        Title = Loc.Current["quick.goTo.title"];
        Width = 880;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _results.ItemTemplate = new FuncDataTemplate<GoToEntry>((entry, _) => entry is null ? new TextBlock() : Row(entry));
        var gap = Space("Space.2");
        _left = new DockPanel { Children = { Docked(_empty, Avalonia.Controls.Dock.Bottom, gap), _results } };
        var right = _previewScroll = new ScrollViewer { Name = "PreviewScroll", Content = new StackPanel { Children = { _previewPicture, _previewText } }, MinHeight = 280, MaxHeight = 360 };
        var grid = new Grid { ColumnSpacing = gap, ColumnDefinitions = { new ColumnDefinition(1.2, GridUnitType.Star), new ColumnDefinition(1, GridUnitType.Star) } };
        grid.Children.Add(_left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        _previewText.FontFamily = Editor.EditorOptions.Family ?? (Avalonia.Media.FontFamily?)Application.Current?.FindResource("Font.Code") ?? Avalonia.Media.FontFamily.Default;
        Avalonia.Automation.AutomationProperties.SetName(_input, Title);
        Avalonia.Automation.AutomationProperties.SetName(_results, Loc.Current["quick.goTo.results"]);
        Avalonia.Automation.AutomationProperties.SetName(right, Loc.Current["quick.goTo.preview"]);
        ToolTip.SetTip(_input, _input.PlaceholderText); // the hint is longer than the box in some languages
        var body = new DockPanel { Margin = new Thickness(gap), Children = { Docked(_input, Avalonia.Controls.Dock.Top, gap), grid } };
        Content = new ScrollViewer { Name = "GoToScroll", Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        _input.TextChanged += (_, _) => Refresh();
        _results.SelectionChanged += (_, _) => _ = ShowPreviewAsync();
        // On the window, so the keys work after a click or Tab moved the focus into the list; Escape is closed by DialogSizing.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        _results.DoubleTapped += (_, _) => Choose(GoToMode.Open);
        Opened += (_, _) =>
        {
            _input.Focus();
        };
        Refresh();
    }

    public IReadOnlyList<GoToEntry> Items => [.. _results.Items.OfType<GoToEntry>()];

    public string Text
    {
        get => _input.Text ?? "";
        set
        {
            _input.Text = value;
            _input.CaretIndex = value.Length;
        }
    }

    public event Action<GoToEntry, GoToMode>? Chosen;

    public string PreviewText => _previewText.Text ?? "";

    /// <summary>The preview of the entry selected now; completes when it is shown.</summary>
    public Task PreviewShown { get; private set; } = Task.CompletedTask;

    private static double Space(string key) => Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is double d ? d : 8;

    private static Control Docked(Control control, Avalonia.Controls.Dock side, double gap)
    {
        control.Margin = side == Avalonia.Controls.Dock.Top ? new Thickness(0, 0, 0, gap) : new Thickness(0, gap, 0, 0);
        DockPanel.SetDock(control, side);
        return control;
    }

    /// <summary>A short mark and the kind name on the right, so a script and its code of one name differ at a glance.</summary>
    private static DockPanel Row(GoToEntry entry)
    {
        var kind = KindName(entry);
        var detail = entry.Detail.Length > 0 ? $"{kind}  {entry.Detail}" : kind;
        var row = new DockPanel
        {
            Children =
            {
                new TextBlock { Text = detail, Classes = { "muted" }, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, MaxWidth = 200 },
                new TextBlock { Text = entry.Kind is GoToKind.Resource or GoToKind.Code ? Badge(entry.Type) : "", Width = 28, FontWeight = Avalonia.Media.FontWeight.Bold, Classes = { "muted" }, [DockPanel.DockProperty] = Avalonia.Controls.Dock.Left },
                new TextBlock { Text = entry.Name, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            },
        };
        ToolTip.SetTip(row, Tip(entry));
        return row;
    }

    /// <summary>The hover text of a row: the kind (the two-letter mark is spelled out) and the whole description, which the row cuts short.</summary>
    internal static string Tip(GoToEntry entry)
    {
        var kind = KindName(entry);
        return entry.Detail.Length > 0 ? $"{kind}\n{entry.Detail}" : kind;
    }

    private static string KindName(GoToEntry entry) => entry.Kind == GoToKind.Resource ? Loc.Current["explorer.type." + entry.Type] : Loc.Current[KindKey(entry.Kind)];

    /// <summary>A short mark in front of a row, so a script and the code of the same name differ at a glance and not by the right-hand label alone.</summary>
    public static string Badge(string type) => type switch
    {
        "code" => "{ }",
        "scripts" => "fx",
        "objects" => "ob",
        "sprites" => "sp",
        "rooms" => "rm",
        "sounds" => "sn",
        _ => type.Length > 1 ? type[..2] : type,
    };

    private static string KindKey(GoToKind kind) => kind switch
    {
        GoToKind.Code => "quick.kind.code",
        GoToKind.Text => "quick.kind.text",
        GoToKind.Function => "quick.kind.function",
        GoToKind.Variable => "quick.kind.variable",
        GoToKind.Command => "quick.kind.command",
        GoToKind.Setting => "quick.kind.setting",
        GoToKind.Tab => "quick.kind.tab",
        GoToKind.Reference => "quick.kind.reference",
        _ => "quick.kind.resource",
    };

    private void Refresh()
    {
        var text = _input.Text ?? "";
        var items = _search(text);
        _results.ItemsSource = items;
        _results.SelectedIndex = items.Count > 0 ? 0 : -1;
        var prefixes = Loc.Current["quick.goTo.prefixes"];
        var none = items.Count == 0 && text.Trim().Length > 0;
        _empty.IsVisible = none || text.Trim().Length == 0;
        // With nothing to list, the hint takes the whole width instead of a bare list and half a column.
        var bare = items.Count == 0;
        _results.IsVisible = !bare;
        _previewScroll.IsVisible = !bare;
        Grid.SetColumnSpan(_left, bare ? 2 : 1);
        _empty.Text = none ? Loc.Current.Format("palette.empty", ("text", text)) + "\n" + prefixes : prefixes;
        if (items.Count == 0) _previewText.Text = "";
    }

    private async Task ShowPreviewAsync()
    {
        if (_results.SelectedItem is not GoToEntry entry) return;
        var run = ++_previewRun;
        PreviewShown = ShowAsync(entry, run);
        await PreviewShown;
    }

    private async Task ShowAsync(GoToEntry entry, int run)
    {
        string text;
        _previewPicture.IsVisible = false;
        _previewPicture.Source = null;
        try
        {
            text = await _preview(entry);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or ArgumentException or IOException)
        {
            text = e.Message;
        }

        // A slower preview of an earlier selection must not overwrite the one of the current selection.
        if (run != _previewRun) return;
        _previewText.Text = text;
        if (Picture is null || entry.Kind != GoToKind.Resource) return;
        var picture = await Picture(entry);
        if (run != _previewRun || picture is null) return;
        (_previewPicture.Source, _previewPicture.IsVisible) = (picture, true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Move(1); break;
            case Key.Up: Move(-1); break;
            case Key.Enter: Choose(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? GoToMode.Beside : GoToMode.Open); break;
            case Key.Back when !_input.IsFocused && Text.Length > 0:
                Text = Text[..^1];
                _input.Focus();
                break;
            default: return;
        }

        e.Handled = true;
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_input.IsFocused || string.IsNullOrEmpty(e.Text)) return;
        Text += e.Text;
        _input.Focus();
        e.Handled = true;
    }

    private void Move(int step)
    {
        var count = _results.ItemCount;
        if (count > 0) _results.SelectedIndex = (_results.SelectedIndex + step + count) % count;
    }

    /// <summary>Picks the selected entry as Enter (or Shift+Enter) does; tests call it.</summary>
    public void Choose(GoToMode mode)
    {
        if (_results.SelectedItem is not GoToEntry entry) return;
        Close();
        Chosen?.Invoke(entry, mode);
    }
}
