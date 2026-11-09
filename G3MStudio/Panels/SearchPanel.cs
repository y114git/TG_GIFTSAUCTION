using Avalonia.Input.Platform;
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
    private readonly CheckBox _word = new CheckBox { Name = "SearchWord" }.Tr(ContentControl.ContentProperty, "search.word");
    private readonly TextBox _replace = new TextBox { Name = "SearchReplace" }.Tr(TextBox.PlaceholderTextProperty, "search.replacePlaceholder");
    private readonly Button _preview = new Button { Name = "SearchPreview" }.Tr(ContentControl.ContentProperty, "search.preview");
    private readonly Button _replaceSelected = new Button { Name = "SearchReplaceSelected", IsVisible = false }.Tr(ContentControl.ContentProperty, "search.replaceSelected");
    private readonly HashSet<CodeHit> _unchecked = [];
    private CodeSearchOptions? _previewed;
    private readonly Button _run = new Button { Name = "SearchRun" }.Tr(ContentControl.ContentProperty, "search.run");
    private readonly Button _cancel = new Button { Name = "SearchCancel", IsVisible = false }.Tr(ContentControl.ContentProperty, "explorer.cancel");
    private readonly TextBlock _status = new() { Name = "SearchStatus", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ListBox _results = new() { Name = "SearchResults" };
    private Workspace? _workspace;
    private Action? _cancelRunning;

    public SearchPanel()
    {
        _results.ItemTemplate = new FuncDataTemplate<CodeHit>((hit, _) => Row(hit));
        _preview.Click += async (_, _) => await PreviewAsync();
        _replaceSelected.Click += async (_, _) => await ReplaceSelectedAsync();
        _results.DoubleTapped += (_, _) => { if (_results.SelectedItem is CodeHit hit) OpenRequested?.Invoke(hit); };
        _results.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && _results.SelectedItem is CodeHit hit) { e.Handled = true; OpenRequested?.Invoke(hit); } };
        // Open, Copy and Copy all (UTMT-SRCH-02); a hit opens as a tab of its own, never as a preview that the next click replaces.
        MenuItem Item(string key, Action run)
        {
            var item = new MenuItem().Tr(MenuItem.HeaderProperty, key);
            item.Click += (_, _) => run();
            return item;
        }

        string Line(CodeHit hit) => $"{hit.Entry}({hit.Line})  {hit.Text}";
        void Copy(string text) { if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) _ = clipboard.SetTextAsync(text); }
        _results.ContextMenu = new ContextMenu
        {
            ItemsSource = new[]
            {
                Item("search.open", () => { if (_results.SelectedItem is CodeHit hit) OpenRequested?.Invoke(hit); }),
                Item("search.copy", () => { if (_results.SelectedItem is CodeHit hit) Copy(Line(hit)); }),
                Item("search.copyAll", () => Copy(string.Join(Environment.NewLine, Hits.Select(Line)))),
            },
        };
        _run.Click += async (_, _) => await RunAsync();
        _query.KeyDown += async (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) await RunAsync(); };
        _cancel.Click += (_, _) => _cancelRunning?.Invoke();
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _case, _word, _regex, _run, _cancel } };
        var replaceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _preview, _replaceSelected } };
        var layout = new DockPanel { Margin = new Thickness(8) };
        foreach (var (control, side) in new (Control, Avalonia.Controls.Dock)[] { (_query, Avalonia.Controls.Dock.Top), (_replace, Avalonia.Controls.Dock.Top), (replaceRow, Avalonia.Controls.Dock.Top), (options, Avalonia.Controls.Dock.Top), (_status, Avalonia.Controls.Dock.Top) })
        {
            DockPanel.SetDock(control, side);
            layout.Children.Add(control);
        }

        layout.Children.Add(_results);
        Content = layout;
    }

    private Control Row(CodeHit? hit)
    {
        if (hit is null) return new TextBlock();
        var text = new TextBlock { Text = hit.Replaced is null ? $"{hit.Entry}({hit.Line})  {hit.Text}" : $"{hit.Entry}({hit.Line})  {hit.Text}  ->  {hit.Replaced}", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        if (hit.Replaced is null) return text;
        var box = new CheckBox { IsChecked = !_unchecked.Contains(hit) };
        box.IsCheckedChanged += (_, _) => { if (box.IsChecked == true) _unchecked.Remove(hit); else _unchecked.Add(hit); };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { box, text } };
    }

    /// <summary>Puts the cursor in the replace box (Ctrl+Shift+H).</summary>
    public void FocusReplace() => _replace.Focus();

    /// <summary>The previewed lines whose check box is on.</summary>
    public IReadOnlyList<CodeHit> Chosen => [.. Hits.Where(h => h.Replaced is not null && !_unchecked.Contains(h))];

    /// <summary>Unchecks a previewed line, as a click on its box does.</summary>
    public void Uncheck(CodeHit hit) => _unchecked.Add(hit);

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

    /// <summary>True while the program runs in low-memory mode; the search then decompiles one entry at a time.</summary>
    public Func<bool>? IsLowMemory { get; set; }

    /// <summary>Runs the search with the typed text and options; a search that is running is cancelled first.</summary>
    public Task RunAsync() => RunAsync(replace: false);

    /// <summary>Lists the lines the replacement would change, each with a check box; nothing is written until Replace Selected.</summary>
    public Task PreviewAsync() => RunAsync(replace: true);

    private async Task RunAsync(bool replace)
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
        _unchecked.Clear();
        _replaceSelected.IsVisible = false;
        _previewed = null;
        try
        {
            var progress = new Progress<(int Done, int Total)>(p =>
            {
                if (Current()) _status.Text = Loc.Current.Format("search.progress", ("done", p.Done), ("total", p.Total));
            });
            var options = new CodeSearchOptions(_query.Text, _case.IsChecked == true, _regex.IsChecked == true, IsLowMemory?.Invoke() == true, _word.IsChecked == true, replace ? _replace.Text ?? "" : null);
            var result = await CodeSearch.RunAsync(workspace, options, progress, source.Token);
            if (!Current() || source.IsCancellationRequested) return;
            _results.ItemsSource = result.Hits;
            if (replace)
            {
                _previewed = options;
                _replaceSelected.IsVisible = result.Hits.Count > 0;
            }

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

    /// <summary>Writes the checked lines of the preview as one undoable edit; entries that do not compile stay as they were and are named in the status.</summary>
    public async Task ReplaceSelectedAsync()
    {
        if (_workspace is not { } workspace || _previewed is not { } options || Chosen.Count == 0 || BeforeRun?.Invoke() == false) return;
        _replaceSelected.IsVisible = false;
        try
        {
            var result = await CodeReplace.ApplyAsync(workspace, options, Chosen);
            _results.ItemsSource = null;
            _unchecked.Clear();
            _previewed = null;
            _status.Text = Loc.Current.Format("search.replaced", ("lines", result.Lines), ("entries", result.Entries))
                + (result.Failed.Count > 0 ? " " + Loc.Current.Format("search.replaceFailed", ("entries", string.Join(", ", result.Failed))) : "");
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or RegexMatchTimeoutException)
        {
            _status.Text = Loc.Current.Format("search.badPattern", ("reason", e.Message));
            _replaceSelected.IsVisible = true;
        }
    }
}
