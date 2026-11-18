using G3MStudio.Core.ModPack;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// Changes against the code as it was when the tab opened (UTMT shows the original beside the modded code).
public sealed partial class CodeEditorTab
{
    private readonly TextEditor _diffText = new() { Name = "DiffText", IsReadOnly = true, ShowLineNumbers = false, FontFamily = (FontFamily?)Application.Current?.FindResource("Font.Code") ?? FontFamily.Default };
    private readonly TextBlock _diffTitle = new() { Name = "DiffTitle", Classes = { "muted" } };
    private DockPanel? _diffPanel;
    private string? _original;
    private int _diffRun;

    /// <summary>The lines of the diff on show (empty while the panel is closed), for tests.</summary>
    public IReadOnlyList<DiffLine> DiffShown { get; private set; } = [];

    public bool DiffVisible => _diffPanel?.IsVisible == true;

    private DockPanel DiffPanel()
    {
        if (_diffPanel is not null) return _diffPanel;
        _diffText.TextArea.TextView.LineTransformers.Add(new DiffColorizer());
        _diffText.TextArea.TextView.ElementGenerators.Add(new NoLigatureGenerator());
        void Restyle() => _diffText.TextArea.TextView.Redraw();
        // The panel is built on first use, usually while the tab is already shown: subscribe now, not on the next attach.
        if (TopLevel.GetTopLevel(this) is not null) EditorOptions.Changed += Restyle;
        AttachedToVisualTree += (_, _) => EditorOptions.Changed += Restyle;
        DetachedFromVisualTree += (_, _) => EditorOptions.Changed -= Restyle;
        _diffText.Height = 200;
        _diffPanel = new DockPanel { Name = "DiffPanel", IsVisible = false, Children = { _diffTitle, _diffText } };
        DockPanel.SetDock(_diffTitle, Avalonia.Controls.Dock.Top);
        DockPanel.SetDock(_diffPanel, Avalonia.Controls.Dock.Bottom);
        return _diffPanel;
    }

    private void ToggleDiff()
    {
        var panel = DiffPanel();
        panel.IsVisible = !panel.IsVisible;
        if (panel.IsVisible) RefreshDiff();
        else DiffShown = [];
    }

    private void RefreshDiff()
    {
        if (_diffPanel is not { IsVisible: true } || _original is null) return;
        var (original, current, run) = (_original, Editor.Text, ++_diffRun);
        _ = ComputeDiffAsync(original, current, run);
    }

    private async Task ComputeDiffAsync(string original, string current, int run)
    {
        var lines = await Task.Run(() => LineDiff.Compute(original, current));
        if (run != _diffRun) return;
        DiffShown = lines;
        _diffText.Text = string.Join('\n', lines.Select(l => (l.Kind == DiffKind.Added ? "+ " : l.Kind == DiffKind.Removed ? "- " : "  ") + l.Text));
        _diffTitle.Text = Loc.Current.Format("code.diff.title", ("added", lines.Count(l => l.Kind == DiffKind.Added)), ("removed", lines.Count(l => l.Kind == DiffKind.Removed)));
    }

    /// <summary>Gives a line of the diff text a green or red background from its first character.</summary>
    private sealed class DiffColorizer : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            if (line.Length == 0) return;
            var first = CurrentContext.Document.GetCharAt(line.Offset);
            if (first is not ('+' or '-')) return;
            var key = first == '+' ? "Success" : "Danger";
            var color = (Application.Current?.FindResource(key) as ISolidColorBrush)?.Color ?? (first == '+' ? Colors.Green : Colors.Red);
            var brush = new SolidColorBrush(color, 0.25);
            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetBackgroundBrush(brush));
        }
    }
}
