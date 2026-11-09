using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Panels;

/// <summary>One line of the Output panel: a message of the app or a line of the game. A line that names a code entry carries its location.</summary>
public sealed record OutputRow(DateTimeOffset Time, string Text, GmlLocation? Location);

/// <summary>The Output panel: what the app told the user and what the running game wrote, newest last, with a filter, copy, clear and timestamps. A click on a line with a GML location opens that code line.</summary>
public sealed class OutputPanel : UserControl
{
    private readonly ListBox _list = new() { Name = "OutputList" };
    private readonly TextBox _filter = new TextBox { Name = "OutputFilter", MinWidth = 160 }.Tr(TextBox.PlaceholderTextProperty, "output.filter");
    private readonly CheckBox _times = new CheckBox { Name = "OutputTimestamps", IsChecked = true }.Tr(ContentControl.ContentProperty, "output.timestamps");
    private readonly Button _copy = new Button { Name = "OutputCopy" }.Tr(ContentControl.ContentProperty, "output.copy");
    private readonly Button _clear = new Button { Name = "OutputClear" }.Tr(ContentControl.ContentProperty, "output.clear");
    private readonly Button _restart = new Button { Name = "OutputRestart" }.Tr(ContentControl.ContentProperty, "output.restart");
    private readonly Button _stop = new Button { Name = "OutputStop" }.Tr(ContentControl.ContentProperty, "output.stop");
    private readonly TextBlock _empty = new() { Name = "OutputEmpty", Text = Loc.Current["empty.output"], Classes = { "muted" }, Margin = new Thickness(12), IsHitTestVisible = false };
    private NotificationCenter? _center;
    private RunLog? _log;
    private bool _pending;

    public OutputPanel()
    {
        _list.ItemTemplate = new FuncDataTemplate<OutputRow>((row, _) => new TextBlock
        {
            Text = row is null ? "" : Format(row),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextDecorations = row?.Location is null ? null : Avalonia.Media.TextDecorations.Underline,
        });
        _list.Tapped += (_, _) => Open(_list.SelectedItem as OutputRow);
        _filter.TextChanged += (_, _) => Refresh();
        _times.IsCheckedChanged += (_, _) => Refresh();
        _copy.Click += async (_, _) => await CopyAsync();
        _clear.Click += (_, _) => Clear();
        _restart.Click += (_, _) => RestartRequested?.Invoke();
        _stop.Click += (_, _) => StopRequested?.Invoke();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8), Children = { _filter, _times, _copy, _clear, _restart, _stop } };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        Content = new DockPanel { Children = { bar, new Grid { Children = { _list, _empty } } } };
    }

    /// <summary>The rows shown now, after the filter.</summary>
    public IReadOnlyList<OutputRow> Rows => [.. _list.Items.OfType<OutputRow>()];

    public event Action<GmlLocation>? LocationRequested;

    public event Action? RestartRequested;

    public event Action? StopRequested;

    public void SetFilter(string text)
    {
        _filter.Text = text;
        Refresh();
    }

    public void Open(OutputRow? row)
    {
        if (row?.Location is { } location) LocationRequested?.Invoke(location);
    }

    public void Bind(NotificationCenter center, RunLog log)
    {
        _center = center;
        _log = log;
        center.Changed += Refresh;
        // The game writes from other threads; one refresh is posted for any number of lines.
        log.Changed += () =>
        {
            if (_pending) return;
            _pending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _pending = false;
                Refresh();
            });
        };
        Refresh();
    }

    public void Clear()
    {
        _log?.Clear();
        _center?.ClearHistory();
    }

    /// <summary>The visible rows as text, one per line.</summary>
    public string VisibleText() => string.Join('\n', Rows.Select(Format));

    private string Format(OutputRow row) => (_times.IsChecked == true ? row.Time.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " " : "") + row.Text;

    private async Task CopyAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(VisibleText());
    }

    private void Refresh()
    {
        var rows = (_center?.History ?? []).Select(n => new OutputRow(n.Time, $"{n.Level}: {n.Text}", null))
            .Concat((_log?.Lines ?? []).Select(l => new OutputRow(l.Time, l.Error ? "! " + l.Text : l.Text, GmlLocation.Find(l.Text))))
            .OrderBy(r => r.Time);
        var filter = _filter.Text;
        List<OutputRow> shown = [.. rows.Where(r => string.IsNullOrEmpty(filter) || r.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))];
        _list.ItemsSource = shown;
        _empty.IsVisible = _list.ItemCount == 0;
        _empty.Text = Loc.Current["empty.output"];
        if (_list.ItemCount > 0) _list.ScrollIntoView(_list.ItemCount - 1);
    }
}
