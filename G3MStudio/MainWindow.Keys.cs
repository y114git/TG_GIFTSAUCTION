using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using G3MStudio.Commands;

namespace G3MStudio;

// Window-level shortcut handling.
public sealed partial class MainWindow
{
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Settings.ShortcutsEditor.Capturing) return; // the Settings page is waiting for keys to assign
        if (_blocked)
        {
            // A script holds the data: no shortcut may touch it, and Escape stops the script.
            if (e.Key == Key.Escape) _exporting?.Cancel();
            e.Handled = true;
            return;
        }

        // With the focus inside the image editor its own keys come first (undo, redo, apply, copy and the rest act on the picture, not on the data file).
        if (e.Source is Avalonia.Visual inside && inside.FindAncestorOfType<G3MStudio.Editor.ImageEditorView>(true) is { } image && image.HandleKey(e))
        {
            e.Handled = true;
            return;
        }

        // A command that does not apply here (an image command on a room) leaves the key to the focused control, which may have its own use of it.
        if (Shortcuts.Match(Registry, e) is { } command && Registry.IsEnabled(command) && !TypesText(e.Source, e.Key, e.KeyModifiers) && !(command.Id.StartsWith("img.", StringComparison.Ordinal) && e.Source is TextBox or NumericUpDown))
        {
            // Typed changes in a code, shader or text file editor are saved first; with none the key saves the data file.
            var from = e.Source as Avalonia.Visual;
            // In an image editor the key does one thing: a picture waiting for Apply (this tab's was taken by the view above, another tab's here) is applied, and only with none the file is saved.
            if (command.Id == "file.save" && from?.FindAncestorOfType<G3MStudio.Editor.ImageEditorView>() is not null && _pictureSessions.Values.Any(p => p.Session.Dirty)) _ = ApplyDirtyPicturesAsync();
            else if (command.Id == "file.save" && from?.FindAncestorOfType<G3MStudio.Editor.CodeEditorTab>() is { HasPending: true } code) code.Apply();
            else if (command.Id == "file.save" && from?.FindAncestorOfType<G3MStudio.Editor.ShaderSourceView>()?.SaveAll() == true) { }
            else if (command.Id == "file.save" && from?.FindAncestorOfType<G3MStudio.Editor.TextFileView>() is { HasPending: true } text) text.Apply();
            else Registry.Execute(command.Id);
            e.Handled = true;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Settings.ShortcutsEditor.Capturing || _blocked) return;
        if (MouseGestures.Match(Registry, e) is not { } command || !Registry.IsEnabled(command)) return;
        Registry.Execute(command.Id);
        e.Handled = true;
    }

    /// <summary>
    /// A plain letter, digit or punctuation key in a text box or the code editor is typing, not a shortcut: the single-key tool shortcuts of the image
    /// editor (B, E, M and the rest) must not swallow it. Function keys, Escape and anything with Ctrl, Alt or the Windows key stay shortcuts.
    /// </summary>
    internal static bool TypesText(object? source, Key key, KeyModifiers modifiers) =>
        source is TextBox or NumericUpDown or AvaloniaEdit.Editing.TextArea && (modifiers is KeyModifiers.None or KeyModifiers.Shift && key is not (>= Key.F1 and <= Key.F24 or Key.Escape) || EditsText(key, modifiers));

    /// <summary>Undo, redo, cut, copy, paste and select all belong to the focused text: the window must not turn them into file-wide commands.</summary>
    private static bool EditsText(Key key, KeyModifiers modifiers) =>
        modifiers == Shortcuts.Command && key is Key.Z or Key.Y or Key.X or Key.C or Key.V or Key.A or Key.Insert
        || modifiers == (Shortcuts.Command | KeyModifiers.Shift) && key == Key.Z
        || modifiers == KeyModifiers.Shift && key is Key.Insert or Key.Delete;
}
