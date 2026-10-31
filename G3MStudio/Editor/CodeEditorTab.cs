using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// Minimal code entry editor (design/shell.md 9.2 item 10, 14.4 item 4): the decompiled GML in a highlighted AvaloniaEdit view.
/// Typing is local until Apply, which commits one <see cref="EditCodeCommand"/> to the workspace (undo, redo, journal).
/// shortcut: the text is decompiled once and highlighted by re-tokenizing the whole text 300 ms after a change; add incremental
/// tokens and diagnostics with spans when the full code editor comes in M2.
/// </summary>
public sealed class CodeEditorTab : DockPanel
{
    private readonly Workspace _workspace;
    private readonly GmlColorizer _colorizer;
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, Name = "Message" };
    private readonly Button _apply = new() { Name = "Apply" };
    private readonly Button _revert = new() { Name = "Revert" };
    private readonly DispatcherTimer _retokenize = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly SearchPanel _search;
    private readonly FoldingManager _folding;
    private string _applied = "";
    private bool _loading, _applying;
    private int _loadId;

    public CodeEditorTab(Workspace workspace, string entry)
    {
        _workspace = workspace;
        Entry = entry;
        Editor = GmlColorizer.CreateEditor("", readOnly: true, out _colorizer);
        Editor.Name = "Code";
        _colorizer.Classifier = GmlNames.For(workspace);
        _search = SearchPanel.Install(Editor);
        _folding = FoldingManager.Install(Editor.TextArea);
        _apply.Content = Loc.Current["code.apply"];
        _revert.Content = Loc.Current["code.revert"];
        _apply.Click += (_, _) => Apply();
        _revert.Click += (_, _) => Revert();
        var bar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 4), Children = { _apply, _revert, _message } };
        SetDock(bar, Avalonia.Controls.Dock.Top);
        Children.Add(bar);
        Children.Add(Editor);
        Editor.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _retokenize.Stop();
            _retokenize.Start();
            UpdateState();
        };
        _retokenize.Tick += (_, _) =>
        {
            _retokenize.Stop();
            _colorizer.Tokenize(Editor.Text);
            _folding.UpdateFoldings(_colorizer.Foldings, -1);
            Editor.TextArea.TextView.Redraw();
        };
        // Only a shown tab follows the model; a tab that was hidden meanwhile catches up when it is shown again.
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= OnHistoryChanged;
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += OnHistoryChanged;
            if (Ready.IsCompleted && !HasPending) Ready = LoadAsync();
        };
        Ready = LoadAsync();
    }

    public string Entry { get; }

    public TextEditor Editor { get; }

    public string Text => Editor.Text;

    /// <summary>Text typed here that is not in the model yet.</summary>
    public bool HasPending => !_loading && Editor.Text != _applied;

    /// <summary>The user typed: the tab stops being a preview tab and shows its modified mark.</summary>
    public event Action? Edited;

    public event Action? PendingChanged;

    /// <summary>True once the first decompile finished (or failed).</summary>
    public Task Ready { get; private set; } = Task.CompletedTask;

    /// <summary>Commits the typed text. False when it does not compile; the message shows why and nothing changed.</summary>
    public bool Apply()
    {
        if (!HasPending) return true;
        _applying = true;
        try
        {
            _workspace.Edit(new EditCodeCommand(Entry, Editor.Text));
            _applied = Editor.Text;
            _workspace.Problems.Clear(Entry);
            Show("", false);
            return true;
        }
        catch (InvalidOperationException e)
        {
            if (e is CodeCompileException compile) _workspace.Problems.Set(Entry, compile.Diagnostics);
            Show(e.Message, true);
            return false;
        }
        finally
        {
            _applying = false;
            UpdateState(false);
        }
    }

    /// <summary>Opens the search box of the editor (Ctrl+F).</summary>
    public void ShowFind() => _search.Open();

    /// <summary>Puts the caret on a one-based line and column and scrolls there (a double click in the Problems panel).</summary>
    public void GoTo(int line, int column)
    {
        var position = new TextViewPosition(Math.Clamp(line, 1, Editor.Document.LineCount), Math.Max(column, 1));
        Editor.TextArea.Caret.Position = position;
        Editor.ScrollToLine(position.Line);
    }

    /// <summary>Drops the typed text and shows the model's code again.</summary>
    public void Revert()
    {
        _workspace.Problems.Clear(Entry);
        Ready = LoadAsync();
    }

    private void OnHistoryChanged()
    {
        // An undo, redo or rename reached the model: show it, unless the user is in the middle of typing.
        if (!_applying && !HasPending) Ready = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var id = ++_loadId;
        _loading = true;
        Editor.IsReadOnly = true;
        Show(Loc.Current["code.decompiling"], false);
        try
        {
            var text = await Task.Run(() =>
            {
                using (_workspace.Lock.Read()) return CodeText.Decompile(_workspace.Data, Entry, _workspace.DecompileContext());
            });
            if (id != _loadId) return;
            Editor.Text = text;
            _applied = text;
            _colorizer.Tokenize(text);
            _folding.UpdateFoldings(_colorizer.Foldings, -1);
            Editor.TextArea.TextView.Redraw();
            Editor.IsReadOnly = false;
            Show("", false);
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            if (id == _loadId) Show(Loc.Current.Format("code.showFailed", ("entry", Entry), ("reason", e.Message)), true);
        }
        finally
        {
            if (id == _loadId) _loading = false;
            UpdateState(false);
        }
    }

    private void Show(string text, bool error)
    {
        _message.Text = text;
        _message.Foreground = error ? (IBrush?)Application.Current?.FindResource("Danger") : (IBrush?)Application.Current?.FindResource("Text.Muted");
    }

    private void UpdateState(bool typed = true)
    {
        _apply.IsEnabled = HasPending;
        _revert.IsEnabled = HasPending;
        if (typed && HasPending) Edited?.Invoke();
        PendingChanged?.Invoke();
    }
}
