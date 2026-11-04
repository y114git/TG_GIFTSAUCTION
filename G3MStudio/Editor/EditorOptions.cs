using Avalonia.Media;
using AvaloniaEdit;

namespace G3MStudio.Editor;

/// <summary>The code editor options of the Settings page. Every open code view follows a change at once.</summary>
public static class EditorOptions
{
    public static int FontSize { get; private set; } = 13;

    public static bool WordWrap { get; private set; }

    public static bool LineNumbers { get; private set; } = true;

    /// <summary>The completion list opens while typing; Ctrl+Space opens it either way.</summary>
    public static bool AutoComplete { get; private set; } = true;

    /// <summary>The font the user chose for code, or null for the theme's code font.</summary>
    public static FontFamily? Family { get; private set; }

    public static event Action? Changed;

    /// <summary>The smallest and largest code font size, as in Settings.</summary>
    public const int MinFontSize = 8, MaxFontSize = 32;

    /// <summary>Asked for by Ctrl + mouse wheel in a code view; the window turns it into the setting, so the size also carries to the next editor.</summary>
    public static event Action<int>? ZoomRequested;

    public static void Zoom(int steps) => ZoomRequested?.Invoke(Math.Clamp(FontSize + steps, MinFontSize, MaxFontSize));

    /// <summary>Asks every open code view to apply the options again, after a theme color changed.</summary>
    public static void Refresh() => Changed?.Invoke();

    public static void Set(int fontSize, bool wordWrap, bool lineNumbers, FontFamily? family = null, bool autoComplete = true)
    {
        if ((fontSize, wordWrap, lineNumbers, family, autoComplete) == (FontSize, WordWrap, LineNumbers, Family, AutoComplete)) return;
        (FontSize, WordWrap, LineNumbers, Family, AutoComplete) = (fontSize, wordWrap, lineNumbers, family, autoComplete);
        Changed?.Invoke();
    }

    public static void ApplyTo(TextEditor editor, FontFamily themeFont)
    {
        editor.FontFamily = Family ?? themeFont;
        editor.FontSize = FontSize;
        editor.WordWrap = WordWrap;
        editor.ShowLineNumbers = LineNumbers;
        editor.Options.HighlightCurrentLine = true;
        editor.TextArea.TextView.CurrentLineBackground = G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Selection, 0.25);
    }
}
