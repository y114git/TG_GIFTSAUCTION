using Avalonia;
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
    ];

    public static void Apply()
    {
        if (Application.Current?.Resources is not { } resources) return;
        foreach (var key in Keys)
        {
            // Only a text that changed is put over the resource: setting one makes every open window look its texts up again.
            var text = Loc.Current["dock." + key];
            if (!(resources.TryGetValue(key, out var current) && Equals(current, text))) resources[key] = text;
        }
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
