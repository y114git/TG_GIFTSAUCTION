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

    public bool IsFolded(string side) => Find<IToolDock>(DockOf(side)) is { } dock && dock.Proportion <= FoldedShare;

    /// <summary>Folds the dock of a side ("left", "right", "bottom") to nothing, or gives it back the share it had.</summary>
    public void ToggleDock(string side)
    {
        if (Find<IToolDock>(DockOf(side)) is not { } dock) return;
        if (dock.Proportion <= FoldedShare)
        {
            dock.Proportion = dock.CollapsedProportion > FoldedShare ? dock.CollapsedProportion : DefaultShare(dock.Id);
        }
        else
        {
            dock.CollapsedProportion = dock.Proportion;
            dock.Proportion = FoldedShare;
        }

        Balance();
        Changed?.Invoke();
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
