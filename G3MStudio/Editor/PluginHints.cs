using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using G3MStudio.Core.Plugins;

namespace G3MStudio.Editor;

/// <summary>Where the code editors reach the plugin host. The window sets it once; a code tab asks it for plugin hints and actions.</summary>
public static class PluginHooks
{
    public static PluginHost? Host { get; set; }
}

/// <summary>
/// Draws the gray notes plugins add after lines of code. A note is a part of the view only: it is not in the text, so it is never saved, compiled,
/// searched or copied. The notes are read again a moment after typing stops and whenever the plugins change.
/// </summary>
internal sealed class PluginHints : VisualLineElementGenerator
{
    private readonly TextEditor _editor;
    private readonly Func<string> _entry;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private Dictionary<int, CodeHint> _byLine = [];
    private PluginHost? _host;
    private readonly Action _onChanged;

    private PluginHints(TextEditor editor, Func<string> entry)
    {
        (_editor, _entry) = (editor, entry);
        _onChanged = () => Dispatcher.UIThread.Post(Schedule);
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Refresh();
        };
        editor.TextChanged += (_, _) => Schedule();
        editor.AttachedToVisualTree += (_, _) =>
        {
            // The host holds the editor only while the editor is on screen, and the window clears the static host when it closes.
            _host = PluginHooks.Host;
            _host?.HintsChanged += _onChanged;
            Schedule();
        };
        editor.DetachedFromVisualTree += (_, _) =>
        {
            _host?.HintsChanged -= _onChanged;
            _host = null;
            _timer.Stop();
        };
    }

    public static void Install(TextEditor editor, Func<string> entry) => editor.TextArea.TextView.ElementGenerators.Add(new PluginHints(editor, entry));

    private void Schedule()
    {
        // With no plugin that adds notes there is nothing to wait for: no timer is started.
        if (_byLine.Count == 0 && PluginHooks.Host is not { HasHints: true }) return;
        _timer.Stop();
        _timer.Start();
    }

    private void Refresh()
    {
        var host = PluginHooks.Host;
        var hints = host is { Enabled: true } && _editor.Text.Length > 0 ? host.CollectHints(_entry(), _editor.Text) : [];
        _byLine = hints.GroupBy(h => h.Line).ToDictionary(g => g.Key, g => new CodeHint(g.Key, string.Join("   ", g.Select(h => h.Text)), g.Select(h => h.Tooltip).FirstOrDefault(t => t is not null)));
        _editor.TextArea.TextView.Redraw();
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (_byLine.Count == 0) return -1;
        var line = CurrentContext.Document.GetLineByOffset(startOffset);
        return _byLine.ContainsKey(line.LineNumber) && startOffset <= line.EndOffset ? line.EndOffset : -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var line = CurrentContext.Document.GetLineByOffset(offset);
        if (offset != line.EndOffset || !_byLine.TryGetValue(line.LineNumber, out var hint)) return null;
        var brush = Application.Current?.TryGetResource("Syntax.Comment", Application.Current.ActualThemeVariant, out var found) == true ? found as IBrush : null;
        var note = new TextBlock { Text = "  " + hint.Text, FontStyle = FontStyle.Italic, Foreground = brush, Opacity = 0.8, FontFamily = _editor.FontFamily, FontSize = _editor.FontSize, IsHitTestVisible = hint.Tooltip is not null };
        if (hint.Tooltip is not null) ToolTip.SetTip(note, hint.Tooltip);
        return new InlineObjectElement(0, note);
    }
}
