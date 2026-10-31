using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using System.Text.RegularExpressions;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Panels;

/// <summary>Find in code: a query, two options and the matching lines. A double click asks the window to open the entry at the line.</summary>
public sealed class SearchPanel : UserControl
{
    private readonly TextBox _query = new TextBox { Name = "SearchQuery" }.Tr(TextBox.PlaceholderTextProperty, "search.placeholder");
    private readonly CheckBox _case = new CheckBox { Name = "SearchCase" }.Tr(ContentControl.ContentProperty, "search.case");
    private readonly CheckBox _regex = new CheckBox { Name = "SearchRegex" }.Tr(ContentControl.ContentProperty, "search.regex");
    private readonly Button _run = new Button { Name = "SearchRun" }.Tr(ContentControl.ContentProperty, "search.run");
    private readonly Button _cancel = new Button { Name = "SearchCancel", IsVisible = false }.Tr(ContentControl.ContentProperty, "explorer.cancel");
    private readonly TextBlock _status = new() { Name = "SearchStatus", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ListBox _results = new() { Name = "SearchResults" };
    private Workspace? _workspace;
    private Action? _cancelRunning;

    public SearchPanel()
    {
        _results.ItemTemplate = new FuncDataTemplate<CodeHit>((hit, _) => new TextBlock { Text = hit is null ? "" : $"{hit.Entry}({hit.Line})  {hit.Text}", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        _results.DoubleTapped += (_, _) => { if (_results.SelectedItem is CodeHit hit) OpenRequested?.Invoke(hit); };
        _run.Click += async (_, _) => await RunAsync();
        _query.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) await RunAsync(); };
        _cancel.Click += (_, _) => _cancelRunning?.Invoke();
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _case, _regex, _run, _cancel } };
        var layout = new DockPanel { Margin = new Thickness(8) };
        foreach (var (control, side) in new (Control, Avalonia.Controls.Dock)[] { (_query, Avalonia.Controls.Dock.Top), (options, Avalonia.Controls.Dock.Top), (_status, Avalonia.Controls.Dock.Top) })
        {
            DockPanel.SetDock(control, side);
            layout.Children.Add(control);
        }

        layout.Children.Add(_results);
        Content = layout;
    }

    public IReadOnlyList<CodeHit> Hits => [.. _results.Items.OfType<CodeHit>()];

    public string Status => _status.Text ?? "";

    public event Action<CodeHit>? OpenRequested;

    public void Bind(Workspace? workspace)
    {
        _cancelRunning?.Invoke();
        _workspace = workspace;
        _results.ItemsSource = null;
        _status.Text = "";
    }

    /// <summary>Asked before a search runs; false stops it. The window uses it to apply the text typed in code editors first, as Save does.</summary>
    public Func<bool>? BeforeRun { get; set; }

    /// <summary>Runs the search with the typed text and options; a search that is running is cancelled first.</summary>
    public async Task RunAsync()
    {
        if (_workspace is not { } workspace || string.IsNullOrEmpty(_query.Text) || BeforeRun?.Invoke() == false) return;
        _cancelRunning?.Invoke();
        using var source = new CancellationTokenSource();
        Action cancel = source.Cancel;
        _cancelRunning = cancel;
        // Only the newest search may touch the panel; an older one that finishes late must not overwrite it.
        bool Current() => _cancelRunning == cancel;
        _cancel.IsVisible = true;
        _results.ItemsSource = null;
        try
        {
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                if (Current()) _status.Text = Loc.Current.Format("search.progress", ("done", p.Done), ("total", p.Total));
            });
            var result = await CodeSearch.RunAsync(workspace, new CodeSearchOptions(_query.Text, _case.IsChecked == true, _regex.IsChecked == true), progress, source.Token);
            if (!Current() || source.IsCancellationRequested) return;
            _results.ItemsSource = result.Hits;
            _status.Text = Loc.Current.Format("search.done", ("hits", result.Hits.Count), ("entries", result.Searched))
                + (result.Failed > 0 ? " " + Loc.Current.Format("search.failed", ("count", result.Failed)) : "");
        }
        catch (Exception e) when (e is OperationCanceledException or ArgumentException or RegexMatchTimeoutException && Current())
        {
            _status.Text = e switch
            {
                OperationCanceledException => Loc.Current["search.cancelled"],
                RegexMatchTimeoutException => Loc.Current["search.timeout"],
                _ => Loc.Current.Format("search.badPattern", ("reason", e.Message)), // a pattern that does not parse
            };
        }
        finally
        {
            if (Current())
            {
                _cancelRunning = null;
                _cancel.IsVisible = false;
            }
        }
    }
}
