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

    /// <summary>Thin vertical lines at each indent level.</summary>
    public static bool IndentGuides { get; private set; } = true;

    /// <summary>Columns one tab stands for and one indent step uses; 2 to 8.</summary>
    public static int TabWidth { get; private set; } = 4;

    /// <summary>Brackets take a color by nesting level.</summary>
    public static bool BracketColors { get; private set; } = true;

    /// <summary>Font ligatures of the code font (-> as one glyph and the like). Off by default; code and diff views follow it.</summary>
    public static bool Ligatures { get; private set; }

    /// <summary>Squiggles for syntax problems appear while typing, after a short pause.</summary>
    public static bool LiveCheck { get; private set; } = true;

    /// <summary>Entries longer than this many characters are checked when applied, not while typing.</summary>
    public const int LiveCheckMaxChars = 200_000;

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

    public static void Set(int fontSize, bool wordWrap, bool lineNumbers, FontFamily? family = null, bool autoComplete = true, bool indentGuides = true)
    {
        if ((fontSize, wordWrap, lineNumbers, family, autoComplete, indentGuides) == (FontSize, WordWrap, LineNumbers, Family, AutoComplete, IndentGuides)) return;
        (FontSize, WordWrap, LineNumbers, Family, AutoComplete, IndentGuides) = (fontSize, wordWrap, lineNumbers, family, autoComplete, indentGuides);
        Changed?.Invoke();
    }

    /// <summary>The tab width (2 to 8 columns) and the bracket colors.</summary>
    public static void SetLayout(int tabWidth, bool bracketColors, bool ligatures = false, bool liveCheck = true)
    {
        tabWidth = Math.Clamp(tabWidth, 2, 8);
        if ((tabWidth, bracketColors, ligatures, liveCheck) == (TabWidth, BracketColors, Ligatures, LiveCheck)) return;
        (TabWidth, BracketColors, Ligatures, LiveCheck) = (tabWidth, bracketColors, ligatures, liveCheck);
        Changed?.Invoke();
    }

    public static void ApplyTo(TextEditor editor, FontFamily themeFont)
    {
        editor.FontFamily = Family ?? themeFont;
        editor.FontSize = FontSize;
        editor.WordWrap = WordWrap;
        editor.ShowLineNumbers = LineNumbers;
        editor.Options.HighlightCurrentLine = true;
        editor.Options.IndentationSize = TabWidth;
        editor.TextArea.TextView.CurrentLineBackground = G3MStudio.Theme.CanvasTheme.Soft(G3MStudio.Theme.CanvasTheme.Selection, 0.25);
    }
}
