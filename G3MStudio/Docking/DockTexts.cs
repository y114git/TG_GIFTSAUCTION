using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Localization;
using G3MStudio.Localization;

namespace G3MStudio.Docking;

/// <summary>
/// The menus of the docking library (tab right-click, tool panel menu, drag preview) take their texts from string resources with fixed English values. Putting
/// the program language over the same keys translates them, at start and each time the language changes.
/// </summary>
public static class DockTexts
{
    /// <summary>The resource keys of the library's texts; each has the language key "dock.&lt;key&gt;".</summary>
    public static readonly string[] Keys =
    [
        "DragPreviewControlFloatString", "DragPreviewControlNoneString",
        "DocumentTabStripItemFloatString", "DocumentTabStripItemFloatAllString", "DocumentTabStripItemCloseString",
        "DocumentTabStripItemCloseOtherTabsString", "DocumentTabStripItemCloseAllTabsString",
        "DocumentTabStripItemCloseTabsLeftString", "DocumentTabStripItemCloseTabsRightString",
        "DocumentTabStripItemCloseTabsAboveString", "DocumentTabStripItemCloseTabsBelowString",
        "DocumentTabStripItemNewHorizontalDockString", "DocumentTabStripItemNewVerticalDockString",
        "DocumentTabStripItemTabLayoutString", "DocumentTabStripItemTabLayoutLeftString", "DocumentTabStripItemTabLayoutTopString", "DocumentTabStripItemTabLayoutRightString",
        "DocumentTabStripItemLayoutModeString", "DocumentTabStripItemLayoutModeTabbedString", "DocumentTabStripItemLayoutModeMdiString",
        "ToolPinItemControlShowString", "ToolPinItemControlDockAsDocumentString", "ToolChromeControlDockString", "ToolChromeControlAutoHideString",
        // Texts of this program for the panel header buttons (see Styles.axaml).
        "StudioToolPinTip", "StudioToolUnpinTip", "StudioToolMenuTip", "StudioToolCloseTip", "StudioToolSlideTip",
    ];

    private static ResourceDictionary? _texts;

    public static void Apply()
    {
        if (Application.Current?.Resources is not { } resources) return;
        // One dictionary swapped in as a whole: setting 24 resources one by one made every open window look its resources up 24 times.
        var texts = new ResourceDictionary();
        foreach (var key in Keys) texts[key] = Loc.Current["dock." + key];
        // Swapping the dictionary tells every open window to look its resources up again: skip it when no text changed (the language was set to what it already was).
        if (_texts is not null && resources.MergedDictionaries.Contains(_texts) && Keys.All(k => _texts.TryGetValue(k, out var old) && Equals(old, texts[k]))) return;
        var merged = resources.MergedDictionaries;
        if (_texts is not null) merged.Remove(_texts);
        merged.Add(_texts = texts);
    }

    /// <summary>Applies now and again whenever the language changes.</summary>
    public static void Follow()
    {
        Apply();
        Loc.Current.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Localizer.Language)) Apply();
        };
    }
}
