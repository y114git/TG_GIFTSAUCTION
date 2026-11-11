using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit.Search;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// A text file of the Included Files folder in the code editor control (colours, line numbers, find and replace, its own undo). Apply hands the text to
/// <see cref="ApplyRequested"/>, which writes it through one undoable command; Revert goes back to the text last applied.
/// </summary>
public sealed class TextFileView : DockPanel
{
    private readonly AvaloniaEdit.TextEditor _editor;
    private readonly Button _apply = new() { Name = "TextApply", Content = Loc.Current["code.apply"] };
    private readonly Button _revert = new() { Name = "TextRevert", Content = Loc.Current["code.revert"] };
    private readonly SearchPanel _search;
    private string _applied;

    public TextFileView(string text)
    {
        _applied = text;
        Name = "TextFileView";
        _editor = GmlColorizer.CreateEditor(text, false, out var colorizer);
        _editor.Name = "TextFile";
        _search = SearchPanel.Install(_editor);
        _search.IsReplaceMode = true;
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchPanelTexts.Apply(_search), DispatcherPriority.Loaded);
        _editor.TextChanged += (_, _) =>
        {
            colorizer.Tokenize(_editor.Text);
            _editor.TextArea.TextView.Redraw();
            Changed?.Invoke();
        };
        _apply.Click += (_, _) => ApplyRequested?.Invoke(_editor.Text);
        _revert.Click += (_, _) => _editor.Text = _applied;
        var bar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 4), Children = { _apply, _revert } };
        SetDock(bar, Avalonia.Controls.Dock.Top);
        Children.Add(bar);
        Children.Add(_editor);
    }

    /// <summary>Apply was pressed with the text in the editor.</summary>
    public event Action<string>? ApplyRequested;

    public event Action? Changed;

    public string Text => _editor.Text;

    /// <summary>True while the text differs from what was last applied.</summary>
    public bool HasPending => _editor.Text != _applied;

    public void MarkApplied() => _applied = _editor.Text;

    /// <summary>Replaces the text (a test, or a change that came from elsewhere).</summary>
    public void SetText(string text) => _editor.Text = text;
}
