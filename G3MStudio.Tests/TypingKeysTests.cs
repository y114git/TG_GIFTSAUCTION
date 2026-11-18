using G3MStudio.Commands;
using Avalonia.Input;
using AvaloniaEdit;

namespace G3MStudio.Tests;

public sealed class TypingKeysTests
{
    [Fact]
    public async Task PlainKeysInTheCodeEditorOrABoxAreTypingNotImageToolShortcuts()
    {
        await HeadlessApp.Run(() =>
        {
            var editor = new TextEditor();
            Assert.True(MainWindow.TypesText(editor.TextArea, Key.B, KeyModifiers.None));
            Assert.True(MainWindow.TypesText(editor.TextArea, Key.E, KeyModifiers.Shift));
            Assert.True(MainWindow.TypesText(new Avalonia.Controls.TextBox(), Key.M, KeyModifiers.None));
            Assert.False(MainWindow.TypesText(editor.TextArea, Key.B, Shortcuts.Command)); // Ctrl combinations stay shortcuts
            Assert.True(MainWindow.TypesText(editor.TextArea, Key.Z, Shortcuts.Command)); // undo, redo and the clipboard keys edit the text
            Assert.True(MainWindow.TypesText(new Avalonia.Controls.TextBox(), Key.Y, Shortcuts.Command));
            Assert.False(MainWindow.TypesText(new Avalonia.Controls.Canvas(), Key.Z, Shortcuts.Command));
            Assert.False(MainWindow.TypesText(editor.TextArea, Key.F5, KeyModifiers.None));
            Assert.False(MainWindow.TypesText(editor.TextArea, Key.Escape, KeyModifiers.None));
            Assert.False(MainWindow.TypesText(new Avalonia.Controls.Canvas(), Key.B, KeyModifiers.None)); // outside a text field the tool key still works
            return Task.CompletedTask;
        });
    }
}
