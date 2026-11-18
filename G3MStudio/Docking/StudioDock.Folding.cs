using Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using G3MStudio.Core.Settings;

namespace G3MStudio.Docking;

/// <summary>Folding the docks: a fold gives a dock a share of 0 and keeps its old share for opening it again.</summary>
public sealed partial class StudioDock
{
    private static readonly string[] DockIds = ["LeftDock", "RightDock", "BottomDock"];

    private static string DockOf(string side) => side switch { "left" => "LeftDock", "right" => "RightDock", _ => "BottomDock" };

    /// <summary>Share a folded dock keeps; the chevron buttons of the window sit over this strip.</summary>
    public const double FoldedShare = 0.0;

    /// <summary>Narrowest a panel can be dragged, so checkboxes and captions keep whole words.</summary>
    public const double MinPanelWidth = 160;

    /// <summary>Smallest a floating panel window and a bottom dock can be; below it the panel scrolls (see <see cref="PanelHost"/>). A bottom dock needs its header, its tab strip and room for the content between them, or the chrome overlaps.</summary>
    public const double MinFloatWidth = 320, MinFloatHeight = 220, MinBottomHeight = 140;

    // A panel is never squeezed below this size: a smaller space scrolls it. The reference needs room for its list beside the detail text.
    private static MinSizeScroller PanelHost(string? id, Control panel) => id == "Reference" ? new MinSizeScroller(panel, 360, 220) : new MinSizeScroller(panel, MinPanelWidth, 160);

    public bool IsFolded(string side) => Find<IToolDock>(DockOf(side)) is { } dock && dock.Proportion <= FoldedShare;

    /// <summary>Folds the dock of a side ("left", "right", "bottom") to nothing, or gives it back the share it had.</summary>
    public void ToggleDock(string side)
    {
        if (Find<IToolDock>(DockOf(side)) is not { } dock) return;
        if (dock.Proportion > FoldedShare && FoldAway(dock, () => Apply(dock))) return;
        Apply(dock);
    }

    /// <summary>The default layout of a short window: the bottom dock starts folded so the editors keep the height. A layout the user saved is not passed through here.</summary>
    public void FoldBottomForHeight(double layoutHeight)
    {
        if (layoutHeight < UiSizing.FoldBottomBelow && Find<IToolDock>(DockOf("bottom")) is { } dock && dock.Proportion > FoldedShare) Apply(dock);
    }

    private void Apply(IToolDock dock)
    {
        if (dock.Proportion <= FoldedShare)
        {
            dock.Proportion = dock.CollapsedProportion > FoldedShare ? dock.CollapsedProportion : DefaultShare(dock.Id);
        }
        else
        {
            dock.CollapsedProportion = dock.Proportion;
            dock.Proportion = FoldedShare;
        }

        LimitWidth(dock);
        Balance();
        FoldMotion(dock);
        Changed?.Invoke();
    }

    // A folded dock must be able to shrink to nothing, so only an open side dock keeps the minimum.
    private static void LimitWidth(IToolDock dock)
    {
        var open = dock.Proportion > FoldedShare;
        dock.MinWidth = dock.Alignment is Alignment.Left or Alignment.Right && open ? MinPanelWidth : 0;
        dock.MinHeight = dock.Alignment is Alignment.Bottom && open ? MinBottomHeight : 0;
    }

    // The library scales the shares to their sum, which would drift the side docks on each fold; the centre takes what the side docks leave.
    private void Balance()
    {
        if (Find<IDock>("Centre") is { } centre && Find<IToolDock>("LeftDock") is { } left && Find<IToolDock>("RightDock") is { } right) centre.Proportion = Math.Max(0.1, 1 - left.Proportion - right.Proportion);
        if (Find<IDock>("Editors") is { } editors && Find<IToolDock>("BottomDock") is { } bottom) editors.Proportion = Math.Max(0.1, 1 - bottom.Proportion);
    }

    private static double DefaultShare(string? id) => id switch { "LeftDock" => UiSizing.ExplorerShare, "RightDock" => UiSizing.InspectorShare, _ => UiSizing.BottomShare };

    /// <summary>True when every dock is folded.</summary>
    public bool AllFolded => DockIds.All(id => IsFolded(Side(id)));

    private static string Side(string id) => id[..^4].ToLowerInvariant();

    /// <summary>Folds all docks, or opens them all when every one is folded (and opens all when only some are folded).</summary>
    public void ToggleAll()
    {
        var fold = DockIds.Any(id => !IsFolded(Side(id)));
        foreach (var id in DockIds.Where(id => IsFolded(Side(id)) != fold)) ToggleDock(Side(id));
    }

    /// <summary>Opens every panel in its dock and gives every folded dock its share back.</summary>
    public void ShowAll()
    {
        foreach (var id in DockIds)
            if (IsFolded(Side(id))) ToggleDock(Side(id));
        foreach (var panel in Panels) ShowPanel(panel);
    }

    /// <summary>Back to the default placement; every panel returns, none is lost. Open editors stay (design/shell.md section 10 item 7).</summary>
    public void Reset()
    {
        var tabs = Tabs.Tabs.ToList();
        Use(Create());
        foreach (var tab in tabs) AddDocument(tab, Group());
    }
}
