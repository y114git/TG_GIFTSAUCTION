using Avalonia.Controls;
using AvaloniaEdit;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The VM assembly of one code entry with the editor's find panel. Built off the UI thread so a long entry does not freeze the window. Assemble (or Ctrl+K) puts the edited text back into the entry as one undo step (UTMT-CODE-05).</summary>
public sealed class DisassemblyWindow : Window
{
    private readonly TextEditor _text = new() { Name = "Disassembly", ShowLineNumbers = true, FontFamily = (Avalonia.Media.FontFamily?)Avalonia.Application.Current?.FindResource("Font.Code") ?? Avalonia.Media.FontFamily.Default, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

    public DisassemblyWindow(Workspace workspace, string entry)
    {
        Title = Loc.Current.Format("disassembly.title", ("entry", entry));
        Width = 820;
        Height = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AvaloniaEdit.Search.SearchPanel.Install(_text);
        _text.Text = Loc.Current["disassembly.loading"];
        _text.IsReadOnly = true;
        var assemble = new Button { Name = "Assemble", Content = Loc.Current["disassembly.assemble"] };
        void Assemble()
        {
            if (_text.IsReadOnly || _text.Text == _last) return;
            try
            {
                workspace.Edit(new EditAssemblyCommand(entry, _text.Text));
                _last = _text.Text;
                Message.Text = Loc.Current["disassembly.assembled"];
            }
            catch (InvalidOperationException e)
            {
                Message.Text = Loc.Current.Format("disassembly.assembleFailed", ("reason", e.Message));
            }
        }

        assemble.Click += (_, _) => Assemble();
        KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.K || e.KeyModifiers != Avalonia.Input.KeyModifiers.Control) return;
            Assemble();
            e.Handled = true;
        };
        var bar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Margin = new Avalonia.Thickness(8, 4), Children = { assemble, Message } };
        DockPanel.SetDock(bar, Avalonia.Controls.Dock.Top);
        Content = new DockPanel { Children = { bar, _text } };
        _ = Load(workspace, entry);
    }

    /// <summary>The result of the last Assemble.</summary>
    public TextBlock Message { get; } = new() { Name = "AssembleMessage", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

    private string _last = string.Empty;

    public string Text => _text.Text;

    private async Task Load(Workspace workspace, string entry)
    {
        try
        {
            _text.Text = await Task.Run(() =>
            {
                using (workspace.Lock.Read()) return CodeText.Disassemble(workspace.Data, entry);
            });
            _last = _text.Text;
            _text.IsReadOnly = false;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            _text.Text = Loc.Current.Format("disassembly.failed", ("reason", e.Message));
        }
    }
}
