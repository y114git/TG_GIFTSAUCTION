using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>F6 and Shift+F6: the keyboard focus jumps between the window regions (design/ux-standards.md, design/shell.md section 3).</summary>
public sealed partial class MainWindow
{
    private IReadOnlyList<(string Key, Control? Root)> Regions() =>
    [
        ("view.region.menu", MainMenu),
        DockRegion("LeftDock"),
        ("view.region.editor", Docking.DocumentOf(Docking.ActiveTabId ?? "")?.Content as Control),
        DockRegion("RightDock"),
        DockRegion("BottomDock", "view.region.bottom"),
        ("view.region.status", StatusBar),
    ];

    /// <summary>The active panel of a tool dock; the region is named after the panel unless a fixed key is given.</summary>
    private (string Key, Control? Root) DockRegion(string dock, string? key = null) =>
        Docking.Find<IToolDock>(dock)?.ActiveDockable is Tool { Content: Control panel } tool ? (key ?? "panel." + tool.Id.ToLowerInvariant(), panel) : (key ?? "", null);

    public void FocusRegion(int direction)
    {
        var regions = new List<(string Key, Control Root)>();
        foreach (var region in Regions())
            if (region.Root is { IsEffectivelyVisible: true } visible) regions.Add((region.Key, visible));
        if (regions.Count == 0) return;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var at = regions.FindIndex(r => focused is not null && (focused == r.Root || r.Root.IsVisualAncestorOf(focused)));
        var (key, root) = regions[at < 0 ? (direction > 0 ? 0 : regions.Count - 1) : (at + direction + regions.Count) % regions.Count];
        var target = root is { Focusable: true, IsEffectivelyEnabled: true } ? root : root.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled) ?? root;
        target.Focus(NavigationMethod.Tab); // Tab-style focus draws the focus ring
        RegionAnnouncement.Text = Loc.Current.Format("view.regionMoved", ("region", Loc.Current[key]));
    }
}
