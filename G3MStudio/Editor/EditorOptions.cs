using AvaloniaEdit;

namespace G3MStudio.Editor;

/// <summary>The code editor options of the Settings page. Every open code view follows a change at once.</summary>
public static class EditorOptions
{
    public static int FontSize { get; private set; } = 13;

    public static bool WordWrap { get; private set; }

    public static bool LineNumbers { get; private set; } = true;

    public static event Action? Changed;

    public static void Set(int fontSize, bool wordWrap, bool lineNumbers)
    {
        if ((fontSize, wordWrap, lineNumbers) == (FontSize, WordWrap, LineNumbers)) return;
        (FontSize, WordWrap, LineNumbers) = (fontSize, wordWrap, lineNumbers);
        Changed?.Invoke();
    }

    public static void ApplyTo(TextEditor editor)
    {
        editor.FontSize = FontSize;
        editor.WordWrap = WordWrap;
        editor.ShowLineNumbers = LineNumbers;
        editor.Options.HighlightCurrentLine = true;
    }
}
