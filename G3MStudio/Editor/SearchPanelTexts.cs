using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The find and replace panel of the code editor comes from AvaloniaEdit with fixed English texts and no way to give it others. This puts the language of the
/// program on its check boxes, boxes and tooltips once it is on screen, matching each by its English text.
/// shortcut: the "n matches" counter is set by the panel on every search and stays English.
/// </summary>
public static class SearchPanelTexts
{
    private static readonly (string English, string Key)[] Texts =
    [
        ("Match case", "editorSearch.matchCase"),
        ("Match whole words", "editorSearch.wholeWords"),
        ("Use regular expressions", "editorSearch.regex"),
        ("Find next (F3)", "editorSearch.next"),
        ("Find previous (Shift+F3)", "editorSearch.previous"),
        ("Find...", "editorSearch.find"),
        ("Replace...", "editorSearch.replaceWith"),
        ("Replace next (Alt+R)", "editorSearch.replace"),
        ("Replace all (Alt+A)", "editorSearch.replaceAll"),
        ("Toggle between find and replace modes", "editorSearch.toggle"),
    ];

    // The key a control got on the first pass, so a later language change can translate it again from the key instead of from its English text.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, string> TipKeys = [];
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, string> ContentKeys = [];
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, string> HintKeys = [];

    /// <summary>
    /// Opens the find panel of an editor and puts the caret in its box, so what is typed right after the shortcut does not land in the text. The panel builds its
    /// parts when it first shows: the texts are put on then, and the focus is asked for again for a few frames while the editor still holds it (some platforms
    /// move the focus after the key press).
    /// </summary>
    public static void Show(AvaloniaEdit.Search.SearchPanel panel, Control owner)
    {
        panel.Open();
        panel.Reactivate();
        var tries = 0;
        void Place()
        {
            Apply(panel);
            panel.Reactivate();
            var focused = TopLevel.GetTopLevel(owner)?.FocusManager?.GetFocusedElement() as Control;
            if (focused?.FindAncestorOfType<AvaloniaEdit.Search.SearchPanel>(true) is null && ++tries < 10) Avalonia.Threading.Dispatcher.UIThread.Post(Place, Avalonia.Threading.DispatcherPriority.Background);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(Place, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    public static void Apply(Control panel)
    {
        foreach (var control in panel.GetVisualDescendants().OfType<Control>().Prepend(panel))
        {
            switch (ToolTip.GetTip(control))
            {
                case string tip when Key(TipKeys, control, tip) is { } tipKey:
                    ToolTip.SetTip(control, Loc.Current[tipKey]);
                    break;
                case TextBlock { Text: { } tipText } block when Key(TipKeys, block, tipText) is { } blockKey:
                    block.Text = Loc.Current[blockKey];
                    break;
            }

            switch (control)
            {
                case CheckBox { Content: string text } check when Key(ContentKeys, check, text) is { } key:
                    check.Content = Loc.Current[key];
                    break;
                case TextBox { PlaceholderText: { } hint } box when Key(HintKeys, box, hint) is { } key:
                    box.PlaceholderText = Loc.Current[key];
                    break;
            }
        }
    }

    /// <summary>The key of a control: remembered from an earlier pass, else found from its English text (and remembered).</summary>
    private static string? Key(System.Runtime.CompilerServices.ConditionalWeakTable<Control, string> known, Control control, string current)
    {
        if (known.TryGetValue(control, out var key)) return key;
        key = Find(current);
        if (key is not null) known.Add(control, key);
        return key;
    }

    /// <summary>The language key of an English panel text, or null.</summary>
    public static string? Find(string english) => Array.Find(Texts, t => t.English == english).Key;

    /// <summary>Every key this class can ask for.</summary>
    public static IEnumerable<string> Keys => Texts.Select(t => t.Key);
}
